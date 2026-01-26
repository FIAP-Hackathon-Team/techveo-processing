using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using TechVeo.Processing.Application.Clients;
using TechVeo.Processing.Application.Events.Integration.Outgoing;
using TechVeo.Processing.Application.Services;
using TechVeo.Shared.Application.Storage;

namespace TechVeo.Processing.Application.Events.Integration.Incoming.Handlers;

internal class VideoUploadedEventHandler(
    IMediator mediator,
    IVideoStorage videoStorage,
    IVideoProcessingService videoProcessingService,
    IGenerativeClient generativeClient,
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
                var width = @event.Metadata.Width;
                var height = @event.Metadata.Height;
                var aiPrompt = @event.Metadata.AiPrompt;

                // If an AI prompt is provided, call the generative client to extract seconds and summaries.
                if (!string.IsNullOrWhiteSpace(aiPrompt))
                {
                    logger.LogInformation("AI prompt provided for VideoId: {VideoId}, calling generative service", @event.VideoId);

                    // Build an instruction to the model to return seconds and a short summary, only the seconds are used for extraction.
                    var combinedPrompt = $"Você é um assistente que retorna os melhores momentos em segundos e um resumo curto. Recebi o seguinte prompt: {aiPrompt}. Retorne apenas um JSON array com objetos contendo 'second' (número em segundos) e 'summary' (texto curto). Ex: [{'{'}\"second\": 12.5, \"summary\": \"cena X\"{'}'}]";

                    var moments = await generativeClient.ExtractKeyMomentsAsync(videoKey, combinedPrompt, cancellationToken);

                    if (moments != null && moments.Count > 0)
                    {
                        // Prepare snapshots by extracting at exact seconds returned by AI.
                        var snapshots = new System.Collections.Generic.List<(Stream Stream, string FileName)>();

                        var timestamps = moments.OrderBy(m => m.Second).Select(m => m.Second).ToList();

                        var extractedSnapshots = await videoProcessingService.ExtractSnapshotsAtTimestampsAsync(
                            videoStream,
                            timestamps,
                            width,
                            height,
                            cancellationToken);

                        var idx = 1;
                        foreach (var (stream, _) in extractedSnapshots)
                        {
                            var fileName = $"snapshot_{idx:D3}.jpg";
                            snapshots.Add((stream, fileName));
                            idx++;
                        }

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

                        return;
                    }
                    else
                    {
                        logger.LogWarning("No moments returned by generative service for VideoId: {VideoId}", @event.VideoId);
                    }
                }

                logger.LogInformation("Extracting snapshots for VideoId: {VideoId} (SnapshotCount={SnapshotCount}, IntervalSeconds={IntervalSeconds})", snapshotCount, intervalSeconds, @event.VideoId);

                var defaultSnapshots = await videoProcessingService.ExtractSnapshotsAsync(
                    videoStream,
                    snapshotCount,
                    intervalSeconds,
                    width,
                    height,
                    cancellationToken);

                await mediator.Publish(new VideoSnapshotsGenerated(
                    @event.VideoId,
                    DateTime.UtcNow), cancellationToken);

                var defaultZipId = Guid.NewGuid();
                var defaultZipFileName = $"snapshots_{@event.VideoId}_{defaultZipId}.zip";

                logger.LogInformation("Uploading zip file to S3 for VideoId: {VideoId}, ZipId: {ZipId}", @event.VideoId, defaultZipId);
                var defaultZipUrl = await videoStorage.UploadSnapshotsAsZipAsync(defaultSnapshots, defaultZipFileName, cancellationToken);

                await mediator.Publish(new VideoZipGenerated(
                    @event.VideoId,
                    defaultZipId,
                    defaultZipUrl), cancellationToken);

                await mediator.Publish(new VideoProcessingCompletedEvent(
                    @event.VideoId,
                    DateTime.UtcNow), cancellationToken);

                logger.LogInformation("Video processing completed successfully for VideoId: {VideoId}", @event.VideoId);

                foreach (var (stream, _) in defaultSnapshots)
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
