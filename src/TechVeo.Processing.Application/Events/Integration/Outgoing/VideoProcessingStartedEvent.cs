using System;
using TechVeo.Shared.Application.Events;

namespace TechVeo.Processing.Application.Events.Integration.Outgoing
{
    public record VideoProcessingStartedEvent(
        Guid VideoId,
        DateTime StartedAt
        ) : IIntegrationEvent;
}
