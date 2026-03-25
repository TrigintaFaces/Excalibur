// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.DataProcessing.Diagnostics;
using Excalibur.Dispatch.Abstractions.Diagnostics;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.DataProcessing.Processing;

/// <summary>
/// Background service that polls for pending data tasks and processes them through
/// a registered <see cref="IDataOrchestrationManager"/>.
/// </summary>
/// <remarks>
/// <para>
/// This service follows the same pattern as <c>CdcProcessingHostedService</c>:
/// a polling loop with configurable interval, graceful drain on shutdown,
/// health state tracking, and metrics recording.
/// </para>
/// <para>
/// Register via the DI extension:
/// <code>
/// services.EnableDataProcessingBackgroundService(options =>
/// {
///     options.PollingInterval = TimeSpan.FromSeconds(10);
/// });
/// </code>
/// </para>
/// </remarks>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
	"Performance",
	"CA1812:AvoidUninstantiatedInternalClasses",
	Justification = "Instantiated by the DI container as IHostedService.")]
internal sealed partial class DataProcessingHostedService : BackgroundService
{
	private readonly IServiceScopeFactory _scopeFactory;
	private readonly IOptions<DataProcessingHostedServiceOptions> _options;
	private readonly ILogger<DataProcessingHostedService> _logger;

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
	/// Initializes a new instance of the <see cref="DataProcessingHostedService"/> class.
	/// </summary>
	/// <param name="scopeFactory">The service scope factory for resolving scoped dependencies.</param>
	/// <param name="options">The hosted service options.</param>
	/// <param name="logger">The logger instance.</param>
	public DataProcessingHostedService(
		IServiceScopeFactory scopeFactory,
		IOptions<DataProcessingHostedServiceOptions> options,
		ILogger<DataProcessingHostedService> logger)
	{
		_scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
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

				// IDataOrchestrationManager is scoped (depends on scoped IDataProcessorRegistry).
				// Create a fresh scope per polling cycle to avoid captive dependency.
				using var scope = _scopeFactory.CreateScope();
				var orchestrationManager = scope.ServiceProvider.GetRequiredService<IDataOrchestrationManager>();
				await orchestrationManager.ProcessDataTasksAsync(stoppingToken).ConfigureAwait(false);

				_consecutiveErrors = 0;
				_isHealthy = true;
				Interlocked.Exchange(ref _lastSuccessfulProcessingTicks, DateTimeOffset.UtcNow.UtcTicks);

				LogProcessedTasks(stopwatch.Elapsed.TotalMilliseconds);
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
			catch (OperationCanceledException ex) when (ex.CancellationToken.IsCancellationRequested)
			{
				break;
			}
		}

		_isHealthy = false;
		LogBackgroundServiceStopped();
	}

	[LoggerMessage(DataProcessingEventId.BackgroundServiceDisabled, LogLevel.Information,
		"Data processing background service is disabled.")]
	private partial void LogBackgroundServiceDisabled();

	[LoggerMessage(DataProcessingEventId.BackgroundServiceStarting, LogLevel.Information,
		"Data processing background service starting with polling interval {PollingInterval}.")]
	private partial void LogBackgroundServiceStarting(TimeSpan pollingInterval);

	[LoggerMessage(DataProcessingEventId.BackgroundServiceError, LogLevel.Error,
		"Error processing data tasks.")]
	private partial void LogBackgroundServiceError(Exception exception);

	[LoggerMessage(DataProcessingEventId.BackgroundServiceStopped, LogLevel.Information,
		"Data processing background service stopped.")]
	private partial void LogBackgroundServiceStopped();

	[LoggerMessage(DataProcessingEventId.BackgroundServiceProcessedTasks, LogLevel.Debug,
		"Processed data tasks in {DurationMs:F1}ms.")]
	private partial void LogProcessedTasks(double durationMs);

	[LoggerMessage(DataProcessingEventId.BackgroundServiceDrainTimeout, LogLevel.Warning,
		"Data processing background service drain timeout exceeded ({DrainTimeout}).")]
	private partial void LogDrainTimeoutExceeded(TimeSpan drainTimeout);
}
