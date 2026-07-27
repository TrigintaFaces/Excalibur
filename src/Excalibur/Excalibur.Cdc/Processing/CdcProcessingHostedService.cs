// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc.Diagnostics;
using Excalibur.Dispatch.Diagnostics;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Cdc.Processing;

/// <summary>
/// Background service that polls for CDC changes and processes them through
/// a registered <see cref="ICdcBackgroundProcessor"/>.
/// </summary>
/// <remarks>
/// <para>
/// This service follows the same pattern as <c>OutboxBackgroundService</c>:
/// a polling loop with configurable interval, graceful drain on shutdown,
/// health state tracking, and metrics recording.
/// </para>
/// <para>
/// Register via the CDC builder:
/// <code>
/// services.AddCdcProcessor(cdc =>
/// {
///     cdc.UseSqlServer(sql => sql.ConnectionString(connectionString))
///        .TrackTable("dbo.Orders", t => t.MapAll&lt;OrderChangedEvent&gt;())
///        .EnableBackgroundProcessing();
/// });
/// </code>
/// </para>
/// </remarks>
internal sealed partial class CdcProcessingHostedService : BackgroundService
{
	private readonly IServiceProvider _serviceProvider;
	private readonly IOptions<CdcProcessingOptions> _options;
	private readonly TimeProvider _timeProvider;
	private readonly ILogger<CdcProcessingHostedService> _logger;

	private volatile bool _isHealthy;
	private volatile int _consecutiveErrors;
	private long _lastSuccessfulProcessingTicks;

	/// <summary>
	/// Gets a value indicating whether the service is in a healthy state.
	/// The service is considered unhealthy after consecutive processing errors
	/// exceed the configured threshold.
	/// </summary>
	public bool IsHealthy => _isHealthy;

	/// <summary>
	/// Gets the number of consecutive processing errors.
	/// Resets to zero on successful processing.
	/// </summary>
	public int ConsecutiveErrors => _consecutiveErrors;

	/// <summary>
	/// Gets the timestamp of the last successful processing cycle.
	/// </summary>
	public DateTimeOffset LastSuccessfulProcessing =>
		new(Interlocked.Read(ref _lastSuccessfulProcessingTicks), TimeSpan.Zero);

	/// <summary>
	/// Initializes a new instance of the <see cref="CdcProcessingHostedService"/> class.
	/// </summary>
	/// <param name="serviceProvider">
	/// The service provider used to resolve the <see cref="ICdcBackgroundProcessor"/> lazily at start
	/// time, so the host can be registered independently of the provider-specific processor registration
	/// order (a processor registered after <c>AddCdcProcessor()</c> is still picked up).
	/// </param>
	/// <param name="options">The processing options.</param>
	/// <param name="timeProvider">
	/// The time provider used for the last-success timestamp and the adaptive-poll / error-backoff delays,
	/// so the polling loop is deterministically testable without wall-clock sleeps.
	/// </param>
	/// <param name="logger">The logger instance.</param>
	public CdcProcessingHostedService(
		IServiceProvider serviceProvider,
		IOptions<CdcProcessingOptions> options,
		TimeProvider timeProvider,
		ILogger<CdcProcessingHostedService> logger)
	{
		_serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <inheritdoc/>
	public override async Task StopAsync(CancellationToken cancellationToken)
	{
		var drainTimeout = _options.Value.DrainTimeout;
		using var drainCts = new CancellationTokenSource(drainTimeout);
		using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(
			cancellationToken, drainCts.Token);

		try
		{
			await base.StopAsync(combinedCts.Token).ConfigureAwait(false);
		}
		catch (OperationCanceledException) when (drainCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
		{
			LogDrainTimeoutExceeded(drainTimeout);
		}
	}

	/// <inheritdoc/>
	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		if (!_options.Value.Enabled)
		{
			LogBackgroundServiceDisabled();
			return;
		}

		// Resolve the processor lazily (order-independent): the host is registered unconditionally, so a
		// provider that registers ICdcBackgroundProcessor AFTER AddCdcProcessor() is still picked up. If
		// none is registered, fail LOUD and no-op rather than validate-green-and-silently-skip.
		var processor = _serviceProvider.GetService<ICdcBackgroundProcessor>();
		if (processor is null)
		{
			LogNoBackgroundProcessorRegistered();
			return;
		}

		_isHealthy = true;
		LogBackgroundServiceStarting(_options.Value.PollingInterval);

		while (!stoppingToken.IsCancellationRequested)
		{
			var processed = 0;

			try
			{
				var stopwatch = ValueStopwatch.StartNew();
				processed = await processor.ProcessChangesAsync(stoppingToken).ConfigureAwait(false);

				_consecutiveErrors = 0;
				_isHealthy = true;
				Interlocked.Exchange(ref _lastSuccessfulProcessingTicks, _timeProvider.GetUtcNow().UtcTicks);

				if (processed > 0)
				{
					LogProcessedChanges(processed, stopwatch.Elapsed.TotalMilliseconds);
				}
			}
			catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
			{
				break;
			}
			catch (Exception ex)
			{
				_consecutiveErrors++;
				if (_consecutiveErrors >= _options.Value.UnhealthyThreshold)
				{
					_isHealthy = false;
				}

				LogBackgroundServiceError(ex);
			}

			// Adaptive polling: skip the delay when work was found so the next batch
			// is picked up immediately, reducing end-to-end latency under load.
			// Only delay on empty polls or after errors to avoid busy-spinning.
			if (processed == 0)
			{
				var delay = _options.Value.PollingInterval;

				// Exponential backoff on errors, capped at 5x polling interval.
				// Prevents rapid retry storms while allowing recovery once errors clear.
				// _consecutiveErrors resets to 0 on the next successful cycle.
				if (_consecutiveErrors > 0)
				{
					var backoffMultiplier = Math.Min(_consecutiveErrors, 5);
					delay = TimeSpan.FromTicks(delay.Ticks * backoffMultiplier);
					LogErrorBackoff(delay, _consecutiveErrors);
				}

				try
				{
					await Task.Delay(delay, _timeProvider, stoppingToken).ConfigureAwait(false);
				}
				catch (OperationCanceledException ex) when (ex.CancellationToken.IsCancellationRequested)
				{
					break;
				}
			}
		}

		_isHealthy = false;
		LogBackgroundServiceStopped();
	}

	[LoggerMessage(CdcProcessingEventId.CdcBackgroundServiceDisabled, LogLevel.Information,
		"CDC background processing service is disabled.")]
	private partial void LogBackgroundServiceDisabled();

	[LoggerMessage(CdcProcessingEventId.CdcBackgroundServiceStarting, LogLevel.Information,
		"CDC background processing service starting with polling interval {PollingInterval}.")]
	private partial void LogBackgroundServiceStarting(TimeSpan pollingInterval);

	[LoggerMessage(CdcProcessingEventId.CdcBackgroundServiceError, LogLevel.Error,
		"Error processing CDC changes.")]
	private partial void LogBackgroundServiceError(Exception exception);

	[LoggerMessage(CdcProcessingEventId.CdcBackgroundServiceStopped, LogLevel.Information,
		"CDC background processing service stopped.")]
	private partial void LogBackgroundServiceStopped();

	[LoggerMessage(CdcProcessingEventId.CdcBackgroundServiceProcessedChanges, LogLevel.Debug,
		"Processed {ChangeCount} CDC changes in {DurationMs:F1}ms.")]
	private partial void LogProcessedChanges(int changeCount, double durationMs);

	[LoggerMessage(CdcProcessingEventId.CdcBackgroundServiceNoProcessor, LogLevel.Error,
		"CDC background processing is enabled but no ICdcBackgroundProcessor is registered — CDC changes will NOT be processed. Register a provider-specific CDC processor (e.g. via UseSqlServer(...) on the CDC builder).")]
	private partial void LogNoBackgroundProcessorRegistered();

	[LoggerMessage(CdcProcessingEventId.CdcBackgroundServiceDrainTimeout, LogLevel.Warning,
		"CDC background processing service drain timeout exceeded ({DrainTimeout}).")]
	private partial void LogDrainTimeoutExceeded(TimeSpan drainTimeout);

	[LoggerMessage(CdcProcessingEventId.CdcBackgroundServiceErrorBackoff, LogLevel.Warning,
		"CDC background processing applying error backoff: {Delay} ({ConsecutiveErrors} consecutive errors).")]
	private partial void LogErrorBackoff(TimeSpan delay, int consecutiveErrors);
}
