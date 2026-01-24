using System;
using System.Globalization;
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
        int? snapshotCount = null,
        double? intervalSeconds = null,
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
                // compute how many snapshots will fit in the duration using the interval
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

        return double.Parse(output.Trim(), CultureInfo.InvariantCulture);
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
            // prevent ffmpeg from waiting for stdin and reduce log verbosity
            Arguments = $"-nostdin -y -hide_banner -loglevel error -ss {timestamp.ToString("F2", CultureInfo.InvariantCulture)} -i \"{videoPath}\" -vframes 1 -q:v 2 \"{outputPath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        var stdOutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stdErrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        var waitTask = process.WaitForExitAsync(cancellationToken);

        try
        {
            // Wait for process exit and stream reads to complete to avoid deadlocks when buffers fill
            await Task.WhenAll(waitTask, stdOutTask, stdErrTask);
        }
        catch (OperationCanceledException)
        {
            // If the operation was canceled, ensure the child process is terminated to avoid orphan processes
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
                // ignore kill failures
            }

            throw;
        }

        if (process.ExitCode != 0)
        {
            var error = await stdErrTask;
            throw new InvalidOperationException($"Failed to extract snapshot: {error}");
        }
    }
}
