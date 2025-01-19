using Microsoft.Extensions.Hosting;

namespace Excalibur.Jobs;

/// <summary>
///     Represents a hosted service that monitors job configurations and manages their execution lifecycle.
/// </summary>
/// <remarks>
///     This interface combines the functionalities of <see cref="IHostedService" /> for background processing and
///     <see cref="IDisposable" /> for resource cleanup.
/// </remarks>
public interface IJobConfigHostedWatcherService : IHostedService, IDisposable
{
}
