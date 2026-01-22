using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using TechVeo.Processing.Application.Events.Integration.Outgoing;
using TechVeo.Processing.Application.Services;
using TechVeo.Shared.Application.Storage;

namespace TechVeo.Processing.Application.Events.Integration.Incoming.Handlers;

internal class VideoUploadedEventHandler(
    IMediator mediator,
    IVideoStorage videoStorage,
    IVideoProcessingService videoProcessingService,
    ILogger<VideoUploadedEventHandler> logger) : INotificationHandler<VideoUploadedEvent>
{
    public async Task Handle(VideoUploadedEvent @event, CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Starting video processing for VideoId: {VideoId}", @event.VideoId);

            await mediator.Publish(new VideoProcessingStarted(
                @event.VideoId,
                DateTime.UtcNow), cancellationToken);

            Stream? videoStream = null;

            try
            {
                var videoKey = @event.VideoKey;

                logger.LogInformation("Downloading video from S3 for VideoId: {VideoId}, Key: {VideoKey}", @event.VideoId, videoKey);
                videoStream = await videoStorage.DownloadVideoAsync(videoKey, cancellationToken);

                var snapshotCount = @event.Metadata.SnapshotCount;
                var intervalSeconds = @event.Metadata.IntervalSeconds;

                logger.LogInformation("Extracting snapshots for VideoId: {VideoId} (SnapshotCount={SnapshotCount}, IntervalSeconds={IntervalSeconds})", snapshotCount, intervalSeconds, @event.VideoId);

                var snapshots = await videoProcessingService.ExtractSnapshotsAsync(
                    videoStream,
                    snapshotCount,
                    intervalSeconds,
                    cancellationToken);

                await mediator.Publish(new VideoSnapshotsGenerated(
                    @event.VideoId,
                    DateTime.UtcNow), cancellationToken);

                var zipId = Guid.NewGuid();
                var zipFileName = $"snapshots_{@event.VideoId}_{zipId}.zip";

                logger.LogInformation("Uploading zip file to S3 for VideoId: {VideoId}, ZipId: {ZipId}", @event.VideoId, zipId);
                var zipUrl = await videoStorage.UploadSnapshotsAsZipAsync(snapshots, zipFileName, cancellationToken);

                await mediator.Publish(new VideoZipGenerated(
                    @event.VideoId,
                    zipId,
                    zipUrl), cancellationToken);

                await mediator.Publish(new VideoProcessingCompletedEvent(
                    @event.VideoId,
                    DateTime.UtcNow), cancellationToken);

                logger.LogInformation("Video processing completed successfully for VideoId: {VideoId}", @event.VideoId);

                foreach (var (stream, _) in snapshots)
                {
                    await stream.DisposeAsync();
                }
            }
            finally
            {
                if (videoStream != null)
                {
                    await videoStream.DisposeAsync();
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Video processing failed for VideoId: {VideoId}", @event.VideoId);

            await mediator.Publish(new VideoProcessingFailedEvent(
                @event.VideoId,
                DateTime.UtcNow), cancellationToken);

            throw;
        }
    }
}
