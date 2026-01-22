using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace TechVeo.Processing.Application.Services;

public interface IVideoProcessingService
{
    // If 'intervalSeconds' is provided it will be used to extract snapshots at that interval (in seconds).
    // Otherwise 'snapshotCount' (or default) will be used to compute evenly spaced snapshots across the video.
    Task<IReadOnlyList<(Stream Stream, string FileName)>> ExtractSnapshotsAsync(
        Stream videoStream,
        int? snapshotCount = null,
        double? intervalSeconds = null,
        CancellationToken cancellationToken = default);
}
