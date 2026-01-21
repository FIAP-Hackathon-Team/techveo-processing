using System;
using TechVeo.Shared.Application.Events;

namespace TechVeo.Processing.Application.Events.Integration.Outgoing
{
    public record VideoZipGenerated(
        Guid VideoId,
        Guid ZipId,
        string ZipUrl
        ) : IIntegrationEvent;
}
