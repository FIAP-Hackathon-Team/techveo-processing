using System.Threading;
using System.Threading.Tasks;

namespace TechVeo.Processing.Application.Services;

public interface IProcessRunner
{
    Task<ProcessResult> RunAsync(string fileName, string arguments, CancellationToken cancellationToken = default);
}

public record ProcessResult(int ExitCode, string StdOut, string StdErr);
