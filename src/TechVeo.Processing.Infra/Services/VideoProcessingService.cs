using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TechVeo.Processing.Application.Services;

namespace TechVeo.Processing.Infra.Services;

public class VideoProcessingService : IVideoProcessingService
{
    private readonly ILogger<VideoProcessingService> _logger;

    public VideoProcessingService(ILogger<VideoProcessingService> logger)
    {
        _logger = logger;
    }

    public async Task<IReadOnlyList<(Stream Stream, string FileName)>> ExtractSnapshotsAsync(
        Stream videoStream,
        int snapshotCount = 5,
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
            var interval = duration / (snapshotCount + 1);

            _logger.LogInformation("Video duration: {Duration}s, extracting {Count} snapshots with interval {Interval}s",
                duration, snapshotCount, interval);

            var snapshots = new List<(Stream Stream, string FileName)>();

            for (int i = 1; i <= snapshotCount; i++)
            {
                var timestamp = interval * i;
                var fileName = $"snapshot_{i:D3}.jpg";
                var snapshotPath = Path.Combine(tempSnapshotDir, fileName);

                await ExtractSnapshotAtTimestampAsync(tempVideoPath, snapshotPath, timestamp, cancellationToken);

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
            {
                File.Delete(tempVideoPath);
            }

            if (Directory.Exists(tempSnapshotDir))
            {
                Directory.Delete(tempSnapshotDir, true);
            }
        }
    }

    private static async Task<double> GetVideoDurationAsync(string videoPath, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "ffprobe",
            Arguments = $"-v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 \"{videoPath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            var error = await process.StandardError.ReadToEndAsync(cancellationToken);
            throw new InvalidOperationException($"Failed to get video duration: {error}");
        }

        return double.Parse(output.Trim());
    }

    private static async Task ExtractSnapshotAtTimestampAsync(
        string videoPath,
        string outputPath,
        double timestamp,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "ffmpeg",
            Arguments = $"-ss {timestamp:F2} -i \"{videoPath}\" -vframes 1 -q:v 2 \"{outputPath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            var error = await process.StandardError.ReadToEndAsync(cancellationToken);
            throw new InvalidOperationException($"Failed to extract snapshot: {error}");
        }
    }
}
