using System;
using TechVeo.Shared.Application.Events;

namespace TechVeo.Processing.Application.Events.Integration.Outgoing
{
    public record VideoProcessingCompletedEvent(
        Guid VideoId,
        DateTime CompletedAt,
        string Url)
        : IIntegrationEvent;
}
