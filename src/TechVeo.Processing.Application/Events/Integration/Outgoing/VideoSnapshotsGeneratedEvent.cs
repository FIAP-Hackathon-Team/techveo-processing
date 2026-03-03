using System;
using TechVeo.Shared.Application.Events;

namespace TechVeo.Processing.Application.Events.Integration.Outgoing
{
    public record VideoSnapshotsGeneratedEvent(
        Guid VideoId,
        DateTime GeneratedAt
        ) : IIntegrationEvent;
}
