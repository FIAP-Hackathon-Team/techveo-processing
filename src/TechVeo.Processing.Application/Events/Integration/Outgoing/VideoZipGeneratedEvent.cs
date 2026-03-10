using System;
using TechVeo.Shared.Application.Events;

namespace TechVeo.Processing.Application.Events.Integration.Outgoing
{
    public record VideoZipGeneratedEvent(
        Guid VideoId,
        string ZipKey
        ) : IIntegrationEvent;
}
