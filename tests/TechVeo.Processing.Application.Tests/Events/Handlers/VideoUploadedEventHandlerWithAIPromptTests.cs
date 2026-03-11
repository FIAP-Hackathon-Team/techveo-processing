using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;
using TechVeo.Processing.Application.Clients;
using TechVeo.Processing.Application.Events.Integration.Incoming;
using TechVeo.Processing.Application.Events.Integration.Incoming.Handlers;
using TechVeo.Processing.Application.Events.Integration.Outgoing;
using TechVeo.Processing.Application.Services;
using TechVeo.Shared.Application.Storage;
using Xunit;

namespace TechVeo.Processing.Application.Tests.Events.Handlers;

public class VideoUploadedEventHandlerWithAIPromptTests
{
    private readonly Mock<IMediator> _mediatorMock;
    private readonly Mock<IVideoStorage> _videoStorageMock;
    private readonly Mock<IVideoProcessingService> _videoProcessingServiceMock;
    private readonly Mock<IGenerativeClient> _generativeClientMock;
    private readonly Mock<ILogger<VideoUploadedEventHandler>> _loggerMock;
    private readonly VideoUploadedEventHandler _handler;

    public VideoUploadedEventHandlerWithAIPromptTests()
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

    [Fact(DisplayName = "Should process video with AI prompt and extract key moments")]
    [Trait("Application", "VideoUploadedEventHandlerWithAI")]
    public async Task Handle_WithAIPrompt_ShouldExtractKeyMoments()
    {
        // Arrange
        var videoId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var @event = new VideoUploadedEvent(
            videoId,
            userId,
            "videos/test.mp4",
            DateTime.UtcNow,
            new VideoUploadedMetadata(1920, 1080, 5, 10.5, "Find key moments"));

        var mockStream = new MemoryStream();
        _videoStorageMock
            .Setup(x => x.DownloadVideoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockStream);

        var moments = new List<(double, string)>
        {
            (5.0, "Moment 1"),
            (10.0, "Moment 2"),
            (15.0, "Moment 3")
        };

        _generativeClientMock
            .Setup(x => x.ExtractKeyMomentsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(moments.AsReadOnly());

        var snapshots = new List<(Stream, string)>
        {
            (new MemoryStream(), "snapshot_001.jpg"),
            (new MemoryStream(), "snapshot_002.jpg"),
            (new MemoryStream(), "snapshot_003.jpg")
        };

        _videoProcessingServiceMock
            .Setup(x => x.ExtractSnapshotsAtTimestampsAsync(
                It.IsAny<Stream>(),
                It.IsAny<IReadOnlyList<double>>(),
                It.IsAny<int?>(),
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshots.AsReadOnly());

        _videoStorageMock
            .Setup(x => x.UploadSnapshotsAsZipAsync(
                It.IsAny<List<(Stream, string)>>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("snapshots/test.zip");

        _mediatorMock
            .Setup(x => x.Publish(It.IsAny<INotification>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _handler.Handle(@event, CancellationToken.None);

        // Assert
        _generativeClientMock.Verify(
            x => x.ExtractKeyMomentsAsync(It.IsAny<string>(), It.Is<string>(s => s.Contains("Find key moments")), It.IsAny<CancellationToken>()),
            Times.Once);

        _videoProcessingServiceMock.Verify(
            x => x.ExtractSnapshotsAtTimestampsAsync(
                mockStream,
                It.Is<IReadOnlyList<double>>(list => list.Count == 3),
                It.IsAny<int?>(),
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        _mediatorMock.Verify(
            x => x.Publish(It.IsAny<VideoProcessingStartedEvent>(), It.IsAny<CancellationToken>()),
            Times.Once);

        _mediatorMock.Verify(
            x => x.Publish(It.IsAny<VideoSnapshotsGeneratedEvent>(), It.IsAny<CancellationToken>()),
            Times.Once);

        _mediatorMock.Verify(
            x => x.Publish(It.IsAny<VideoZipGeneratedEvent>(), It.IsAny<CancellationToken>()),
            Times.Once);

        _mediatorMock.Verify(
            x => x.Publish(It.IsAny<VideoProcessingCompletedEvent>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact(DisplayName = "Should process video with AI prompt but no moments returned")]
    [Trait("Application", "VideoUploadedEventHandlerWithAI")]
    public async Task Handle_WithAIPromptButNoMoments_ShouldFallbackToDefault()
    {
        // Arrange
        var videoId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var @event = new VideoUploadedEvent(
            videoId,
            userId,
            "videos/test.mp4",
            DateTime.UtcNow,
            new VideoUploadedMetadata(1920, 1080, 5, 10.5, "Find key moments"));

        var mockStream = new MemoryStream();
        _videoStorageMock
            .Setup(x => x.DownloadVideoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockStream);

        _generativeClientMock
            .Setup(x => x.ExtractKeyMomentsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<(double, string)>().AsReadOnly());

        var snapshots = new List<(Stream, string)>
        {
            (new MemoryStream(), "snapshot_001.jpg"),
            (new MemoryStream(), "snapshot_002.jpg")
        };

        _videoProcessingServiceMock
            .Setup(x => x.ExtractSnapshotsAsync(
                It.IsAny<Stream>(),
                It.IsAny<int?>(),
                It.IsAny<double?>(),
                It.IsAny<int?>(),
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshots.AsReadOnly());

        _videoStorageMock
            .Setup(x => x.UploadSnapshotsAsZipAsync(
                It.IsAny<List<(Stream, string)>>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("snapshots/test.zip");

        _mediatorMock
            .Setup(x => x.Publish(It.IsAny<INotification>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _handler.Handle(@event, CancellationToken.None);

        // Assert - Should fallback to default extraction
        _videoProcessingServiceMock.Verify(
            x => x.ExtractSnapshotsAsync(
                mockStream,
                It.IsAny<int?>(),
                It.IsAny<double?>(),
                It.IsAny<int?>(),
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact(DisplayName = "Should process video without AI prompt using default settings")]
    [Trait("Application", "VideoUploadedEventHandlerWithAI")]
    public async Task Handle_WithoutAIPrompt_ShouldUseDefaultExtraction()
    {
        // Arrange
        var videoId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var @event = new VideoUploadedEvent(
            videoId,
            userId,
            "videos/test.mp4",
            DateTime.UtcNow,
            new VideoUploadedMetadata(1920, 1080, 5, 10.5, null));

        var mockStream = new MemoryStream();
        _videoStorageMock
            .Setup(x => x.DownloadVideoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockStream);

        var snapshots = new List<(Stream, string)>
        {
            (new MemoryStream(), "snapshot_001.jpg"),
            (new MemoryStream(), "snapshot_002.jpg")
        };

        _videoProcessingServiceMock
            .Setup(x => x.ExtractSnapshotsAsync(
                It.IsAny<Stream>(),
                It.IsAny<int?>(),
                It.IsAny<double?>(),
                It.IsAny<int?>(),
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshots.AsReadOnly());

        _videoStorageMock
            .Setup(x => x.UploadSnapshotsAsZipAsync(
                It.IsAny<List<(Stream, string)>>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("snapshots/test.zip");

        _mediatorMock
            .Setup(x => x.Publish(It.IsAny<INotification>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _handler.Handle(@event, CancellationToken.None);

        // Assert
        _generativeClientMock.Verify(
            x => x.ExtractKeyMomentsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);

        _videoProcessingServiceMock.Verify(
            x => x.ExtractSnapshotsAsync(
                mockStream,
                5,
                10.5,
                1920,
                1080,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact(DisplayName = "Should handle exception when AI prompt with empty string")]
    [Trait("Application", "VideoUploadedEventHandlerWithAI")]
    public async Task Handle_WithEmptyAIPrompt_ShouldUseDefaultExtraction()
    {
        // Arrange
        var videoId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var @event = new VideoUploadedEvent(
            videoId,
            userId,
            "videos/test.mp4",
            DateTime.UtcNow,
            new VideoUploadedMetadata(1920, 1080, 5, 10.5, "   "));

        var mockStream = new MemoryStream();
        _videoStorageMock
            .Setup(x => x.DownloadVideoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockStream);

        var snapshots = new List<(Stream, string)>
        {
            (new MemoryStream(), "snapshot_001.jpg")
        };

        _videoProcessingServiceMock
            .Setup(x => x.ExtractSnapshotsAsync(
                It.IsAny<Stream>(),
                It.IsAny<int?>(),
                It.IsAny<double?>(),
                It.IsAny<int?>(),
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshots.AsReadOnly());

        _videoStorageMock
            .Setup(x => x.UploadSnapshotsAsZipAsync(
                It.IsAny<List<(Stream, string)>>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("snapshots/test.zip");

        _mediatorMock
            .Setup(x => x.Publish(It.IsAny<INotification>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _handler.Handle(@event, CancellationToken.None);

        // Assert
        _generativeClientMock.Verify(
            x => x.ExtractKeyMomentsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);

        _videoProcessingServiceMock.Verify(
            x => x.ExtractSnapshotsAsync(
                mockStream,
                It.IsAny<int?>(),
                It.IsAny<double?>(),
                It.IsAny<int?>(),
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact(DisplayName = "Should handle AI extraction failure gracefully")]
    [Trait("Application", "VideoUploadedEventHandlerWithAI")]
    public async Task Handle_WithAIExtractionFailure_ShouldPublishFailedEvent()
    {
        // Arrange
        var videoId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var @event = new VideoUploadedEvent(
            videoId,
            userId,
            "videos/test.mp4",
            DateTime.UtcNow,
            new VideoUploadedMetadata(1920, 1080, 5, 10.5, "Find key moments"));

        var mockStream = new MemoryStream();
        _videoStorageMock
            .Setup(x => x.DownloadVideoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockStream);

        _generativeClientMock
            .Setup(x => x.ExtractKeyMomentsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("AI Service error"));

        _mediatorMock
            .Setup(x => x.Publish(It.IsAny<INotification>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _handler.Handle(@event, CancellationToken.None);

        // Assert
        _mediatorMock.Verify(
            x => x.Publish(It.IsAny<VideoProcessingFailedEvent>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact(DisplayName = "Should handle download video failure")]
    [Trait("Application", "VideoUploadedEventHandlerWithAI")]
    public async Task Handle_WithDownloadFailure_ShouldPublishFailedEvent()
    {
        // Arrange
        var videoId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var @event = new VideoUploadedEvent(
            videoId,
            userId,
            "videos/test.mp4",
            DateTime.UtcNow,
            new VideoUploadedMetadata(1920, 1080, 5, 10.5, null));

        _videoStorageMock
            .Setup(x => x.DownloadVideoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("S3 Download failed"));

        _mediatorMock
            .Setup(x => x.Publish(It.IsAny<INotification>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _handler.Handle(@event, CancellationToken.None);

        // Assert
        _mediatorMock.Verify(
            x => x.Publish(It.IsAny<VideoProcessingFailedEvent>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact(DisplayName = "Should handle snapshot extraction failure")]
    [Trait("Application", "VideoUploadedEventHandlerWithAI")]
    public async Task Handle_WithSnapshotExtractionFailure_ShouldPublishFailedEvent()
    {
        // Arrange
        var videoId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var @event = new VideoUploadedEvent(
            videoId,
            userId,
            "videos/test.mp4",
            DateTime.UtcNow,
            new VideoUploadedMetadata(1920, 1080, 5, 10.5, null));

        var mockStream = new MemoryStream();
        _videoStorageMock
            .Setup(x => x.DownloadVideoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockStream);

        _videoProcessingServiceMock
            .Setup(x => x.ExtractSnapshotsAsync(
                It.IsAny<Stream>(),
                It.IsAny<int?>(),
                It.IsAny<double?>(),
                It.IsAny<int?>(),
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("FFmpeg error"));

        _mediatorMock
            .Setup(x => x.Publish(It.IsAny<INotification>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _handler.Handle(@event, CancellationToken.None);

        // Assert
        _mediatorMock.Verify(
            x => x.Publish(It.IsAny<VideoProcessingFailedEvent>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact(DisplayName = "Should publish all events in correct sequence")]
    [Trait("Application", "VideoUploadedEventHandlerWithAI")]
    public async Task Handle_ShouldPublishAllEventsInSequence()
    {
        // Arrange
        var videoId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var @event = new VideoUploadedEvent(
            videoId,
            userId,
            "videos/test.mp4",
            DateTime.UtcNow,
            new VideoUploadedMetadata(1920, 1080, 5, 10.5, null));

        var mockStream = new MemoryStream();
        _videoStorageMock
            .Setup(x => x.DownloadVideoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockStream);

        var snapshots = new List<(Stream, string)>
        {
            (new MemoryStream(), "snapshot_001.jpg")
        };

        _videoProcessingServiceMock
            .Setup(x => x.ExtractSnapshotsAsync(
                It.IsAny<Stream>(),
                It.IsAny<int?>(),
                It.IsAny<double?>(),
                It.IsAny<int?>(),
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshots.AsReadOnly());

        _videoStorageMock
            .Setup(x => x.UploadSnapshotsAsZipAsync(
                It.IsAny<List<(Stream, string)>>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("snapshots/test.zip");

        _mediatorMock
            .Setup(x => x.Publish(It.IsAny<INotification>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _handler.Handle(@event, CancellationToken.None);

        // Assert - Verify all expected events were published
        _mediatorMock.Verify(
            x => x.Publish(It.IsAny<VideoProcessingStartedEvent>(), It.IsAny<CancellationToken>()),
            Times.Once);

        _mediatorMock.Verify(
            x => x.Publish(It.IsAny<VideoSnapshotsGeneratedEvent>(), It.IsAny<CancellationToken>()),
            Times.Once);

        _mediatorMock.Verify(
            x => x.Publish(It.IsAny<VideoZipGeneratedEvent>(), It.IsAny<CancellationToken>()),
            Times.Once);

        _mediatorMock.Verify(
            x => x.Publish(It.IsAny<VideoProcessingCompletedEvent>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact(DisplayName = "Should handle cancellation token")]
    [Trait("Application", "VideoUploadedEventHandlerWithAI")]
    public async Task Handle_WithCancellationToken_ShouldRespectCancellation()
    {
        // Arrange
        var videoId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var @event = new VideoUploadedEvent(
            videoId,
            userId,
            "videos/test.mp4",
            DateTime.UtcNow,
            new VideoUploadedMetadata(1920, 1080, 5, 10.5, null));

        var cts = new CancellationTokenSource();
        cts.Cancel();

        var mockStream = new MemoryStream();
        _videoStorageMock
            .Setup(x => x.DownloadVideoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockStream);

        _mediatorMock
            .Setup(x => x.Publish(It.IsAny<INotification>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act - should not throw, but handle gracefully
        await _handler.Handle(@event, cts.Token);
    }

    [Fact(DisplayName = "Should handle zip upload failure in AI path")]
    [Trait("Application", "VideoUploadedEventHandlerWithAI")]
    public async Task Handle_WithAIPathAndZipUploadFailure_ShouldPublishFailedEvent()
    {
        // Arrange
        var @event = new VideoUploadedEvent(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "videos/test.mp4",
            DateTime.UtcNow,
            new VideoUploadedMetadata(1920, 1080, 5, 10.5, "Find key moments"));

        var mockStream = new MemoryStream();
        _videoStorageMock
            .Setup(x => x.DownloadVideoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockStream);

        var moments = new List<(double, string)> { (5.0, "Moment 1") };
        _generativeClientMock
            .Setup(x => x.ExtractKeyMomentsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(moments.AsReadOnly());

        _videoProcessingServiceMock
            .Setup(x => x.ExtractSnapshotsAtTimestampsAsync(
                It.IsAny<Stream>(),
                It.IsAny<IReadOnlyList<double>>(),
                It.IsAny<int?>(),
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<(Stream, string)> { (new MemoryStream(), "snapshot_001.jpg") }.AsReadOnly());

        _videoStorageMock
            .Setup(x => x.UploadSnapshotsAsZipAsync(
                It.IsAny<List<(Stream, string)>>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("S3 upload failed in AI path"));

        _mediatorMock
            .Setup(x => x.Publish(It.IsAny<INotification>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _handler.Handle(@event, CancellationToken.None);

        // Assert
        _mediatorMock.Verify(
            x => x.Publish(It.IsAny<VideoProcessingFailedEvent>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact(DisplayName = "Should publish events in correct order in AI path")]
    [Trait("Application", "VideoUploadedEventHandlerWithAI")]
    public async Task Handle_WithAIPath_ShouldPublishEventsInCorrectOrder()
    {
        // Arrange
        var publishedEvents = new List<INotification>();
        var @event = new VideoUploadedEvent(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "videos/test.mp4",
            DateTime.UtcNow,
            new VideoUploadedMetadata(1920, 1080, 5, 10.5, "Find moments"));

        _videoStorageMock
            .Setup(x => x.DownloadVideoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream());

        _generativeClientMock
            .Setup(x => x.ExtractKeyMomentsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<(double, string)> { (5.0, "Moment 1") }.AsReadOnly());

        _videoProcessingServiceMock
            .Setup(x => x.ExtractSnapshotsAtTimestampsAsync(
                It.IsAny<Stream>(),
                It.IsAny<IReadOnlyList<double>>(),
                It.IsAny<int?>(),
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<(Stream, string)> { (new MemoryStream(), "snapshot_001.jpg") }.AsReadOnly());

        _videoStorageMock
            .Setup(x => x.UploadSnapshotsAsZipAsync(
                It.IsAny<List<(Stream, string)>>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("snapshots/ai-result.zip");

        _mediatorMock
            .Setup(x => x.Publish(It.IsAny<INotification>(), It.IsAny<CancellationToken>()))
            .Callback<INotification, CancellationToken>((n, _) => publishedEvents.Add(n))
            .Returns(Task.CompletedTask);

        // Act
        await _handler.Handle(@event, CancellationToken.None);

        // Assert - Started -> Snapshots -> Zip -> Completed
        publishedEvents.Should().HaveCount(4);
        publishedEvents[0].Should().BeOfType<VideoProcessingStartedEvent>();
        publishedEvents[1].Should().BeOfType<VideoSnapshotsGeneratedEvent>();
        publishedEvents[2].Should().BeOfType<VideoZipGeneratedEvent>();
        publishedEvents[3].Should().BeOfType<VideoProcessingCompletedEvent>();
    }

    [Fact(DisplayName = "Should publish completed event with correct zip key in AI path")]
    [Trait("Application", "VideoUploadedEventHandlerWithAI")]
    public async Task Handle_WithAIPath_ShouldPublishCompletedEventWithCorrectZipKey()
    {
        // Arrange
        var expectedZipKey = "snapshots/ai-specific-key.zip";
        var @event = new VideoUploadedEvent(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "videos/test.mp4",
            DateTime.UtcNow,
            new VideoUploadedMetadata(1920, 1080, 5, 10.5, "Find moments"));

        _videoStorageMock
            .Setup(x => x.DownloadVideoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream());

        _generativeClientMock
            .Setup(x => x.ExtractKeyMomentsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<(double, string)> { (5.0, "Moment") }.AsReadOnly());

        _videoProcessingServiceMock
            .Setup(x => x.ExtractSnapshotsAtTimestampsAsync(
                It.IsAny<Stream>(),
                It.IsAny<IReadOnlyList<double>>(),
                It.IsAny<int?>(),
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<(Stream, string)> { (new MemoryStream(), "snapshot_001.jpg") }.AsReadOnly());

        _videoStorageMock
            .Setup(x => x.UploadSnapshotsAsZipAsync(
                It.IsAny<List<(Stream, string)>>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedZipKey);

        _mediatorMock
            .Setup(x => x.Publish(It.IsAny<INotification>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _handler.Handle(@event, CancellationToken.None);

        // Assert
        _mediatorMock.Verify(
            x => x.Publish(
                It.Is<VideoProcessingCompletedEvent>(e => e.ZipKey == expectedZipKey),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact(DisplayName = "Should pass timestamps ordered to extraction service")]
    [Trait("Application", "VideoUploadedEventHandlerWithAI")]
    public async Task Handle_WithAIPrompt_ShouldPassTimestampsOrderedAscending()
    {
        // Arrange
        var @event = new VideoUploadedEvent(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "videos/test.mp4",
            DateTime.UtcNow,
            new VideoUploadedMetadata(1920, 1080, 5, 10.5, "Find moments"));

        _videoStorageMock
            .Setup(x => x.DownloadVideoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream());

        // Return moments out of order – handler should sort them
        var moments = new List<(double, string)>
        {
            (30.0, "Late moment"),
            (5.0, "Early moment"),
            (15.0, "Middle moment")
        };
        _generativeClientMock
            .Setup(x => x.ExtractKeyMomentsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(moments.AsReadOnly());

        _videoProcessingServiceMock
            .Setup(x => x.ExtractSnapshotsAtTimestampsAsync(
                It.IsAny<Stream>(),
                It.IsAny<IReadOnlyList<double>>(),
                It.IsAny<int?>(),
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<(Stream, string)>
            {
                (new MemoryStream(), "s1.jpg"),
                (new MemoryStream(), "s2.jpg"),
                (new MemoryStream(), "s3.jpg")
            }.AsReadOnly());

        _videoStorageMock
            .Setup(x => x.UploadSnapshotsAsZipAsync(
                It.IsAny<List<(Stream, string)>>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("snapshots/result.zip");

        _mediatorMock
            .Setup(x => x.Publish(It.IsAny<INotification>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _handler.Handle(@event, CancellationToken.None);

        // Assert - timestamps passed to extraction service must be sorted ascending
        _videoProcessingServiceMock.Verify(
            x => x.ExtractSnapshotsAtTimestampsAsync(
                It.IsAny<Stream>(),
                It.Is<IReadOnlyList<double>>(list =>
                    list.Count == 3 &&
                    list[0] == 5.0 &&
                    list[1] == 15.0 &&
                    list[2] == 30.0),
                It.IsAny<int?>(),
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact(DisplayName = "Should extract and rename snapshots correctly with AI moments")]
    [Trait("Application", "VideoUploadedEventHandlerWithAI")]
    public async Task Handle_ShouldRenameSnapshotsSequentially()
    {
        // Arrange
        var videoId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var @event = new VideoUploadedEvent(
            videoId,
            userId,
            "videos/test.mp4",
            DateTime.UtcNow,
            new VideoUploadedMetadata(1920, 1080, 5, 10.5, "AI prompt"));

        var mockStream = new MemoryStream();
        _videoStorageMock
            .Setup(x => x.DownloadVideoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockStream);

        var moments = new List<(double, string)>
        {
            (5.0, "Moment 1"),
            (10.0, "Moment 2")
        };

        _generativeClientMock
            .Setup(x => x.ExtractKeyMomentsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(moments.AsReadOnly());

        var snapshots = new List<(Stream, string)>
        {
            (new MemoryStream(), "original_001.jpg"),
            (new MemoryStream(), "original_002.jpg")
        };

        _videoProcessingServiceMock
            .Setup(x => x.ExtractSnapshotsAtTimestampsAsync(
                It.IsAny<Stream>(),
                It.IsAny<IReadOnlyList<double>>(),
                It.IsAny<int?>(),
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshots.AsReadOnly());

        _videoStorageMock
            .Setup(x => x.UploadSnapshotsAsZipAsync(
                It.IsAny<List<(Stream, string)>>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("snapshots/test.zip");

        _mediatorMock
            .Setup(x => x.Publish(It.IsAny<INotification>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _handler.Handle(@event, CancellationToken.None);

        // Assert
        _videoStorageMock.Verify(
            x => x.UploadSnapshotsAsZipAsync(
                It.Is<List<(Stream, string)>>(list =>
                    list.Count == 2 &&
                    list[0].Item2 == "snapshot_001.jpg" &&
                    list[1].Item2 == "snapshot_002.jpg"),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
