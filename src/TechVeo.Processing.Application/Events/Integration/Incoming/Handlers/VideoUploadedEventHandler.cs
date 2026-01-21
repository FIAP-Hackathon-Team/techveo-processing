using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace TechVeo.Processing.Application.Events.Integration.Incoming.Handlers;

internal class VideoUploadedEventHandler : INotificationHandler<VideoUploadedEvent>
{
    public async Task Handle(VideoUploadedEvent @event, CancellationToken cancellationToken)
    {

    }
}
