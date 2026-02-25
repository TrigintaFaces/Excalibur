// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Jobs.Abstractions;
using Excalibur.Jobs.Diagnostics;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Quartz;

namespace Excalibur.Jobs.Core;

/// <summary>
/// Factory for creating instances of <see cref="JobConfigHostedWatcherService{TJob, TConfig}" />.
/// </summary>
public sealed partial class JobConfigHostedWatcherServiceFactory : IJobConfigHostedWatcherServiceFactory
{
	private readonly IServiceProvider _serviceProvider;

	/// <summary>
	/// Initializes a new instance of the <see cref="JobConfigHostedWatcherServiceFactory" /> class.
	/// </summary>
	/// <param name="serviceProvider"> The service provider for resolving dependencies. </param>
	/// <exception cref="ArgumentNullException"> Thrown when <paramref name="serviceProvider" /> is null. </exception>
	public JobConfigHostedWatcherServiceFactory(IServiceProvider serviceProvider)
	{
		ArgumentNullException.ThrowIfNull(serviceProvider);

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
			if (logger != null)
			{
				LogErrorCreatingService(logger, typeof(TJob).Name, typeof(TConfig).Name, ex);
			}

			throw new InvalidOperationException($"Failed to create JobConfigHostedWatcherService for {typeof(TJob).Name}.", ex);
		}
	}

	// Source-generated logging methods
	[LoggerMessage(JobsEventId.ErrorCreatingJobConfigWatcherService, LogLevel.Error,
		"Error creating JobConfigHostedWatcherService for {JobType} with config {ConfigType}.")]
	private static partial void LogErrorCreatingService(ILogger logger, string jobType, string configType, Exception ex);
}
