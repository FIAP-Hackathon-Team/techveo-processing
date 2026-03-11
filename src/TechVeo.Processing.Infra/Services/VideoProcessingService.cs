using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TechVeo.Processing.Application.Services;

namespace TechVeo.Processing.Infra.Services;

public class VideoProcessingService : IVideoProcessingService
{
    private readonly ILogger<VideoProcessingService> _logger;
    private readonly IProcessRunner _processRunner;

    public VideoProcessingService(ILogger<VideoProcessingService> logger, IProcessRunner processRunner)
    {
        _logger = logger;
        _processRunner = processRunner;
    }

    public async Task<IReadOnlyList<(Stream Stream, string FileName)>> ExtractSnapshotsAsync(
        Stream videoStream,
        int? snapshotCount = null,
        double? intervalSeconds = null,
        int? width = null,
        int? height = null,
        CancellationToken cancellationToken = default)
    {
        var tempVideoPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.mp4");
        var tempSnapshotDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempSnapshotDir);

        try
        {
            _logger.LogInformation("Saving video to temporary file: {TempVideoPath}", tempVideoPath);
            await using (var fileStream = File.Create(tempVideoPath))
            {
                await videoStream.CopyToAsync(fileStream, cancellationToken);
            }

            var duration = await GetVideoDurationAsync(tempVideoPath, cancellationToken);

            int finalSnapshotCount = snapshotCount ?? 5;
            double interval;

            if (intervalSeconds.HasValue && intervalSeconds.Value > 0)
            {
                interval = intervalSeconds.Value;
                finalSnapshotCount = Math.Max(1, (int)Math.Floor(duration / interval));
            }
            else
            {
                interval = duration / (finalSnapshotCount + 1);
            }

            _logger.LogInformation("Video duration: {Duration}s, extracting {Count} snapshots with interval {Interval}s",
                duration, snapshotCount, interval);

            var snapshots = new List<(Stream Stream, string FileName)>();

            for (int i = 1; i <= finalSnapshotCount; i++)
            {
                var timestamp = interval * i;
                var fileName = $"snapshot_{i:D3}.jpg";
                var snapshotPath = Path.Combine(tempSnapshotDir, fileName);

                await ExtractSnapshotAtTimestampAsync(tempVideoPath, snapshotPath, timestamp, width, height, cancellationToken);

                var memoryStream = new MemoryStream();
                await using (var fileStream = File.OpenRead(snapshotPath))
                {
                    await fileStream.CopyToAsync(memoryStream, cancellationToken);
                }
                memoryStream.Position = 0;
                snapshots.Add((memoryStream, fileName));

                _logger.LogInformation("Snapshot {Index} extracted at {Timestamp}s", i, timestamp);
            }

            return snapshots;
        }
        finally
        {
            if (File.Exists(tempVideoPath))
                File.Delete(tempVideoPath);

            if (Directory.Exists(tempSnapshotDir))
                Directory.Delete(tempSnapshotDir, true);
        }
    }

    public async Task<IReadOnlyList<(Stream Stream, string FileName)>> ExtractSnapshotsAtTimestampsAsync(
        Stream videoStream,
        IReadOnlyList<double> timestamps,
        int? width = null,
        int? height = null,
        CancellationToken cancellationToken = default)
    {
        var tempVideoPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.mp4");
        var tempSnapshotDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempSnapshotDir);

        try
        {
            _logger.LogInformation("Saving video to temporary file: {TempVideoPath}", tempVideoPath);
            await using (var fileStream = File.Create(tempVideoPath))
            {
                await videoStream.CopyToAsync(fileStream, cancellationToken);
            }

            var snapshots = new List<(Stream Stream, string FileName)>();
            int index = 1;
            foreach (var ts in timestamps)
            {
                var fileName = $"snapshot_{index:D3}.jpg";
                var snapshotPath = Path.Combine(tempSnapshotDir, fileName);

                await ExtractSnapshotAtTimestampAsync(tempVideoPath, snapshotPath, ts, width, height, cancellationToken);

                var memoryStream = new MemoryStream();
                await using (var fileStream = File.OpenRead(snapshotPath))
                {
                    await fileStream.CopyToAsync(memoryStream, cancellationToken);
                }
                memoryStream.Position = 0;
                snapshots.Add((memoryStream, fileName));

                _logger.LogInformation("Snapshot {Index} extracted at {Timestamp}s", index, ts);
                index++;
            }

            return snapshots;
        }
        finally
        {
            if (File.Exists(tempVideoPath))
                File.Delete(tempVideoPath);

            if (Directory.Exists(tempSnapshotDir))
                Directory.Delete(tempSnapshotDir, true);
        }
    }

    private async Task<double> GetVideoDurationAsync(string videoPath, CancellationToken cancellationToken)
    {
        var args = $"-v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 \"{videoPath}\"";
        var result = await _processRunner.RunAsync("ffprobe", args, cancellationToken);

        if (result.ExitCode != 0)
            throw new InvalidOperationException($"Failed to get video duration: {result.StdErr}");

        return double.Parse(result.StdOut.Trim(), CultureInfo.InvariantCulture);
    }

    private async Task ExtractSnapshotAtTimestampAsync(
        string videoPath,
        string outputPath,
        double timestamp,
        int? width,
        int? height,
        CancellationToken cancellationToken)
    {
        var args = BuildFfmpegArguments(videoPath, outputPath, timestamp, width, height);
        var result = await _processRunner.RunAsync("ffmpeg", args, cancellationToken);

        if (result.ExitCode != 0)
            throw new InvalidOperationException($"Failed to extract snapshot: {result.StdErr}");
    }

    internal static string BuildFfmpegArguments(string videoPath, string outputPath, double timestamp, int? width, int? height)
    {
        var ts = timestamp.ToString("F2", CultureInfo.InvariantCulture);

        if (width.HasValue || height.HasValue)
        {
            var w = width.HasValue ? width.Value.ToString() : "-1";
            var h = height.HasValue ? height.Value.ToString() : "-1";
            var scale = $"scale={w}:{h}";
            return $"-nostdin -y -hide_banner -loglevel error -ss {ts} -i \"{videoPath}\" -vf \"{scale}\" -frames:v 1 -q:v 2 \"{outputPath}\"";
        }

        return $"-nostdin -y -hide_banner -loglevel error -ss {ts} -i \"{videoPath}\" -vframes 1 -q:v 2 \"{outputPath}\"";
    }
}
