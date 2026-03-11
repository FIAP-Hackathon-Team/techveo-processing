using System;
using FluentAssertions;
using TechVeo.Processing.Application.Events.Integration.Incoming;
using TechVeo.Processing.Application.Events.Integration.Outgoing;
using Xunit;

namespace TechVeo.Processing.Application.Tests.Events;

public class VideoUploadedEventTests
{
    [Fact(DisplayName = "Should create VideoUploadedEvent with all properties")]
    [Trait("Application", "VideoUploadedEvent")]
    public void Constructor_WithValidProperties_ShouldCreateEvent()
    {
        // Arrange
        var videoId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var videoKey = "videos/test-video.mp4";
        var uploadedAt = DateTime.UtcNow;
        var metadata = new VideoUploadedMetadata(1920, 1080, 5, 10.5, "AI prompt");

        // Act
        var @event = new VideoUploadedEvent(videoId, userId, videoKey, uploadedAt, metadata);

        // Assert
        @event.VideoId.Should().Be(videoId);
        @event.UserId.Should().Be(userId);
        @event.VideoKey.Should().Be(videoKey);
        @event.UploadedAt.Should().Be(uploadedAt);
        @event.Metadata.Should().NotBeNull();
        @event.Metadata.Width.Should().Be(1920);
        @event.Metadata.Height.Should().Be(1080);
        @event.Metadata.SnapshotCount.Should().Be(5);
        @event.Metadata.IntervalSeconds.Should().Be(10.5);
        @event.Metadata.AiPrompt.Should().Be("AI prompt");
    }

    [Fact(DisplayName = "Should create VideoUploadedMetadata without optional fields")]
    [Trait("Application", "VideoUploadedEvent")]
    public void MetadataConstructor_WithoutOptionalFields_ShouldCreateMetadata()
    {
        // Arrange
        var width = 1280;
        var height = 720;

        // Act
        var metadata = new VideoUploadedMetadata(width, height, null, null, null);

        // Assert
        metadata.Width.Should().Be(width);
        metadata.Height.Should().Be(height);
        metadata.SnapshotCount.Should().BeNull();
        metadata.IntervalSeconds.Should().BeNull();
        metadata.AiPrompt.Should().BeNull();
    }
}

public class VideoProcessingStartedEventTests
{
    [Fact(DisplayName = "Should create VideoProcessingStartedEvent with valid properties")]
    [Trait("Application", "VideoProcessingStartedEvent")]
    public void Constructor_WithValidProperties_ShouldCreateEvent()
    {
        // Arrange
        var videoId = Guid.NewGuid();
        var startedAt = DateTime.UtcNow;

        // Act
        var @event = new VideoProcessingStartedEvent(videoId, startedAt);

        // Assert
        @event.VideoId.Should().Be(videoId);
        @event.StartedAt.Should().Be(startedAt);
    }

    [Fact(DisplayName = "Should handle minimum DateTime")]
    [Trait("Application", "VideoProcessingStartedEvent")]
    public void Constructor_WithMinDateTime_ShouldCreateEvent()
    {
        // Arrange
        var videoId = Guid.NewGuid();
        var startedAt = DateTime.MinValue;

        // Act
        var @event = new VideoProcessingStartedEvent(videoId, startedAt);

        // Assert
        @event.StartedAt.Should().Be(DateTime.MinValue);
    }

    [Fact(DisplayName = "Should handle maximum DateTime")]
    [Trait("Application", "VideoProcessingStartedEvent")]
    public void Constructor_WithMaxDateTime_ShouldCreateEvent()
    {
        // Arrange
        var videoId = Guid.NewGuid();
        var startedAt = DateTime.MaxValue;

        // Act
        var @event = new VideoProcessingStartedEvent(videoId, startedAt);

        // Assert
        @event.StartedAt.Should().Be(DateTime.MaxValue);
    }
}

public class VideoSnapshotsGeneratedEventTests
{
    [Fact(DisplayName = "Should create VideoSnapshotsGeneratedEvent with valid properties")]
    [Trait("Application", "VideoSnapshotsGeneratedEvent")]
    public void Constructor_WithValidProperties_ShouldCreateEvent()
    {
        // Arrange
        var videoId = Guid.NewGuid();
        var generatedAt = DateTime.UtcNow;

        // Act
        var @event = new VideoSnapshotsGeneratedEvent(videoId, generatedAt);

        // Assert
        @event.VideoId.Should().Be(videoId);
        @event.GeneratedAt.Should().Be(generatedAt);
    }

    [Fact(DisplayName = "Should maintain correct timestamp")]
    [Trait("Application", "VideoSnapshotsGeneratedEvent")]
    public void Constructor_ShouldPreserveTimestamp()
    {
        // Arrange
        var videoId = Guid.NewGuid();
        var timestamp = new DateTime(2024, 1, 1, 12, 30, 45, DateTimeKind.Utc);

        // Act
        var @event = new VideoSnapshotsGeneratedEvent(videoId, timestamp);

        // Assert
        @event.GeneratedAt.Year.Should().Be(2024);
        @event.GeneratedAt.Month.Should().Be(1);
        @event.GeneratedAt.Day.Should().Be(1);
        @event.GeneratedAt.Hour.Should().Be(12);
        @event.GeneratedAt.Minute.Should().Be(30);
        @event.GeneratedAt.Second.Should().Be(45);
    }
}

public class VideoZipGeneratedEventTests
{
    [Fact(DisplayName = "Should create VideoZipGeneratedEvent with valid ZipKey")]
    [Trait("Application", "VideoZipGeneratedEvent")]
    public void Constructor_WithValidProperties_ShouldCreateEvent()
    {
        // Arrange
        var videoId = Guid.NewGuid();
        var zipKey = "snapshots/zip-123.zip";

        // Act
        var @event = new VideoZipGeneratedEvent(videoId, zipKey);

        // Assert
        @event.VideoId.Should().Be(videoId);
        @event.ZipKey.Should().Be(zipKey);
    }

    [Fact(DisplayName = "Should handle empty ZipKey")]
    [Trait("Application", "VideoZipGeneratedEvent")]
    public void Constructor_WithEmptyZipKey_ShouldCreateEvent()
    {
        // Arrange
        var videoId = Guid.NewGuid();

        // Act
        var @event = new VideoZipGeneratedEvent(videoId, "");

        // Assert
        @event.ZipKey.Should().Be("");
    }

    [Fact(DisplayName = "Should have unique VideoIds for different events")]
    [Trait("Application", "VideoZipGeneratedEvent")]
    public void Constructor_MultipleEvents_ShouldHaveUniqueVideoIds()
    {
        // Arrange
        var videoId1 = Guid.NewGuid();
        var videoId2 = Guid.NewGuid();
        var zipKey = "snapshots/zip-123.zip";

        // Act
        var event1 = new VideoZipGeneratedEvent(videoId1, zipKey);
        var event2 = new VideoZipGeneratedEvent(videoId2, zipKey);

        // Assert
        event1.VideoId.Should().NotBe(event2.VideoId);
        event1.ZipKey.Should().Be(event2.ZipKey);
    }
}

public class VideoProcessingCompletedEventTests
{
    [Fact(DisplayName = "Should create VideoProcessingCompletedEvent with valid properties")]
    [Trait("Application", "VideoProcessingCompletedEvent")]
    public void Constructor_WithValidProperties_ShouldCreateEvent()
    {
        // Arrange
        var videoId = Guid.NewGuid();
        var completedAt = DateTime.UtcNow;
        var zipKey = "snapshots/completed-zip.zip";

        // Act
        var @event = new VideoProcessingCompletedEvent(videoId, completedAt, zipKey);

        // Assert
        @event.VideoId.Should().Be(videoId);
        @event.CompletedAt.Should().Be(completedAt);
        @event.ZipKey.Should().Be(zipKey);
    }

    [Fact(DisplayName = "Should preserve timestamp with millisecond precision")]
    [Trait("Application", "VideoProcessingCompletedEvent")]
    public void Constructor_ShouldPreservePrecision()
    {
        // Arrange
        var videoId = Guid.NewGuid();
        var completedAt = new DateTime(2024, 1, 1, 12, 30, 45, 123, DateTimeKind.Utc);
        var zipKey = "snapshots/zip.zip";

        // Act
        var @event = new VideoProcessingCompletedEvent(videoId, completedAt, zipKey);

        // Assert
        @event.CompletedAt.Millisecond.Should().Be(123);
    }
}

public class VideoProcessingFailedEventTests
{
    [Fact(DisplayName = "Should create VideoProcessingFailedEvent with valid properties")]
    [Trait("Application", "VideoProcessingFailedEvent")]
    public void Constructor_WithValidProperties_ShouldCreateEvent()
    {
        // Arrange
        var videoId = Guid.NewGuid();
        var failedAt = DateTime.UtcNow;

        // Act
        var @event = new VideoProcessingFailedEvent(videoId, failedAt);

        // Assert
        @event.VideoId.Should().Be(videoId);
        @event.FailedAt.Should().Be(failedAt);
    }

    [Fact(DisplayName = "Should handle different failure times")]
    [Trait("Application", "VideoProcessingFailedEvent")]
    public void Constructor_WithDifferentTimes_ShouldCreateEvents()
    {
        // Arrange
        var videoId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        // Act
        var event1 = new VideoProcessingFailedEvent(videoId, now);
        var event2 = new VideoProcessingFailedEvent(videoId, now.AddSeconds(10));

        // Assert
        event1.FailedAt.Should().BeBefore(event2.FailedAt);
    }
}

public class EventEqualityTests
{
    [Fact(DisplayName = "Events should be records with value-based equality")]
    [Trait("Application", "Events")]
    public void Events_ShouldEqualIfPropertiesMatch()
    {
        // Arrange
        var videoId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        // Act
        var event1 = new VideoProcessingStartedEvent(videoId, now);
        var event2 = new VideoProcessingStartedEvent(videoId, now);

        // Assert
        event1.Should().Be(event2);
    }

    [Fact(DisplayName = "Different event values should not be equal")]
    [Trait("Application", "Events")]
    public void Events_WithDifferentValues_ShouldNotBeEqual()
    {
        // Arrange
        var videoId1 = Guid.NewGuid();
        var videoId2 = Guid.NewGuid();
        var now = DateTime.UtcNow;

        // Act
        var event1 = new VideoProcessingStartedEvent(videoId1, now);
        var event2 = new VideoProcessingStartedEvent(videoId2, now);

        // Assert
        event1.Should().NotBe(event2);
    }
}
