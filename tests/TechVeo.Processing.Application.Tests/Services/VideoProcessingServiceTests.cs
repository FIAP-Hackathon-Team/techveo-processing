using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TechVeo.Processing.Application.Services;
using TechVeo.Processing.Infra.Services;
using Xunit;

namespace TechVeo.Processing.Application.Tests.Services;

public class VideoProcessingServiceTests : IDisposable
{
    private readonly Mock<IProcessRunner> _processRunnerMock;
    private readonly Mock<ILogger<VideoProcessingService>> _loggerMock;
    private readonly VideoProcessingService _service;

    private static readonly byte[] FakeJpegData = { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10 };

    public VideoProcessingServiceTests()
    {
        _processRunnerMock = new Mock<IProcessRunner>();
        _loggerMock = new Mock<ILogger<VideoProcessingService>>();
        _service = new VideoProcessingService(_loggerMock.Object, _processRunnerMock.Object);

        // Default: ffprobe returns 30 seconds duration
        SetupFfprobeSuccess("30.0");
        // Default: ffmpeg creates a fake snapshot file
        SetupFfmpegSuccess();
    }

    public void Dispose() { }

    // ─── ExtractSnapshotsAsync ───────────────────────────────────────────────

    [Fact(DisplayName = "Should extract correct number of snapshots (default 5)")]
    [Trait("Infra", "VideoProcessingService")]
    public async Task ExtractSnapshotsAsync_WithDefaultCount_ShouldReturn5Snapshots()
    {
        // Arrange
        var stream = CreateVideoStream();

        // Act
        var snapshots = await _service.ExtractSnapshotsAsync(stream, cancellationToken: CancellationToken.None);

        // Assert
        snapshots.Should().HaveCount(5);
    }

    [Fact(DisplayName = "Should extract specified snapshot count")]
    [Trait("Infra", "VideoProcessingService")]
    public async Task ExtractSnapshotsAsync_WithSnapshotCount_ShouldReturnCorrectCount()
    {
        // Arrange
        var stream = CreateVideoStream();

        // Act
        var snapshots = await _service.ExtractSnapshotsAsync(stream, snapshotCount: 3, cancellationToken: CancellationToken.None);

        // Assert
        snapshots.Should().HaveCount(3);
    }

    [Fact(DisplayName = "Should use interval seconds when provided")]
    [Trait("Infra", "VideoProcessingService")]
    public async Task ExtractSnapshotsAsync_WithIntervalSeconds_ShouldComputeCountFromDuration()
    {
        // Arrange – 30s duration, 10s interval → 3 snapshots
        SetupFfprobeSuccess("30.0");
        var stream = CreateVideoStream();

        // Act
        var snapshots = await _service.ExtractSnapshotsAsync(stream, intervalSeconds: 10.0, cancellationToken: CancellationToken.None);

        // Assert
        snapshots.Should().HaveCount(3);
    }

    [Fact(DisplayName = "Should name snapshots sequentially")]
    [Trait("Infra", "VideoProcessingService")]
    public async Task ExtractSnapshotsAsync_ShouldNameSnapshotsSequentially()
    {
        // Arrange
        var stream = CreateVideoStream();

        // Act
        var snapshots = await _service.ExtractSnapshotsAsync(stream, snapshotCount: 3, cancellationToken: CancellationToken.None);

        // Assert
        snapshots[0].FileName.Should().Be("snapshot_001.jpg");
        snapshots[1].FileName.Should().Be("snapshot_002.jpg");
        snapshots[2].FileName.Should().Be("snapshot_003.jpg");
    }

    [Fact(DisplayName = "Should return readable streams for each snapshot")]
    [Trait("Infra", "VideoProcessingService")]
    public async Task ExtractSnapshotsAsync_ShouldReturnReadableStreams()
    {
        // Arrange
        var stream = CreateVideoStream();

        // Act
        var snapshots = await _service.ExtractSnapshotsAsync(stream, snapshotCount: 2, cancellationToken: CancellationToken.None);

        // Assert
        foreach (var (snapshotStream, _) in snapshots)
        {
            snapshotStream.Should().NotBeNull();
            snapshotStream.CanRead.Should().BeTrue();
            snapshotStream.Position.Should().Be(0);
        }
    }

    [Fact(DisplayName = "Should throw when ffprobe fails")]
    [Trait("Infra", "VideoProcessingService")]
    public async Task ExtractSnapshotsAsync_WhenFfprobeFails_ShouldThrowInvalidOperationException()
    {
        // Arrange
        _processRunnerMock
            .Setup(x => x.RunAsync("ffprobe", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult(1, "", "ffprobe: error reading file"));

        var stream = CreateVideoStream();

        // Act & Assert
        await _service.Invoking(s => s.ExtractSnapshotsAsync(stream, cancellationToken: CancellationToken.None))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*ffprobe*");
    }

    [Fact(DisplayName = "Should throw when ffmpeg fails")]
    [Trait("Infra", "VideoProcessingService")]
    public async Task ExtractSnapshotsAsync_WhenFfmpegFails_ShouldThrowInvalidOperationException()
    {
        // Arrange
        _processRunnerMock
            .Setup(x => x.RunAsync("ffmpeg", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult(1, "", "ffmpeg: error"));

        var stream = CreateVideoStream();

        // Act & Assert
        await _service.Invoking(s => s.ExtractSnapshotsAsync(stream, snapshotCount: 1, cancellationToken: CancellationToken.None))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*snapshot*");
    }

    [Fact(DisplayName = "Should cleanup temp files after successful extraction")]
    [Trait("Infra", "VideoProcessingService")]
    public async Task ExtractSnapshotsAsync_AfterSuccess_ShouldDeleteTempFiles()
    {
        // Arrange
        var stream = CreateVideoStream();
        string? capturedVideoPath = null;

        _processRunnerMock
            .Setup(x => x.RunAsync("ffprobe", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((_, args, _) =>
            {
                // Extract temp video path from args (last quoted arg)
                capturedVideoPath = ExtractLastQuotedArg(args);
            })
            .ReturnsAsync(new ProcessResult(0, "30.0", ""));

        SetupFfmpegSuccess();

        // Act
        await _service.ExtractSnapshotsAsync(stream, snapshotCount: 1, cancellationToken: CancellationToken.None);

        // Assert – temp video file should be deleted
        if (capturedVideoPath != null)
            File.Exists(capturedVideoPath).Should().BeFalse();
    }

    [Fact(DisplayName = "Should use snapshotCount=1 when interval is too large")]
    [Trait("Infra", "VideoProcessingService")]
    public async Task ExtractSnapshotsAsync_WhenIntervalLargerThanDuration_ShouldReturnAtLeastOneSnapshot()
    {
        // Arrange – 10s duration, 60s interval → Math.Max(1, floor(10/60)) = 1 snapshot
        SetupFfprobeSuccess("10.0");
        var stream = CreateVideoStream();

        // Act
        var snapshots = await _service.ExtractSnapshotsAsync(stream, intervalSeconds: 60.0, cancellationToken: CancellationToken.None);

        // Assert
        snapshots.Should().HaveCount(1);
    }

    [Fact(DisplayName = "Should pass width and height to ffmpeg when specified")]
    [Trait("Infra", "VideoProcessingService")]
    public async Task ExtractSnapshotsAsync_WithWidthAndHeight_ShouldPassScaleToFfmpeg()
    {
        // Arrange
        var capturedArgs = new List<string>();
        _processRunnerMock
            .Setup(x => x.RunAsync("ffmpeg", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((_, args, _) =>
            {
                capturedArgs.Add(args);
                var outputPath = ExtractLastQuotedArg(args);
                if (outputPath != null) File.WriteAllBytes(outputPath, FakeJpegData);
            })
            .ReturnsAsync(new ProcessResult(0, "", ""));

        var stream = CreateVideoStream();

        // Act
        await _service.ExtractSnapshotsAsync(stream, snapshotCount: 1, width: 1920, height: 1080, cancellationToken: CancellationToken.None);

        // Assert
        capturedArgs.Should().HaveCount(1);
        capturedArgs[0].Should().Contain("scale=1920:1080");
    }

    // ─── ExtractSnapshotsAtTimestampsAsync ──────────────────────────────────

    [Fact(DisplayName = "Should extract snapshot for each timestamp")]
    [Trait("Infra", "VideoProcessingService")]
    public async Task ExtractSnapshotsAtTimestampsAsync_WithMultipleTimestamps_ShouldReturnCorrectCount()
    {
        // Arrange
        var timestamps = new List<double> { 5.0, 10.0, 15.0 };
        var stream = CreateVideoStream();

        // Act
        var snapshots = await _service.ExtractSnapshotsAtTimestampsAsync(stream, timestamps, cancellationToken: CancellationToken.None);

        // Assert
        snapshots.Should().HaveCount(3);
    }

    [Fact(DisplayName = "Should return empty list when timestamps list is empty")]
    [Trait("Infra", "VideoProcessingService")]
    public async Task ExtractSnapshotsAtTimestampsAsync_WithEmptyTimestamps_ShouldReturnEmptyList()
    {
        // Arrange
        var timestamps = new List<double>();
        var stream = CreateVideoStream();

        // Act
        var snapshots = await _service.ExtractSnapshotsAtTimestampsAsync(stream, timestamps, cancellationToken: CancellationToken.None);

        // Assert
        snapshots.Should().BeEmpty();
    }

    [Fact(DisplayName = "Should name snapshots sequentially for timestamp extraction")]
    [Trait("Infra", "VideoProcessingService")]
    public async Task ExtractSnapshotsAtTimestampsAsync_ShouldNameSnapshotsSequentially()
    {
        // Arrange
        var timestamps = new List<double> { 5.0, 10.0 };
        var stream = CreateVideoStream();

        // Act
        var snapshots = await _service.ExtractSnapshotsAtTimestampsAsync(stream, timestamps, cancellationToken: CancellationToken.None);

        // Assert
        snapshots[0].FileName.Should().Be("snapshot_001.jpg");
        snapshots[1].FileName.Should().Be("snapshot_002.jpg");
    }

    [Fact(DisplayName = "Should throw when ffmpeg fails in timestamp extraction")]
    [Trait("Infra", "VideoProcessingService")]
    public async Task ExtractSnapshotsAtTimestampsAsync_WhenFfmpegFails_ShouldThrow()
    {
        // Arrange
        _processRunnerMock
            .Setup(x => x.RunAsync("ffmpeg", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult(1, "", "ffmpeg error"));

        var timestamps = new List<double> { 5.0 };
        var stream = CreateVideoStream();

        // Act & Assert
        await _service.Invoking(s => s.ExtractSnapshotsAtTimestampsAsync(stream, timestamps, cancellationToken: CancellationToken.None))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact(DisplayName = "Should cleanup temp files after timestamp extraction")]
    [Trait("Infra", "VideoProcessingService")]
    public async Task ExtractSnapshotsAtTimestampsAsync_AfterSuccess_ShouldDeleteTempFiles()
    {
        // Arrange
        var timestamps = new List<double> { 5.0 };
        var stream = CreateVideoStream();
        string? capturedDir = null;

        _processRunnerMock
            .Setup(x => x.RunAsync("ffmpeg", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((_, args, _) =>
            {
                var outputPath = ExtractLastQuotedArg(args);
                if (outputPath != null)
                {
                    capturedDir = Path.GetDirectoryName(outputPath);
                    File.WriteAllBytes(outputPath, FakeJpegData);
                }
            })
            .ReturnsAsync(new ProcessResult(0, "", ""));

        // Act
        await _service.ExtractSnapshotsAtTimestampsAsync(stream, timestamps, cancellationToken: CancellationToken.None);

        // Assert – temp snapshot dir should be deleted
        if (capturedDir != null)
            Directory.Exists(capturedDir).Should().BeFalse();
    }

    [Fact(DisplayName = "Should pass width and height to ffmpeg for timestamp extraction")]
    [Trait("Infra", "VideoProcessingService")]
    public async Task ExtractSnapshotsAtTimestampsAsync_WithDimensions_ShouldPassScaleToFfmpeg()
    {
        // Arrange
        var capturedArgs = new List<string>();
        _processRunnerMock
            .Setup(x => x.RunAsync("ffmpeg", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((_, args, _) =>
            {
                capturedArgs.Add(args);
                var outputPath = ExtractLastQuotedArg(args);
                if (outputPath != null) File.WriteAllBytes(outputPath, FakeJpegData);
            })
            .ReturnsAsync(new ProcessResult(0, "", ""));

        var timestamps = new List<double> { 5.0 };
        var stream = CreateVideoStream();

        // Act
        await _service.ExtractSnapshotsAtTimestampsAsync(stream, timestamps, width: 1280, height: 720, cancellationToken: CancellationToken.None);

        // Assert
        capturedArgs[0].Should().Contain("scale=1280:720");
    }

    // ─── BuildFfmpegArguments (internal static) ──────────────────────────────

    [Fact(DisplayName = "Should build args without scale filter when no dimensions specified")]
    [Trait("Infra", "VideoProcessingService")]
    public void BuildFfmpegArguments_WithoutDimensions_ShouldUseVframes()
    {
        // Act
        var args = VideoProcessingService.BuildFfmpegArguments("/video.mp4", "/snap.jpg", 5.25, null, null);

        // Assert
        args.Should().Contain("-vframes 1");
        args.Should().NotContain("-vf \"scale=");
        args.Should().Contain("-ss 5.25");
    }

    [Fact(DisplayName = "Should build args with scale filter when both dimensions specified")]
    [Trait("Infra", "VideoProcessingService")]
    public void BuildFfmpegArguments_WithBothDimensions_ShouldIncludeScaleFilter()
    {
        // Act
        var args = VideoProcessingService.BuildFfmpegArguments("/video.mp4", "/snap.jpg", 10.0, 1920, 1080);

        // Assert
        args.Should().Contain("scale=1920:1080");
        args.Should().Contain("-frames:v 1");
        args.Should().NotContain("-vframes 1");
    }

    [Fact(DisplayName = "Should use -1 for height when only width is specified")]
    [Trait("Infra", "VideoProcessingService")]
    public void BuildFfmpegArguments_WithWidthOnly_ShouldUseMinusOneForHeight()
    {
        // Act
        var args = VideoProcessingService.BuildFfmpegArguments("/video.mp4", "/snap.jpg", 10.0, 1920, null);

        // Assert
        args.Should().Contain("scale=1920:-1");
    }

    [Fact(DisplayName = "Should use -1 for width when only height is specified")]
    [Trait("Infra", "VideoProcessingService")]
    public void BuildFfmpegArguments_WithHeightOnly_ShouldUseMinusOneForWidth()
    {
        // Act
        var args = VideoProcessingService.BuildFfmpegArguments("/video.mp4", "/snap.jpg", 10.0, null, 1080);

        // Assert
        args.Should().Contain("scale=-1:1080");
    }

    [Fact(DisplayName = "Should format timestamp with 2 decimal places")]
    [Trait("Infra", "VideoProcessingService")]
    public void BuildFfmpegArguments_ShouldFormatTimestampWithTwoDecimals()
    {
        // Act
        var args = VideoProcessingService.BuildFfmpegArguments("/v.mp4", "/s.jpg", 5.0, null, null);

        // Assert
        args.Should().Contain("-ss 5.00");
    }

    [Fact(DisplayName = "Should include video path and output path in quotes")]
    [Trait("Infra", "VideoProcessingService")]
    public void BuildFfmpegArguments_ShouldIncludePathsInQuotes()
    {
        // Act
        var args = VideoProcessingService.BuildFfmpegArguments("/my/video.mp4", "/my/snap.jpg", 1.0, null, null);

        // Assert
        args.Should().Contain("\"/my/video.mp4\"");
        args.Should().Contain("\"/my/snap.jpg\"");
    }

    [Fact(DisplayName = "Should include quality flag -q:v 2")]
    [Trait("Infra", "VideoProcessingService")]
    public void BuildFfmpegArguments_ShouldIncludeQualityFlag()
    {
        // Act
        var argsNoScale = VideoProcessingService.BuildFfmpegArguments("/v.mp4", "/s.jpg", 1.0, null, null);
        var argsWithScale = VideoProcessingService.BuildFfmpegArguments("/v.mp4", "/s.jpg", 1.0, 1920, 1080);

        // Assert
        argsNoScale.Should().Contain("-q:v 2");
        argsWithScale.Should().Contain("-q:v 2");
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private void SetupFfprobeSuccess(string duration)
    {
        _processRunnerMock
            .Setup(x => x.RunAsync("ffprobe", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult(0, duration, ""));
    }

    private void SetupFfmpegSuccess()
    {
        _processRunnerMock
            .Setup(x => x.RunAsync("ffmpeg", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((_, args, _) =>
            {
                var outputPath = ExtractLastQuotedArg(args);
                if (outputPath != null)
                    File.WriteAllBytes(outputPath, FakeJpegData);
            })
            .ReturnsAsync(new ProcessResult(0, "", ""));
    }

    private static MemoryStream CreateVideoStream()
        => new(Encoding.UTF8.GetBytes("fake-video-data"));

    private static string? ExtractLastQuotedArg(string args)
    {
        var parts = args.Split('"');
        return parts.Length >= 2 ? parts[^2] : null;
    }
}
