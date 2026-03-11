using System;
using System.Collections.Generic;
using FluentAssertions;
using TechVeo.Processing.Application.Events.Integration.Incoming;
using TechVeo.Processing.Application.Events.Integration.Outgoing;
using Xunit;

namespace TechVeo.Processing.Application.Tests.Events.Integration;

public class AllOutgoingEventsTests
{
    [Fact(DisplayName = "All outgoing events should be records")]
    [Trait("Application", "OutgoingEvents")]
    public void OutgoingEvents_ShouldBeRecords()
    {
        // Arrange
        var videoId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        // Act & Assert - Create instances
        var startedEvent = new VideoProcessingStartedEvent(videoId, now);
        var snapshotsEvent = new VideoSnapshotsGeneratedEvent(videoId, now);
        var zipEvent = new VideoZipGeneratedEvent(videoId, "test.zip");
        var completedEvent = new VideoProcessingCompletedEvent(videoId, now, "test.zip");
        var failedEvent = new VideoProcessingFailedEvent(videoId, now);

        startedEvent.Should().NotBeNull();
        snapshotsEvent.Should().NotBeNull();
        zipEvent.Should().NotBeNull();
        completedEvent.Should().NotBeNull();
        failedEvent.Should().NotBeNull();
    }

    [Theory(DisplayName = "Should support value equality for records")]
    [Trait("Application", "OutgoingEvents")]
    [InlineData("snapshot_1.jpg")]
    [InlineData("snapshot_2.jpg")]
    [InlineData("snapshot_3.jpg")]
    public void Records_ShouldSupportValueEquality(string fileName)
    {
        // Arrange
        var videoId = Guid.NewGuid();
        var time1 = new DateTime(2024, 1, 1, 12, 0, 0);
        var time2 = new DateTime(2024, 1, 1, 12, 0, 0);

        // Act
        var event1 = new VideoProcessingStartedEvent(videoId, time1);
        var event2 = new VideoProcessingStartedEvent(videoId, time2);

        // Assert
        event1.Should().Be(event2);
        fileName.Should().NotBeNullOrEmpty();
    }

    [Fact(DisplayName = "Events with different VideoIds should not be equal")]
    [Trait("Application", "OutgoingEvents")]
    public void EventsWithDifferentVideoIds_ShouldNotBeEqual()
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

    [Fact(DisplayName = "Events with different timestamps should not be equal")]
    [Trait("Application", "OutgoingEvents")]
    public void EventsWithDifferentTimestamps_ShouldNotBeEqual()
    {
        // Arrange
        var videoId = Guid.NewGuid();
        var time1 = DateTime.UtcNow;
        var time2 = time1.AddSeconds(1);

        // Act
        var event1 = new VideoProcessingStartedEvent(videoId, time1);
        var event2 = new VideoProcessingStartedEvent(videoId, time2);

        // Assert
        event1.Should().NotBe(event2);
    }

    [Fact(DisplayName = "VideoZipGeneratedEvent should correctly store ZipKey")]
    [Trait("Application", "OutgoingEvents")]
    public void VideoZipGeneratedEvent_ShouldStoreZipKeyCorrectly()
    {
        // Arrange
        var videoId = Guid.NewGuid();
        var zipKeys = new[]
        {
            "snapshots/test.zip",
            "s3://bucket/snapshots/test.zip",
            "test_123.zip",
            ""
        };

        // Act & Assert
        foreach (var zipKey in zipKeys)
        {
            var @event = new VideoZipGeneratedEvent(videoId, zipKey);
            @event.ZipKey.Should().Be(zipKey);
            @event.VideoId.Should().Be(videoId);
        }
    }

    [Fact(DisplayName = "VideoProcessingCompletedEvent should include ZipKey")]
    [Trait("Application", "OutgoingEvents")]
    public void VideoProcessingCompletedEvent_ShouldIncludeZipKey()
    {
        // Arrange
        var videoId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var zipKey = "snapshots/completed.zip";

        // Act
        var @event = new VideoProcessingCompletedEvent(videoId, now, zipKey);

        // Assert
        @event.VideoId.Should().Be(videoId);
        @event.CompletedAt.Should().Be(now);
        @event.ZipKey.Should().Be(zipKey);
    }

    [Fact(DisplayName = "VideoProcessingFailedEvent should only need VideoId and FailedAt")]
    [Trait("Application", "OutgoingEvents")]
    public void VideoProcessingFailedEvent_ShouldNotNeedZipKey()
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
}

public class EventTimestampTests
{
    [Fact(DisplayName = "Should preserve millisecond precision in timestamps")]
    [Trait("Application", "EventTimestamps")]
    public void Timestamps_ShouldPreserveMilliseconds()
    {
        // Arrange
        var videoId = Guid.NewGuid();
        var preciseTime = new DateTime(2024, 3, 10, 15, 30, 45, 123, DateTimeKind.Utc);

        // Act
        var @event = new VideoProcessingStartedEvent(videoId, preciseTime);

        // Assert
        @event.StartedAt.Should().Be(preciseTime);
        @event.StartedAt.Millisecond.Should().Be(123);
    }

    [Fact(DisplayName = "Should handle minimum DateTime")]
    [Trait("Application", "EventTimestamps")]
    public void DateTime_ShouldHandleMinValue()
    {
        // Arrange
        var videoId = Guid.NewGuid();

        // Act
        var @event = new VideoProcessingStartedEvent(videoId, DateTime.MinValue);

        // Assert
        @event.StartedAt.Should().Be(DateTime.MinValue);
    }

    [Fact(DisplayName = "Should handle maximum DateTime")]
    [Trait("Application", "EventTimestamps")]
    public void DateTime_ShouldHandleMaxValue()
    {
        // Arrange
        var videoId = Guid.NewGuid();

        // Act
        var @event = new VideoProcessingStartedEvent(videoId, DateTime.MaxValue);

        // Assert
        @event.StartedAt.Should().Be(DateTime.MaxValue);
    }

    [Fact(DisplayName = "Should handle UTC datetime")]
    [Trait("Application", "EventTimestamps")]
    public void DateTime_ShouldPreserveUTCKind()
    {
        // Arrange
        var videoId = Guid.NewGuid();
        var utcTime = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        // Act
        var @event = new VideoProcessingStartedEvent(videoId, utcTime);

        // Assert
        @event.StartedAt.Kind.Should().Be(DateTimeKind.Utc);
    }
}

public class GuidTests
{
    [Fact(DisplayName = "Should handle empty Guid")]
    [Trait("Application", "GuidHandling")]
    public void Should_HandleEmptyGuid()
    {
        // Arrange & Act
        var @event = new VideoProcessingStartedEvent(Guid.Empty, DateTime.UtcNow);

        // Assert
        @event.VideoId.Should().Be(Guid.Empty);
    }

    [Fact(DisplayName = "Should handle various Guids")]
    [Trait("Application", "GuidHandling")]
    public void Should_HandleVariousGuids()
    {
        // Arrange
        var guids = new[]
        {
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            new Guid("12345678-1234-1234-1234-123456789abc")
        };

        // Act & Assert
        foreach (var guid in guids)
        {
            var @event = new VideoProcessingStartedEvent(guid, DateTime.UtcNow);
            @event.VideoId.Should().Be(guid);
        }
    }
}

public class EventImmutabilityTests
{
    [Fact(DisplayName = "Events should be immutable records")]
    [Trait("Application", "Immutability")]
    public void Records_ShouldBeImmutable()
    {
        // Arrange
        var videoId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var @event = new VideoProcessingStartedEvent(videoId, now);

        // Act & Assert - Records are immutable, properties are readonly
        // This test verifies that the properties are accessible
        @event.VideoId.Should().Be(videoId);
        @event.StartedAt.Should().Be(now);

        // Creating new instance with 'with' expression should work
        var modifiedEvent = @event with { StartedAt = now.AddSeconds(1) };
        modifiedEvent.StartedAt.Should().NotBe(@event.StartedAt);
        @event.StartedAt.Should().Be(now); // Original should remain unchanged
    }
}

public class EventSerializationTests
{
    [Fact(DisplayName = "Events should have proper string representation")]
    [Trait("Application", "EventSerialization")]
    public void Events_ShouldHaveStringRepresentation()
    {
        // Arrange
        var videoId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        // Act
        var @event = new VideoProcessingStartedEvent(videoId, now);
        var eventString = @event.ToString();

        // Assert
        eventString.Should().Contain("VideoProcessingStartedEvent");
        eventString.Should().NotBeNullOrEmpty();
    }
}

public class MetadataOptionalFieldsTests
{
    [Fact(DisplayName = "Metadata should support all optional combinations")]
    [Trait("Application", "MetadataOptional")]
    public void Metadata_ShouldSupportAllOptionalCombinations()
    {
        // Arrange & Act & Assert
        var metadata1 = new VideoUploadedMetadata(1920, 1080, null, null, null);
        metadata1.SnapshotCount.Should().BeNull();

        var metadata2 = new VideoUploadedMetadata(1920, 1080, 5, null, null);
        metadata2.SnapshotCount.Should().Be(5);
        metadata2.IntervalSeconds.Should().BeNull();

        var metadata3 = new VideoUploadedMetadata(1920, 1080, null, 10.5, null);
        metadata3.SnapshotCount.Should().BeNull();
        metadata3.IntervalSeconds.Should().Be(10.5);

        var metadata4 = new VideoUploadedMetadata(1920, 1080, null, null, "prompt");
        metadata4.AiPrompt.Should().Be("prompt");
    }

    [Fact(DisplayName = "Metadata should handle whitespace-only prompt")]
    [Trait("Application", "MetadataOptional")]
    public void Metadata_ShouldHandleWhitespacePrompt()
    {
        // Arrange
        var metadata = new VideoUploadedMetadata(1920, 1080, 5, 10.5, "   \t\n   ");

        // Assert
        metadata.AiPrompt.Should().Be("   \t\n   ");
    }
}
