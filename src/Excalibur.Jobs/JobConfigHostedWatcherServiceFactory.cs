using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Quartz;

namespace Excalibur.Jobs;

/// <summary>
///     Factory for creating instances of <see cref="JobConfigHostedWatcherService{TJob, TConfig}" />.
/// </summary>
/// <typeparam name="TJob"> The type of the job being monitored. </typeparam>
/// <typeparam name="TConfig"> The type of the job configuration. </typeparam>
public class JobConfigHostedWatcherServiceFactory : IJobConfigHostedWatcherServiceFactory
{
	private readonly IServiceProvider _serviceProvider;

	/// <summary>
	///     Initializes a new instance of the <see cref="JobConfigHostedWatcherServiceFactory" /> class.
	/// </summary>
	/// <param name="serviceProvider"> The service provider for resolving dependencies. </param>
	/// <exception cref="ArgumentNullException"> Thrown when <paramref name="serviceProvider" /> is null. </exception>
	public JobConfigHostedWatcherServiceFactory(IServiceProvider serviceProvider)
	{
		ArgumentNullException.ThrowIfNull(serviceProvider, nameof(serviceProvider));

		_serviceProvider = serviceProvider;
	}

	/// <inheritdoc />
	public async Task<IJobConfigHostedWatcherService> CreateAsync<TJob, TConfig>()
		where TJob : IConfigurableJob<TConfig>
		where TConfig : class, IJobConfig
	{
		ArgumentNullException.ThrowIfNull(_serviceProvider);

		try
		{
			// Resolve IScheduler asynchronously to avoid blocking
			var schedulerFactory = _serviceProvider.GetRequiredService<ISchedulerFactory>();
			var scheduler = await schedulerFactory.GetScheduler().ConfigureAwait(false);

			// Resolve other required dependencies
			var configMonitor = _serviceProvider.GetRequiredService<IOptionsMonitor<TConfig>>();
			var loggerFactory = _serviceProvider.GetRequiredService<ILoggerFactory>();
			var logger = loggerFactory.CreateLogger<JobConfigHostedWatcherService<TJob, TConfig>>();

			// Create and return the JobConfigHostedWatcherService instance
			return new JobConfigHostedWatcherService<TJob, TConfig>(scheduler, configMonitor, logger);
		}
		catch (Exception ex)
		{
			// Log or throw a meaningful exception if service creation fails
			var logger = _serviceProvider.GetService<ILogger<JobConfigHostedWatcherServiceFactory>>();
			logger?.LogError(ex, "Error creating JobConfigHostedWatcherService for {JobType} with config {ConfigType}.",
				typeof(TJob).Name, typeof(TConfig).Name);

			throw new InvalidOperationException($"Failed to create JobConfigHostedWatcherService for {typeof(TJob).Name}.", ex);
		}
	}
}
