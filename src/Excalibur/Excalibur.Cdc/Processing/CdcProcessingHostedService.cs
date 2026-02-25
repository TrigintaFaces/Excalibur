// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Diagnostics;
using Excalibur.Cdc.Diagnostics;

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
///     cdc.UseSqlServer(connectionString)
///        .TrackTable("dbo.Orders", t => t.MapAll&lt;OrderChangedEvent&gt;())
///        .EnableBackgroundProcessing();
/// });
/// </code>
/// </para>
/// </remarks>
public partial class CdcProcessingHostedService : BackgroundService
{
	private readonly ICdcBackgroundProcessor _processor;
	private readonly IOptions<CdcProcessingOptions> _options;
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
	/// <param name="processor">The CDC background processor implementation.</param>
	/// <param name="options">The processing options.</param>
	/// <param name="logger">The logger instance.</param>
	public CdcProcessingHostedService(
		ICdcBackgroundProcessor processor,
		IOptions<CdcProcessingOptions> options,
		ILogger<CdcProcessingHostedService> logger)
	{
		_processor = processor ?? throw new ArgumentNullException(nameof(processor));
		_options = options ?? throw new ArgumentNullException(nameof(options));
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

		_isHealthy = true;
		LogBackgroundServiceStarting(_options.Value.PollingInterval);

		while (!stoppingToken.IsCancellationRequested)
		{
			try
			{
				var stopwatch = ValueStopwatch.StartNew();
				var processed = await _processor.ProcessChangesAsync(stoppingToken).ConfigureAwait(false);

				_consecutiveErrors = 0;
				_isHealthy = true;
				Interlocked.Exchange(ref _lastSuccessfulProcessingTicks, DateTimeOffset.UtcNow.UtcTicks);

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

			try
			{
				await Task.Delay(_options.Value.PollingInterval, stoppingToken).ConfigureAwait(false);
			}
			catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
			{
				break;
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

	[LoggerMessage(CdcProcessingEventId.CdcBackgroundServiceDrainTimeout, LogLevel.Warning,
		"CDC background processing service drain timeout exceeded ({DrainTimeout}).")]
	private partial void LogDrainTimeoutExceeded(TimeSpan drainTimeout);
}
