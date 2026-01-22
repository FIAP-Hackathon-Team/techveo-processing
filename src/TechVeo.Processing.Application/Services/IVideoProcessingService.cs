using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace TechVeo.Processing.Application.Services;

public interface IVideoProcessingService
{
    Task<IReadOnlyList<(Stream Stream, string FileName)>> ExtractSnapshotsAsync(Stream videoStream, int snapshotCount = 5, CancellationToken cancellationToken = default);
}
