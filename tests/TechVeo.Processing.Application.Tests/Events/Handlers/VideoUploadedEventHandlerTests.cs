using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;
using TechVeo.Processing.Application.Clients;
using TechVeo.Processing.Application.Events.Integration.Incoming;
using TechVeo.Processing.Application.Events.Integration.Incoming.Handlers;
using TechVeo.Processing.Application.Events.Integration.Outgoing;
using TechVeo.Processing.Application.Services;
using TechVeo.Shared.Application.Storage;

namespace TechVeo.Processing.Application.Tests.Events.Handlers;

public class VideoUploadedEventHandlerTests
{
    private readonly Mock<IMediator> _mediatorMock;
    private readonly Mock<IVideoStorage> _videoStorageMock;
    private readonly Mock<IVideoProcessingService> _videoProcessingServiceMock;
    private readonly Mock<IGenerativeClient> _generativeClientMock;
    private readonly Mock<ILogger<VideoUploadedEventHandler>> _loggerMock;
    private readonly VideoUploadedEventHandler _handler;

    public VideoUploadedEventHandlerTests()
    {
        _mediatorMock = new Mock<IMediator>();
        _videoStorageMock = new Mock<IVideoStorage>();
        _videoProcessingServiceMock = new Mock<IVideoProcessingService>();
        _generativeClientMock = new Mock<IGenerativeClient>();
        _loggerMock = new Mock<ILogger<VideoUploadedEventHandler>>();

        _handler = new VideoUploadedEventHandler(
            _mediatorMock.Object,
            _videoStorageMock.Object,
            _videoProcessingServiceMock.Object,
            _generativeClientMock.Object,
            _loggerMock.Object);
    }

    [Fact(DisplayName = "Should handle video processing")]
    [Trait("Application", "VideoUploadedEventHandler")]
    public async Task Handle_WithValidEvent_ShouldProcessVideo()
    {
        // Arrange
        var uploadEvent = CreateVideoUploadedEvent();
        var videoStream = CreateMockVideoStream();

        _videoStorageMock
            .Setup(x => x.DownloadVideoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(videoStream);

        _videoProcessingServiceMock
            .Setup(x => x.ExtractSnapshotsAsync(It.IsAny<Stream>(), It.IsAny<int?>(), It.IsAny<double?>(),
                It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateMockSnapshots(3));

        _videoStorageMock
            .Setup(x => x.UploadSnapshotsAsZipAsync(It.IsAny<IReadOnlyList<(Stream, string)>>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("snapshots.zip");

        _mediatorMock
            .Setup(x => x.Publish(It.IsAny<INotification>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _handler.Handle(uploadEvent, CancellationToken.None);

        // Assert
        _videoStorageMock.Verify(
            x => x.DownloadVideoAsync(uploadEvent.VideoKey, It.IsAny<CancellationToken>()),
            Times.Once);

        _videoProcessingServiceMock.Verify(
            x => x.ExtractSnapshotsAsync(
                It.IsAny<Stream>(),
                It.IsAny<int?>(),
                It.IsAny<double?>(),
                It.IsAny<int?>(),
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact(DisplayName = "Should publish processing started event")]
    [Trait("Application", "VideoUploadedEventHandler")]
    public async Task Handle_ShouldPublishStartedEvent()
    {
        // Arrange
        var uploadEvent = CreateVideoUploadedEvent();
        var videoStream = CreateMockVideoStream();

        _videoStorageMock
            .Setup(x => x.DownloadVideoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(videoStream);

        _videoProcessingServiceMock
            .Setup(x => x.ExtractSnapshotsAsync(It.IsAny<Stream>(), It.IsAny<int?>(), It.IsAny<double?>(),
                It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateMockSnapshots(3));

        _videoStorageMock
            .Setup(x => x.UploadSnapshotsAsZipAsync(It.IsAny<IReadOnlyList<(Stream, string)>>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("snapshots.zip");

        _mediatorMock
            .Setup(x => x.Publish(It.IsAny<INotification>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _handler.Handle(uploadEvent, CancellationToken.None);

        // Assert
        _mediatorMock.Verify(
            x => x.Publish(It.IsAny<VideoProcessingStartedEvent>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact(DisplayName = "Should handle download failure")]
    [Trait("Application", "VideoUploadedEventHandler")]
    public async Task Handle_WithDownloadFailure_ShouldPublishFailedEvent()
    {
        // Arrange
        var uploadEvent = CreateVideoUploadedEvent();

        _videoStorageMock
            .Setup(x => x.DownloadVideoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Download failed"));

        _mediatorMock
            .Setup(x => x.Publish(It.IsAny<INotification>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _handler.Handle(uploadEvent, CancellationToken.None);

        // Assert - Verify failure event was published when download fails
        _mediatorMock.Verify(
            x => x.Publish(It.IsAny<VideoProcessingFailedEvent>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact(DisplayName = "Should handle zip upload failure")]
    [Trait("Application", "VideoUploadedEventHandler")]
    public async Task Handle_WithZipUploadFailure_ShouldPublishFailedEvent()
    {
        // Arrange
        var uploadEvent = CreateVideoUploadedEvent();
        var videoStream = CreateMockVideoStream();

        _videoStorageMock
            .Setup(x => x.DownloadVideoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(videoStream);

        _videoProcessingServiceMock
            .Setup(x => x.ExtractSnapshotsAsync(It.IsAny<Stream>(), It.IsAny<int?>(), It.IsAny<double?>(),
                It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateMockSnapshots(3));

        _videoStorageMock
            .Setup(x => x.UploadSnapshotsAsZipAsync(It.IsAny<IReadOnlyList<(Stream, string)>>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("S3 upload failed"));

        _mediatorMock
            .Setup(x => x.Publish(It.IsAny<INotification>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _handler.Handle(uploadEvent, CancellationToken.None);

        // Assert
        _mediatorMock.Verify(
            x => x.Publish(It.IsAny<VideoProcessingFailedEvent>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact(DisplayName = "Should call upload with filename containing VideoId")]
    [Trait("Application", "VideoUploadedEventHandler")]
    public async Task Handle_ShouldCallUploadWithVideoIdInFilename()
    {
        // Arrange
        var videoId = Guid.NewGuid();
        var uploadEvent = new VideoUploadedEvent(
            videoId,
            Guid.NewGuid(),
            "videos/test.mp4",
            DateTime.UtcNow,
            new VideoUploadedMetadata(1920, 1080, 5, null, null));

        _videoStorageMock
            .Setup(x => x.DownloadVideoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateMockVideoStream());

        _videoProcessingServiceMock
            .Setup(x => x.ExtractSnapshotsAsync(It.IsAny<Stream>(), It.IsAny<int?>(), It.IsAny<double?>(),
                It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateMockSnapshots(1));

        _videoStorageMock
            .Setup(x => x.UploadSnapshotsAsZipAsync(It.IsAny<IReadOnlyList<(Stream, string)>>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("snapshots/result.zip");

        _mediatorMock
            .Setup(x => x.Publish(It.IsAny<INotification>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _handler.Handle(uploadEvent, CancellationToken.None);

        // Assert - zip file name must contain the VideoId
        _videoStorageMock.Verify(
            x => x.UploadSnapshotsAsZipAsync(
                It.IsAny<IReadOnlyList<(Stream, string)>>(),
                It.Is<string>(name => name.Contains(videoId.ToString())),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact(DisplayName = "Should publish completed event with zip key from upload")]
    [Trait("Application", "VideoUploadedEventHandler")]
    public async Task Handle_ShouldPublishCompletedEventWithCorrectZipKey()
    {
        // Arrange
        var uploadEvent = CreateVideoUploadedEvent();
        var expectedZipKey = "snapshots/specific-key.zip";

        _videoStorageMock
            .Setup(x => x.DownloadVideoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateMockVideoStream());

        _videoProcessingServiceMock
            .Setup(x => x.ExtractSnapshotsAsync(It.IsAny<Stream>(), It.IsAny<int?>(), It.IsAny<double?>(),
                It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateMockSnapshots(1));

        _videoStorageMock
            .Setup(x => x.UploadSnapshotsAsZipAsync(It.IsAny<IReadOnlyList<(Stream, string)>>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedZipKey);

        _mediatorMock
            .Setup(x => x.Publish(It.IsAny<INotification>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _handler.Handle(uploadEvent, CancellationToken.None);

        // Assert - VideoProcessingCompletedEvent must carry the zip key returned by upload
        _mediatorMock.Verify(
            x => x.Publish(
                It.Is<VideoProcessingCompletedEvent>(e => e.ZipKey == expectedZipKey),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private VideoUploadedEvent CreateVideoUploadedEvent()
    {
        return new VideoUploadedEvent(
            Guid.NewGuid(),
            Guid.NewGuid(),
            $"videos/test-video-{Guid.NewGuid()}.mp4",
            DateTime.UtcNow,
            new VideoUploadedMetadata(1920, 1080, 5, null, null));
    }

    private IReadOnlyList<(Stream Stream, string FileName)> CreateMockSnapshots(int count)
    {
        var snapshots = new List<(Stream, string)>();
        for (int i = 1; i <= count; i++)
        {
            var stream = new MemoryStream(new byte[] { 0xFF, 0xD8 });
            snapshots.Add((stream, $"snapshot_{i:D3}.jpg"));
        }
        return snapshots.AsReadOnly();
    }

    private Stream CreateMockVideoStream()
    {
        var mockVideoData = new byte[] { 0x00, 0x00, 0x00, 0x20, 0x66, 0x74, 0x79, 0x70 };
        return new MemoryStream(mockVideoData);
    }
}
