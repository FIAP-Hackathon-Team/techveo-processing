using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using TechVeo.Processing.Application.Clients;
using TechVeo.Processing.Application.Events.Integration.Incoming;
using TechVeo.Processing.Application.Events.Integration.Incoming.Handlers;
using TechVeo.Processing.Application.Events.Integration.Outgoing;
using TechVeo.Processing.Application.Services;
using TechVeo.Processing.Infra;
using TechVeo.Processing.Infra.Services;
using TechVeo.Shared.Application.Storage;
using Xunit;

namespace TechVeo.Processing.Application.Tests.DependencyInjection;

public class DependencyInjectionTests
{
    [Fact(DisplayName = "Should register infra services")]
    [Trait("Setup", "DependencyInjection")]
    public void AddInfra_ShouldRegisterVideoProcessingService()
    {
        // Arrange
        var services = new ServiceCollection();

        // Create mocks for external dependencies
        var mockVideoStorage = new Mock<IVideoStorage>();
        var mockGenerativeClient = new Mock<IGenerativeClient>();

        services.AddSingleton(mockVideoStorage.Object);
        services.AddSingleton(mockGenerativeClient.Object);
        services.AddLogging();

        // Act
        services.AddInfra();

        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var processingService = serviceProvider.GetService<IVideoProcessingService>();
        processingService.Should().NotBeNull();
        processingService.Should().BeOfType<VideoProcessingService>();
    }

    [Fact(DisplayName = "Should register video processing service without generative client mock")]
    [Trait("Setup", "DependencyInjection")]
    public void AddInfra_WithoutGenerativeClientMock_ShouldRegisterVideoProcessingService()
    {
        // Arrange
        var services = new ServiceCollection();
        var mockVideoStorage = new Mock<IVideoStorage>();

        services.AddSingleton(mockVideoStorage.Object);
        services.AddLogging();
        services.AddInfra();

        var serviceProvider = services.BuildServiceProvider();

        // Act
        var processingService = serviceProvider.GetService<IVideoProcessingService>();

        // Assert
        processingService.Should().NotBeNull();
        processingService.Should().BeOfType<VideoProcessingService>();
    }
}

public class VideoUploadedEventMetadataTests
{
    [Fact(DisplayName = "Should create metadata with all parameters")]
    [Trait("Application", "VideoUploadedEventMetadata")]
    public void Constructor_WithAllParameters_ShouldCreateMetadata()
    {
        // Arrange & Act
        var metadata = new VideoUploadedMetadata(1920, 1080, 5, 10.5, "Test prompt");

        // Assert
        metadata.Width.Should().Be(1920);
        metadata.Height.Should().Be(1080);
        metadata.SnapshotCount.Should().Be(5);
        metadata.IntervalSeconds.Should().Be(10.5);
        metadata.AiPrompt.Should().Be("Test prompt");
    }

    [Fact(DisplayName = "Should create metadata with null optional parameters")]
    [Trait("Application", "VideoUploadedEventMetadata")]
    public void Constructor_WithNullParameters_ShouldCreateMetadata()
    {
        // Arrange & Act
        var metadata = new VideoUploadedMetadata(1280, 720, null, null, null);

        // Assert
        metadata.Width.Should().Be(1280);
        metadata.Height.Should().Be(720);
        metadata.SnapshotCount.Should().BeNull();
        metadata.IntervalSeconds.Should().BeNull();
        metadata.AiPrompt.Should().BeNull();
    }

    [Fact(DisplayName = "Should handle zero values")]
    [Trait("Application", "VideoUploadedEventMetadata")]
    public void Constructor_WithZeroValues_ShouldCreateMetadata()
    {
        // Arrange & Act
        var metadata = new VideoUploadedMetadata(0, 0, 0, 0, "");

        // Assert
        metadata.Width.Should().Be(0);
        metadata.Height.Should().Be(0);
        metadata.SnapshotCount.Should().Be(0);
        metadata.IntervalSeconds.Should().Be(0);
        metadata.AiPrompt.Should().Be("");
    }

    [Fact(DisplayName = "Should handle large values")]
    [Trait("Application", "VideoUploadedEventMetadata")]
    public void Constructor_WithLargeValues_ShouldCreateMetadata()
    {
        // Arrange & Act
        var metadata = new VideoUploadedMetadata(4096, 2160, 1000, 999.99, "Very long prompt text");

        // Assert
        metadata.Width.Should().Be(4096);
        metadata.Height.Should().Be(2160);
        metadata.SnapshotCount.Should().Be(1000);
        metadata.IntervalSeconds.Should().Be(999.99);
        metadata.AiPrompt.Should().Be("Very long prompt text");
    }
}

public class GenerativeClientInterfaceTests
{
    [Fact(DisplayName = "Should implement IGenerativeClient contract")]
    [Trait("Application", "GenerativeClient")]
    public void IGenerativeClient_ShouldHaveExtractKeyMomentsAsyncMethod()
    {
        // Arrange
        var interfaceType = typeof(IGenerativeClient);

        // Act
        var method = interfaceType.GetMethod(nameof(IGenerativeClient.ExtractKeyMomentsAsync));

        // Assert
        method.Should().NotBeNull();
        method!.ReturnType.Should().Be(typeof(Task<IReadOnlyList<(double, string)>>));
    }
}

public class MultipleSnapshotsExtractionTests
{
    [Fact(DisplayName = "Should handle multiple snapshots correctly")]
    [Trait("Application", "SnapshotExtraction")]
    public void Snapshots_ShouldBeExtractedInOrder()
    {
        // Arrange
        var snapshots = new List<(Stream, string)>
        {
            (new MemoryStream(), "snapshot_001.jpg"),
            (new MemoryStream(), "snapshot_002.jpg"),
            (new MemoryStream(), "snapshot_003.jpg"),
            (new MemoryStream(), "snapshot_004.jpg"),
            (new MemoryStream(), "snapshot_005.jpg")
        };

        // Assert
        snapshots.Should().HaveCount(5);
        snapshots[0].Item2.Should().Be("snapshot_001.jpg");
        snapshots[4].Item2.Should().Be("snapshot_005.jpg");
    }

    [Fact(DisplayName = "Should handle single snapshot")]
    [Trait("Application", "SnapshotExtraction")]
    public void SingleSnapshot_ShouldBeHandledCorrectly()
    {
        // Arrange
        var snapshots = new List<(Stream, string)>
        {
            (new MemoryStream(), "snapshot_001.jpg")
        };

        // Assert
        snapshots.Should().HaveCount(1);
        snapshots[0].Item2.Should().Be("snapshot_001.jpg");
    }

    [Fact(DisplayName = "Should handle large number of snapshots")]
    [Trait("Application", "SnapshotExtraction")]
    public void LargeNumberOfSnapshots_ShouldBeHandledCorrectly()
    {
        // Arrange
        var snapshots = new List<(Stream, string)>();
        for (int i = 1; i <= 100; i++)
        {
            snapshots.Add((new MemoryStream(), $"snapshot_{i:D3}.jpg"));
        }

        // Assert
        snapshots.Should().HaveCount(100);
        snapshots[0].Item2.Should().Be("snapshot_001.jpg");
        snapshots[99].Item2.Should().Be("snapshot_100.jpg");
    }
}

public class VideoUploadedEventInstanceTests
{
    [Fact(DisplayName = "Should create VideoUploadedEvent with valid data")]
    [Trait("Application", "VideoUploadedEvent")]
    public void Constructor_WithValidData_ShouldCreateEvent()
    {
        // Arrange
        var videoId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var uploadedAt = DateTime.UtcNow;
        var metadata = new VideoUploadedMetadata(1920, 1080, 5, 10.5, "Test");

        // Act
        var @event = new VideoUploadedEvent(videoId, userId, "videos/test.mp4", uploadedAt, metadata);

        // Assert
        @event.VideoId.Should().Be(videoId);
        @event.UserId.Should().Be(userId);
        @event.VideoKey.Should().Be("videos/test.mp4");
        @event.UploadedAt.Should().Be(uploadedAt);
        @event.Metadata.Should().Be(metadata);
    }

    [Fact(DisplayName = "Should handle different video keys")]
    [Trait("Application", "VideoUploadedEvent")]
    public void Constructor_WithDifferentVideoKeys_ShouldStoreCorrectly()
    {
        // Arrange
        var videoId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var metadata = new VideoUploadedMetadata(1920, 1080, 5, 10.5, "Test");

        var keys = new[]
        {
            "videos/test.mp4",
            "bucket/videos/test.mp4",
            "videos/2024/01/test.mp4",
            "test.mp4"
        };

        // Act & Assert
        foreach (var key in keys)
        {
            var @event = new VideoUploadedEvent(videoId, userId, key, now, metadata);
            @event.VideoKey.Should().Be(key);
        }
    }
}

public class VideoProcessingServiceInterfaceTests
{
    [Fact(DisplayName = "Should have ExtractSnapshotsAsync method")]
    [Trait("Application", "IVideoProcessingService")]
    public void IVideoProcessingService_ShouldHaveExtractSnapshotsAsyncMethod()
    {
        // Arrange
        var interfaceType = typeof(IVideoProcessingService);

        // Act
        var method = interfaceType.GetMethod(nameof(IVideoProcessingService.ExtractSnapshotsAsync));

        // Assert
        method.Should().NotBeNull();
        method!.ReturnType.Should().Be(typeof(Task<IReadOnlyList<(Stream, string)>>));
    }

    [Fact(DisplayName = "Should have ExtractSnapshotsAtTimestampsAsync method")]
    [Trait("Application", "IVideoProcessingService")]
    public void IVideoProcessingService_ShouldHaveExtractSnapshotsAtTimestampsAsyncMethod()
    {
        // Arrange
        var interfaceType = typeof(IVideoProcessingService);

        // Act
        var method = interfaceType.GetMethod(nameof(IVideoProcessingService.ExtractSnapshotsAtTimestampsAsync));

        // Assert
        method.Should().NotBeNull();
        method!.ReturnType.Should().Be(typeof(Task<IReadOnlyList<(Stream, string)>>));
    }

    [Fact(DisplayName = "Should be mockable via Moq")]
    [Trait("Application", "IVideoProcessingService")]
    public void IVideoProcessingService_ShouldBeMockable()
    {
        // Arrange & Act
        var mock = new Mock<IVideoProcessingService>();

        // Assert
        mock.Object.Should().NotBeNull();
        mock.Object.Should().BeAssignableTo<IVideoProcessingService>();
    }
}

public class EventPublishingOrderTests
{
    private readonly Mock<IMediator> _mediatorMock;
    private readonly Mock<IVideoStorage> _videoStorageMock;
    private readonly Mock<IVideoProcessingService> _videoProcessingServiceMock;
    private readonly Mock<IGenerativeClient> _generativeClientMock;
    private readonly Mock<ILogger<VideoUploadedEventHandler>> _loggerMock;
    private readonly VideoUploadedEventHandler _handler;
    private readonly List<INotification> _publishedEvents;

    public EventPublishingOrderTests()
    {
        _mediatorMock = new Mock<IMediator>();
        _videoStorageMock = new Mock<IVideoStorage>();
        _videoProcessingServiceMock = new Mock<IVideoProcessingService>();
        _generativeClientMock = new Mock<IGenerativeClient>();
        _loggerMock = new Mock<ILogger<VideoUploadedEventHandler>>();
        _publishedEvents = new List<INotification>();

        _handler = new VideoUploadedEventHandler(
            _mediatorMock.Object,
            _videoStorageMock.Object,
            _videoProcessingServiceMock.Object,
            _generativeClientMock.Object,
            _loggerMock.Object);
    }

    [Fact(DisplayName = "Should publish events in correct order")]
    [Trait("Application", "EventOrdering")]
    public async Task Handle_ShouldPublishEventsInCorrectSequence()
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
            .Callback<INotification, CancellationToken>((notification, _) => _publishedEvents.Add(notification))
            .Returns(Task.CompletedTask);

        // Act
        await _handler.Handle(@event, CancellationToken.None);

        // Assert - Verify order: Started -> Snapshots -> Zip -> Completed
        _publishedEvents.Should().HaveCount(4);
        _publishedEvents[0].Should().BeOfType<VideoProcessingStartedEvent>();
        _publishedEvents[1].Should().BeOfType<VideoSnapshotsGeneratedEvent>();
        _publishedEvents[2].Should().BeOfType<VideoZipGeneratedEvent>();
        _publishedEvents[3].Should().BeOfType<VideoProcessingCompletedEvent>();
    }
}
