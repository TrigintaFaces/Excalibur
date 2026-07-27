// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Messaging;

using Excalibur.Saga.Diagnostics;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Saga.Services;

/// <summary>
/// Background service that periodically purges completed sagas older than
/// <see cref="SagaOptions.SagaRetentionPeriod"/> by invoking
/// <see cref="ISagaStore.PurgeCompletedBeforeAsync"/> on a fixed
/// <see cref="SagaOptions.CleanupInterval"/>.
/// </summary>
/// <remarks>
/// <para>
/// The service self-gates on <see cref="SagaOptions.EnableAutomaticCleanup"/>: when the option is
/// <see langword="false"/> it exits immediately without touching the store, so registering it
/// unconditionally is safe. When enabled, it drives the purge primitive that is otherwise the
/// consumer's responsibility. The <see cref="ISagaStore"/> is resolved lazily from the container per
/// cleanup cycle (not injected into the constructor) so the service constructs even when no saga store
/// has been registered yet — a supported intermediate state where cleanup is simply left disabled.
/// </para>
/// <para>
/// All timing is sourced from an injected <see cref="TimeProvider"/> — the interval loop and the
/// retention threshold both use it, so no wall-clock call is on the decision path and the schedule is
/// deterministically testable.
/// </para>
/// </remarks>
internal sealed partial class SagaCleanupBackgroundService : BackgroundService
{
	private readonly IServiceProvider _serviceProvider;
	private readonly SagaOptions _options;
	private readonly TimeProvider _timeProvider;
	private readonly ILogger<SagaCleanupBackgroundService> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="SagaCleanupBackgroundService"/> class.
	/// </summary>
	/// <param name="serviceProvider">
	/// The service provider used to resolve the <see cref="ISagaStore"/> lazily per cleanup cycle, so the
	/// hosted service can be registered unconditionally without forcing a store to exist at construction.
	/// </param>
	/// <param name="options">The saga options driving the retention window and cadence.</param>
	/// <param name="timeProvider">The time source for the interval loop and retention threshold.</param>
	/// <param name="logger">The logger instance.</param>
	public SagaCleanupBackgroundService(
		IServiceProvider serviceProvider,
		IOptions<SagaOptions> options,
		TimeProvider timeProvider,
		ILogger<SagaCleanupBackgroundService> logger)
	{
		_serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
		_options = options?.Value ?? throw new ArgumentNullException(nameof(options));
		_timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <inheritdoc />
	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		if (!_options.EnableAutomaticCleanup)
		{
			// Disabled: the manual purge primitive remains available to the consumer.
			return;
		}

		LogCleanupServiceStarted(_options.CleanupInterval, _options.SagaRetentionPeriod);

		using var timer = new PeriodicTimer(_options.CleanupInterval, _timeProvider);

		try
		{
			while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
			{
				try
				{
					// Resolve the store lazily per cycle so the service constructs even when no store is
					// registered; a scope keeps scoped-lifetime store providers correct.
					await using var scope = _serviceProvider.CreateAsyncScope();
					var sagaStore = scope.ServiceProvider.GetService<ISagaStore>();
					if (sagaStore is null)
					{
						// Cleanup was enabled but no saga store is registered — a misconfiguration. Fail
						// visible and stop rather than spin every interval.
						LogCleanupCycleFailed(new InvalidOperationException(
							"Automatic saga cleanup is enabled but no ISagaStore is registered. Register a saga store " +
							"(e.g. AddInMemorySagaStore() or a persistent provider) or disable EnableAutomaticCleanup."));
						break;
					}

					var threshold = _timeProvider.GetUtcNow() - _options.SagaRetentionPeriod;

					// Estate-wide, deliberately and explicitly. This service is an operator-level retention
					// sweep: it runs on a timer with no request behind it, so it establishes no tenant scope,
					// and it is responsible for every tenant's completed sagas rather than any one tenant's.
					//
					// Calling the tenant-scoped purge here would be wrong in both host shapes, and silently so. In a
					// single-tenant host the ambient context resolves to that host's tenant, so the sweep would
					// delete only its rows and leave every other tenant's to accumulate forever. In a multi-tenant
					// host no tenant is established on a timer callback, so the scoped purge would address the
					// untenanted partition alone and quietly ignore every real tenant. Neither failure surfaces:
					// both return a plausible count and log a successful cycle.
					var purged = await sagaStore
						.PurgeAllTenantsCompletedBeforeAsync(threshold, stoppingToken)
						.ConfigureAwait(false);

					LogCleanupCyclePurged(purged, threshold);
				}
				catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
				{
					break;
				}
				catch (NotSupportedException ex)
				{
					// The configured store cannot purge by age: retrying every interval is futile, so
					// fail visible and stop the loop rather than spin.
					LogCleanupCycleFailed(ex);
					break;
				}
#pragma warning disable CA1031 // A cleanup cycle must not tear down the host; log and retry next tick.
				catch (Exception ex)
#pragma warning restore CA1031
				{
					LogCleanupCycleFailed(ex);
				}
			}
		}
		catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
		{
			// Graceful shutdown.
		}

		LogCleanupServiceStopped();
	}

	[LoggerMessage(SagaEventId.CleanupServiceStarted, LogLevel.Information,
		"Saga automatic cleanup service started (interval {CleanupInterval}, retention {RetentionPeriod})")]
	private partial void LogCleanupServiceStarted(TimeSpan cleanupInterval, TimeSpan retentionPeriod);

	[LoggerMessage(SagaEventId.CleanupServiceStopped, LogLevel.Information,
		"Saga automatic cleanup service stopping")]
	private partial void LogCleanupServiceStopped();

	[LoggerMessage(SagaEventId.CleanupCyclePurged, LogLevel.Debug,
		"Saga cleanup purged {PurgedCount} completed sagas older than {Threshold}")]
	private partial void LogCleanupCyclePurged(int purgedCount, DateTimeOffset threshold);

	[LoggerMessage(SagaEventId.CleanupCycleFailed, LogLevel.Error,
		"Saga cleanup cycle failed; will retry on the next interval unless the store cannot purge")]
	private partial void LogCleanupCycleFailed(Exception exception);
}
