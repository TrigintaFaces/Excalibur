// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Transport;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Diagnostics;
using Excalibur.Dispatch.Messaging;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Cron timer transport adapter that generates synthetic messages on a schedule.
/// </summary>
/// <remarks>
/// <para>
/// This is a <strong> trigger-only </strong> transport that fires messages according to a cron expression. Unlike other transport adapters
/// that receive messages from external systems, this adapter generates synthetic <see cref="CronTimerTriggerMessage" /> instances at
/// scheduled intervals.
/// </para>
/// <para> Key characteristics:
/// <list type="bullet">
/// <item> <see cref="SendAsync" /> is a no-op since this is a receive-only transport </item>
/// <item> Messages are generated internally based on the configured cron expression </item>
/// <item> Supports 5-field and 6-field (with seconds) cron expressions via <see cref="ICronScheduler" /> </item>
/// <item> TimeZone-aware scheduling with configurable overlap prevention </item>
/// </list>
/// </para>
/// <para> Implements <see cref="ITransportHealthChecker" /> for integration with ASP.NET Core health checks and the <see cref="MultiTransportHealthCheck" />. </para>
/// </remarks>
public sealed partial class CronTimerTransportAdapter : ITransportAdapter, ITransportHealthChecker, IAsyncDisposable
{
	/// <summary>
	/// The default transport name for cron timer adapters.
	/// </summary>
	public const string DefaultName = "CronTimer";

	/// <summary>
	/// The transport type identifier.
	/// </summary>
	public const string TransportTypeName = "crontimer";

	private readonly ILogger<CronTimerTransportAdapter> _logger;
	private readonly ICronScheduler _cronScheduler;
	private readonly IServiceProvider _serviceProvider;
	private readonly CronTimerTransportAdapterOptions _options;
	private readonly SemaphoreSlim _executionLock = new(1, 1);

	private ICronExpression? _cronExpression;
	private CancellationTokenSource? _timerCts;
	private Task? _timerTask;
	private IDispatcher? _dispatcher;
	private volatile bool _disposed;

	// Health check and metrics tracking
	private long _totalTriggers;

	private long _successfulTriggers;
	private long _failedTriggers;
	private long _skippedOverlapTriggers;
	private DateTimeOffset _lastTriggerTime;
	private DateTimeOffset? _nextScheduledTrigger;
	private DateTimeOffset _lastHealthCheck = DateTimeOffset.UtcNow;
	private TransportHealthStatus _lastStatus = TransportHealthStatus.Healthy;

	/// <summary>
	/// Initializes a new instance of the <see cref="CronTimerTransportAdapter" /> class.
	/// </summary>
	/// <param name="logger"> The logger instance. </param>
	/// <param name="cronScheduler"> The cron scheduler for parsing expressions. </param>
	/// <param name="serviceProvider"> The service provider for resolving dependencies. </param>
	/// <param name="options"> The adapter options. </param>
	public CronTimerTransportAdapter(
		ILogger<CronTimerTransportAdapter> logger,
		ICronScheduler cronScheduler,
		IServiceProvider serviceProvider,
		CronTimerTransportAdapterOptions options)
	{
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_cronScheduler = cronScheduler ?? throw new ArgumentNullException(nameof(cronScheduler));
		_serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
		_options = options ?? throw new ArgumentNullException(nameof(options));

		if (string.IsNullOrWhiteSpace(options.CronExpression))
		{
			throw new ArgumentException(
				Resources.CronTimerTransportAdapter_CronExpressionIsRequired,
				nameof(options));
		}
	}

	/// <inheritdoc />
	public string Name => _options.Name ?? DefaultName;

	/// <inheritdoc />
	public string TransportType => TransportTypeName;

	/// <inheritdoc />
	public bool IsRunning { get; private set; }

	/// <inheritdoc />
	public Task<IMessageResult> ReceiveAsync(
		object transportMessage,
		IDispatcher dispatcher,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(transportMessage);
		ArgumentNullException.ThrowIfNull(dispatcher);

		if (!IsRunning)
		{
			return Task.FromResult<IMessageResult>(Messaging.MessageResult.Failed(new MessageProblemDetails
			{
				Type = "urn:dispatch:transport:not-running",
				Title = "Transport Not Running",
				ErrorCode = 10001,
				Status = 503,
				Detail = "The cron timer transport adapter is not running",
				Instance = $"crontimer-adapter-{Guid.NewGuid()}",
			}));
		}

		if (transportMessage is not CronTimerTriggerMessage message)
		{
			return Task.FromResult<IMessageResult>(Messaging.MessageResult.Failed(new MessageProblemDetails
			{
				Type = "urn:dispatch:transport:invalid-message-type",
				Title = "Invalid Message Type",
				ErrorCode = 10002,
				Status = 400,
				Detail = $"Expected CronTimerTriggerMessage but received {transportMessage.GetType().Name}",
				Instance = $"crontimer-adapter-{Guid.NewGuid()}",
			}));
		}

		return ProcessTriggerMessageAsync(message, dispatcher, cancellationToken);
	}

	/// <inheritdoc />
	public Task SendAsync(
		IDispatchMessage message,
		string destination,
		CancellationToken cancellationToken)
	{
		// Cron timer is a trigger-only transport - SendAsync is a no-op
		LogSendAsyncNotSupported();
		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public async Task StartAsync(CancellationToken cancellationToken)
	{
		if (IsRunning)
		{
			return;
		}

		LogStarting();

		// Parse the cron expression
		_cronExpression = _cronScheduler.Parse(_options.CronExpression, _options.TimeZone);

		IsRunning = true;

		_timerCts?.Dispose();
		_timerCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

		// Start the timer loop
		_timerTask = Task.Run(() => RunTimerLoopAsync(_timerCts.Token), CancellationToken.None);

		// Record transport start metrics
		TransportMeter.RecordTransportStarted(Name, TransportType);
		TransportMeter.UpdateTransportState(Name, TransportType, isConnected: true);

		// Fire immediately on startup if configured
		if (_options.RunOnStartup && _dispatcher != null)
		{
			await TriggerExecutionAsync(_timerCts.Token).ConfigureAwait(false);
		}
	}

	/// <inheritdoc />
	public async Task StopAsync(CancellationToken cancellationToken)
	{
		if (!IsRunning)
		{
			return;
		}

		LogStopping();
		IsRunning = false;

		// Record transport stop metrics
		TransportMeter.RecordTransportStopped(Name, TransportType);
		TransportMeter.UpdateTransportState(Name, TransportType, isConnected: false);

		await (_timerCts?.CancelAsync() ?? Task.CompletedTask).ConfigureAwait(false);

		if (_timerTask is not null)
		{
			try
			{
				await _timerTask.WaitAsync(cancellationToken).ConfigureAwait(false);
			}
			catch (OperationCanceledException)
			{
				// Expected during cancellation
			}
		}

		_timerTask = null;
		_timerCts?.Dispose();
		_timerCts = null;
	}

	/// <summary>
	/// Sets the dispatcher to use for routing triggered messages.
	/// </summary>
	/// <param name="dispatcher"> The dispatcher instance. </param>
	/// <remarks> This must be called before messages can be processed. Typically called by the transport infrastructure during setup. </remarks>
	public void SetDispatcher(IDispatcher dispatcher)
	{
		_dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
	}

	#region ITransportHealthChecker Implementation

	/// <inheritdoc />
	string ITransportHealthChecker.Name => Name;

	/// <inheritdoc />
	string ITransportHealthChecker.TransportType => TransportType;

	/// <inheritdoc />
	TransportHealthCheckCategory ITransportHealthChecker.Categories =>
		TransportHealthCheckCategory.Connectivity | TransportHealthCheckCategory.Configuration;

	/// <inheritdoc />
	public Task<TransportHealthCheckResult> CheckHealthAsync(
		TransportHealthCheckContext context,
		CancellationToken cancellationToken)
	{
		var stopwatch = Stopwatch.StartNew();

		var data = new Dictionary<string, object>(StringComparer.Ordinal)
		{
			["CronExpression"] = _options.CronExpression,
			["TimeZone"] = _options.TimeZone.Id,
			["TotalTriggers"] = _totalTriggers,
			["SuccessfulTriggers"] = _successfulTriggers,
			["FailedTriggers"] = _failedTriggers,
			["SkippedOverlapTriggers"] = _skippedOverlapTriggers,
			["PreventOverlap"] = _options.PreventOverlap,
			["RunOnStartup"] = _options.RunOnStartup,
		};

		if (_lastTriggerTime != default)
		{
			data["LastTriggerTime"] = _lastTriggerTime.ToString("O");
		}

		if (_nextScheduledTrigger.HasValue)
		{
			data["NextScheduledTrigger"] = _nextScheduledTrigger.Value.ToString("O");
		}

		TransportHealthCheckResult result;

		if (!IsRunning)
		{
			result = TransportHealthCheckResult.Unhealthy(
				"Cron timer transport adapter is not running",
				context.RequestedCategories,
				stopwatch.Elapsed,
				data);
		}
		else if (_failedTriggers > 0 && _failedTriggers > _successfulTriggers / 5)
		{
			// More than 20% failures - degraded
			result = TransportHealthCheckResult.Degraded(
				$"Cron timer has elevated failure rate: {_failedTriggers}/{_totalTriggers}",
				context.RequestedCategories,
				stopwatch.Elapsed,
				data);
		}
		else
		{
			result = TransportHealthCheckResult.Healthy(
				$"Cron timer transport is healthy, next trigger: {_nextScheduledTrigger?.ToString("O") ?? "calculating..."}",
				context.RequestedCategories,
				stopwatch.Elapsed,
				data);
		}

		stopwatch.Stop();
		_lastHealthCheck = DateTimeOffset.UtcNow;
		_lastStatus = result.Status;

		return Task.FromResult(result);
	}

	/// <inheritdoc />
	public Task<TransportHealthCheckResult> CheckQuickHealthAsync(CancellationToken cancellationToken)
	{
		var stopwatch = Stopwatch.StartNew();

		var status = IsRunning
			? TransportHealthStatus.Healthy
			: TransportHealthStatus.Unhealthy;

		var description = IsRunning
			? "Cron timer transport adapter is running"
			: "Cron timer transport adapter is not running";

		var result = new TransportHealthCheckResult(
			status,
			description,
			TransportHealthCheckCategory.Connectivity,
			stopwatch.Elapsed);

		_lastHealthCheck = DateTimeOffset.UtcNow;
		_lastStatus = status;

		return Task.FromResult(result);
	}

	/// <inheritdoc />
	public Task<TransportHealthMetrics> GetHealthMetricsAsync(CancellationToken cancellationToken)
	{
		var successRate = _totalTriggers > 0
			? (double)_successfulTriggers / _totalTriggers
			: 1.0;

		var metrics = new TransportHealthMetrics(
			lastCheckTimestamp: _lastHealthCheck,
			lastStatus: _lastStatus,
			consecutiveFailures: IsRunning ? 0 : 1,
			totalChecks: 1,
			successRate: successRate,
			averageCheckDuration: TimeSpan.FromMilliseconds(1),
			customMetrics: new Dictionary<string, object>(StringComparer.Ordinal)
			{
				["TotalTriggers"] = _totalTriggers,
				["SuccessfulTriggers"] = _successfulTriggers,
				["FailedTriggers"] = _failedTriggers,
				["SkippedOverlapTriggers"] = _skippedOverlapTriggers,
				["CronExpression"] = _options.CronExpression,
			});

		return Task.FromResult(metrics);
	}

	#endregion ITransportHealthChecker Implementation

	/// <inheritdoc />
	public async ValueTask DisposeAsync()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;

		try
		{
			using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
			await StopAsync(cts.Token).ConfigureAwait(false);
		}
		catch (OperationCanceledException)
		{
			// Expected during cancellation
		}
		catch (ObjectDisposedException)
		{
			// Expected if resources already disposed
		}

		// Clean up metrics tracking
		TransportMeter.RemoveTransport(Name);

		_executionLock.Dispose();
		GC.SuppressFinalize(this);
	}

	private async Task RunTimerLoopAsync(CancellationToken cancellationToken)
	{
		while (!cancellationToken.IsCancellationRequested && IsRunning)
		{
			try
			{
				var now = DateTimeOffset.UtcNow;
				var nextOccurrence = _cronExpression?.GetNextOccurrenceUtc(now);

				if (!nextOccurrence.HasValue)
				{
					// No more occurrences - stop the timer
					_nextScheduledTrigger = null;
					break;
				}

				_nextScheduledTrigger = nextOccurrence.Value;
				LogNextOccurrence(Name, nextOccurrence.Value);

				var delay = nextOccurrence.Value - now;
				if (delay > TimeSpan.Zero)
				{
					await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
				}

				await TriggerExecutionAsync(cancellationToken).ConfigureAwait(false);
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				// Expected during shutdown
				break;
			}
			catch (Exception ex)
			{
				LogTimerExecutionFailed(Name, ex);
				TransportMeter.RecordError(Name, TransportType, "timer_loop_error");

				// Brief delay before retrying to prevent tight loop on persistent errors
				try
				{
					await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);
				}
				catch (OperationCanceledException)
				{
					break;
				}
			}
		}
	}

	private async Task TriggerExecutionAsync(CancellationToken cancellationToken)
	{
		if (_dispatcher == null)
		{
			return;
		}

		// Handle overlap prevention
		if (_options.PreventOverlap)
		{
			if (!await _executionLock.WaitAsync(0, cancellationToken).ConfigureAwait(false))
			{
				LogSkippingOverlap(Name);
				_ = Interlocked.Increment(ref _skippedOverlapTriggers);
				return;
			}
		}
		else
		{
			await _executionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
		}

		try
		{
			var triggerTime = DateTimeOffset.UtcNow;
			_lastTriggerTime = triggerTime;

			_ = Interlocked.Increment(ref _totalTriggers);
			LogTimerFired(Name, triggerTime);

			// Use factory if provided (for typed messages), otherwise create default
			var triggerMessage = _options.MessageFactory?.Invoke(
									 Name,
									 _options.CronExpression,
									 triggerTime,
									 _options.TimeZone.Id)
								 ?? new CronTimerTriggerMessage
								 {
									 TimerName = Name,
									 CronExpression = _options.CronExpression,
									 TriggerTimeUtc = triggerTime,
									 TimeZone = _options.TimeZone.Id,
								 };

			_ = await ReceiveAsync(triggerMessage, _dispatcher, cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			_ = _executionLock.Release();
		}
	}

	private async Task<IMessageResult> ProcessTriggerMessageAsync(
		CronTimerTriggerMessage message,
		IDispatcher dispatcher,
		CancellationToken cancellationToken)
	{
		var messageId = Guid.NewGuid().ToString();
		var stopwatch = Stopwatch.StartNew();

		try
		{
			var context = new MessageContext(message, _serviceProvider)
			{
				MessageId = messageId,
				MessageType = typeof(CronTimerTriggerMessage).FullName,
				ReceivedTimestampUtc = DateTimeOffset.UtcNow,
			};

			var result = await dispatcher.DispatchAsync(message, context, cancellationToken).ConfigureAwait(false);

			stopwatch.Stop();
			TransportMeter.RecordMessageReceived(Name, TransportType, nameof(CronTimerTriggerMessage));
			TransportMeter.RecordReceiveDuration(Name, TransportType, stopwatch.Elapsed.TotalMilliseconds);
			_ = Interlocked.Increment(ref _successfulTriggers);

			return result;
		}
		catch (Exception ex)
		{
			stopwatch.Stop();
			LogTimerExecutionFailed(Name, ex);
			TransportMeter.RecordError(Name, TransportType, "processing_failed");
			TransportMeter.RecordReceiveDuration(Name, TransportType, stopwatch.Elapsed.TotalMilliseconds);
			_ = Interlocked.Increment(ref _failedTriggers);

			return Messaging.MessageResult.Failed(new MessageProblemDetails
			{
				Type = "urn:dispatch:transport:processing-failed",
				Title = "Message Processing Failed",
				ErrorCode = 10003,
				Status = 500,
				Detail = ex.Message,
				Instance = $"message-{messageId}",
			});
		}
	}

	// Source-generated logging methods
	[LoggerMessage(DeliveryEventId.CronTimerTransportStarted, LogLevel.Information,
		"Starting cron timer transport adapter")]
	private partial void LogStarting();

	[LoggerMessage(DeliveryEventId.CronTimerTransportStopped, LogLevel.Information,
		"Stopping cron timer transport adapter")]
	private partial void LogStopping();

	[LoggerMessage(DeliveryEventId.CronTimerFired, LogLevel.Debug,
		"Cron timer '{Name}' fired at {TriggerTime}")]
	private partial void LogTimerFired(string name, DateTimeOffset triggerTime);

	[LoggerMessage(DeliveryEventId.CronTimerNextOccurrence, LogLevel.Debug,
		"Next occurrence for '{Name}' scheduled at {NextTime}")]
	private partial void LogNextOccurrence(string name, DateTimeOffset nextTime);

	[LoggerMessage(DeliveryEventId.CronTimerExecutionFailed, LogLevel.Error,
		"Cron timer execution failed for '{Name}'")]
	private partial void LogTimerExecutionFailed(string name, Exception ex);

	[LoggerMessage(DeliveryEventId.CronTimerSkippingOverlap, LogLevel.Warning,
		"Skipping overlapping execution for cron timer '{Name}'")]
	private partial void LogSkippingOverlap(string name);

	[LoggerMessage(DeliveryEventId.CronTimerSendNotSupported, LogLevel.Debug,
		"SendAsync called on cron timer adapter - this is a no-op for trigger-only transports")]
	private partial void LogSendAsyncNotSupported();
}

/// <summary>
/// Configuration options for the cron timer transport adapter.
/// </summary>
public sealed class CronTimerTransportAdapterOptions
{
	/// <summary>
	/// Gets or sets the name of this transport adapter instance.
	/// </summary>
	/// <value> The transport name. Default is "CronTimer". </value>
	public string? Name { get; set; }

	/// <summary>
	/// Gets or sets the cron expression for scheduling.
	/// </summary>
	/// <value> The cron expression string. </value>
	/// <remarks>
	/// Supports both 5-field (minute-level) and 6-field (second-level) cron expressions. Examples:
	/// <list type="bullet">
	/// <item> "0 * * * *" - Every hour at minute 0 </item>
	/// <item> "*/5 * * * *" - Every 5 minutes </item>
	/// <item> "0 0 * * *" - Daily at midnight </item>
	/// <item> "0 0 0 * * *" - Daily at midnight (6-field with seconds) </item>
	/// </list>
	/// </remarks>
	public string CronExpression { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the time zone for the cron expression.
	/// </summary>
	/// <value> The time zone used when evaluating the cron schedule. Default is UTC. </value>
	public TimeZoneInfo TimeZone { get; set; } = TimeZoneInfo.Utc;

	/// <summary>
	/// Gets or sets a value indicating whether to run the timer immediately on startup.
	/// </summary>
	/// <value> <see langword="true" /> to trigger on startup; otherwise, <see langword="false" />. </value>
	public bool RunOnStartup { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to prevent overlapping executions.
	/// </summary>
	/// <value> <see langword="true" /> to skip runs when a previous execution is still active. Default is <see langword="true" />. </value>
	public bool PreventOverlap { get; set; } = true;

	/// <summary>
	/// Gets or sets the factory function for creating trigger messages.
	/// </summary>
	/// <value>
	/// A factory function that creates <see cref="CronTimerTriggerMessage"/> instances.
	/// If null, the default untyped <see cref="CronTimerTriggerMessage"/> is created.
	/// </value>
	/// <remarks>
	/// <para>
	/// This factory is used internally to support typed timer messages.
	/// When using <c>AddCronTimerTransport&lt;TTimer&gt;</c>, this factory is set
	/// to create <see cref="CronTimerTriggerMessage{TTimer}"/> instances.
	/// </para>
	/// <para>
	/// The factory receives the timer name, cron expression, trigger time, and time zone ID.
	/// </para>
	/// </remarks>
	internal Func<string, string, DateTimeOffset, string, CronTimerTriggerMessage>? MessageFactory { get; set; }
}

/// <summary>
/// Synthetic message generated by the cron timer transport when a scheduled trigger fires.
/// </summary>
/// <remarks>
/// <para>
/// This message is dispatched through the pipeline when the cron timer fires,
/// allowing handlers to react to scheduled triggers.
/// </para>
/// <para>
/// For type-safe handler routing with multiple timers, use
/// <see cref="CronTimerTriggerMessage{TTimer}"/> with a marker type.
/// </para>
/// </remarks>
public record CronTimerTriggerMessage : IDispatchEvent
{
	/// <summary>
	/// Gets or sets the name of the timer that fired.
	/// </summary>
	/// <value> The timer name. </value>
	public string TimerName { get; init; } = string.Empty;

	/// <summary>
	/// Gets or sets the cron expression that triggered this message.
	/// </summary>
	/// <value> The cron expression string. </value>
	public string CronExpression { get; init; } = string.Empty;

	/// <summary>
	/// Gets or sets the UTC time when the timer fired.
	/// </summary>
	/// <value> The trigger timestamp in UTC. </value>
	public DateTimeOffset TriggerTimeUtc { get; init; }

	/// <summary>
	/// Gets or sets the time zone ID used for the cron schedule.
	/// </summary>
	/// <value> The time zone identifier. </value>
	public string TimeZone { get; init; } = "UTC";
}
