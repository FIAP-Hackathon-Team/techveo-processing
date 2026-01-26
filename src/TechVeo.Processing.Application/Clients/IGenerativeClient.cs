using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace TechVeo.Processing.Application.Clients;

public interface IGenerativeClient
{
    /// <summary>
    /// Sends a prompt to the generative model and returns a list of timestamps (seconds) and short descriptions.
    /// The returned tuples contain the second (double) and a short summary (string) for that second.
    /// </summary>
    Task<IReadOnlyList<(double Second, string Summary)>> ExtractKeyMomentsAsync(string videoKey, string prompt, CancellationToken cancellationToken = default);
}
