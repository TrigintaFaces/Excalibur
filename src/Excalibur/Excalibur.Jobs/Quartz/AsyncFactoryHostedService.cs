// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Jobs.Abstractions;
using Excalibur.Jobs.Core;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Excalibur.Jobs.Quartz;

/// <summary>
/// A hosted service wrapper that creates the actual job watcher service asynchronously to avoid blocking async calls during DI registration.
/// </summary>
/// <typeparam name="TJob"> The type of the job to be managed. </typeparam>
/// <typeparam name="TConfig"> The type of the job configuration. </typeparam>
internal sealed class AsyncFactoryHostedService<TJob, TConfig> : IHostedService
	where TJob : IConfigurableJob<TConfig>
	where TConfig : class, IJobConfig
{
	private readonly IServiceProvider _serviceProvider;
	private IJobConfigHostedWatcherService? _innerService;

	/// <summary>
	/// Initializes a new instance of the <see cref="AsyncFactoryHostedService{TJob, TConfig}" /> class.
	/// </summary>
	/// <param name="serviceProvider"> The service provider for resolving dependencies. </param>
	/// <exception cref="ArgumentNullException"> Thrown when <paramref name="serviceProvider" /> is null. </exception>
	public AsyncFactoryHostedService(IServiceProvider serviceProvider)
	{
		ArgumentNullException.ThrowIfNull(serviceProvider);
		_serviceProvider = serviceProvider;
	}

	/// <inheritdoc />
	public async Task StartAsync(CancellationToken cancellationToken)
	{
		// Create the actual service asynchronously during startup
		var factory = _serviceProvider.GetRequiredService<IJobConfigHostedWatcherServiceFactory>();
		_innerService = await factory.CreateAsync<TJob, TConfig>().ConfigureAwait(false);

		// Start the inner service
		await _innerService.StartAsync(cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task StopAsync(CancellationToken cancellationToken)
	{
		// Stop the inner service if it was created
		if (_innerService != null)
		{
			await _innerService.StopAsync(cancellationToken).ConfigureAwait(false);
		}
	}
}
