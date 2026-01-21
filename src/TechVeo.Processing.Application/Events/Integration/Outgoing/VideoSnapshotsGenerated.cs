using System;
using TechVeo.Shared.Application.Events;

namespace TechVeo.Processing.Application.Events.Integration.Outgoing
{
    public record VideoSnapshotsGenerated(
        Guid VideoId,
        DateTime GeneratedAt
        ) : IIntegrationEvent;
}
