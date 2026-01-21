using System;
using TechVeo.Shared.Application.Events;

namespace TechVeo.Processing.Application.Events.Integration.Incoming
{
    public record VideoUploadedEvent(
        Guid VideoId,
        Guid UserId,
        string VideoUrl,
        DateTime UploadedAt,
        VideoUploadedMetadata Metadata
        ) : IIntegrationEvent;

    public record VideoUploadedMetadata(
        int Width,
        int Height,
        int? SnapshotCount,
        double? IntervalSeconds);
}
