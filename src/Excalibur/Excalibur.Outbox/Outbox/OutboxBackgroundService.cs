// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Diagnostics;

using Excalibur.Outbox.Diagnostics;
using Excalibur.Outbox.Health;
using Excalibur.Outbox.Partitioning;
using Excalibur.Outbox.Processing;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Outbox.Outbox;

/// <summary>
/// Configuration options for the outbox background service.
/// </summary>
public sealed class OutboxProcessingOptions
{
	/// <summary>
	/// Gets or sets the interval between polling cycles.
	/// </summary>
	public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(5);

	/// <summary>
	/// Gets or sets the maximum number of retries for failed messages.
	/// </summary>
	public int MaxRetries { get; set; } = 3;

	/// <summary>
	/// Gets or sets whether to process scheduled messages.
	/// </summary>
	public bool ProcessScheduledMessages { get; set; } = true;

	/// <summary>
	/// Gets or sets whether to retry failed messages.
	/// </summary>
	public bool RetryFailedMessages { get; set; } = true;

	/// <summary>
	/// Gets or sets whether this instance is enabled.
	/// </summary>
	public bool Enabled { get; set; } = true;

	/// <summary>
	/// Gets or sets the drain timeout in seconds for graceful shutdown.
	/// </summary>
	/// <value>The drain timeout in seconds. Default is 30.</value>
	/// <remarks>
	/// When the service is stopping, this timeout controls how long to wait for
	/// in-flight processing to complete before forcing shutdown.
	/// </remarks>
	public int DrainTimeoutSeconds { get; set; } = 30;

	/// <summary>
	/// Gets the drain timeout as a <see cref="TimeSpan"/>.
	/// </summary>
	/// <value>The drain timeout duration.</value>
	public TimeSpan DrainTimeout => TimeSpan.FromSeconds(DrainTimeoutSeconds);
}

/// <summary>
/// Background service that processes outbox messages for reliable delivery.
/// This is the <b>Excalibur-level</b> (full-featured) outbox background service.
/// </summary>
/// <remarks>
/// <para>
/// This service polls the outbox store for pending messages and publishes them
/// to the message bus. It handles retries for failed messages and processes
/// scheduled messages when they become due.
/// </para>
/// <para>
/// When an <see cref="IProcessingGate"/> is registered (e.g., via
/// <c>WithLeaderElection()</c>), the service checks the gate before each
/// processing cycle and skips if <see cref="IProcessingGate.ShouldProcess"/>
/// returns <see langword="false"/>.
/// </para>
/// <para>
/// For a lightweight Dispatch-level outbox service without leader election or health
/// state, see <c>Excalibur.Dispatch.BackgroundServices.OutboxBackgroundService</c>.
/// <b>Do not register both</b> — this Excalibur.Outbox version supersedes the Dispatch version.
/// </para>
/// </remarks>
internal sealed partial class OutboxBackgroundService : BackgroundService
{
	private readonly IOutboxPublisher _publisher;
	private readonly IOptions<OutboxProcessingOptions> _options;
	private readonly IProcessingGate? _gate;
	private readonly BackgroundServiceHealthState? _healthState;
	private readonly IOutboxPartitioner? _partitioner;
	private readonly IOptions<Partitioning.OutboxPartitionOptions>? _partitionOptions;
	private readonly IServiceProvider _serviceProvider;
	private readonly ILogger<OutboxBackgroundService> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="OutboxBackgroundService"/> class.
	/// </summary>
	/// <param name="publisher">The outbox publisher for message delivery.</param>
	/// <param name="options">The processing options.</param>
	/// <param name="serviceProvider">The service provider for creating scoped processors.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="healthState">Optional health state tracker for health check integration.</param>
	/// <param name="gate">Optional processing gate (e.g., leader election) that controls whether this instance should process.</param>
	/// <param name="partitioner">Optional outbox partitioner. When provided, runs N parallel processor loops (one per partition) instead of a single publisher loop.</param>
	/// <param name="partitionOptions">Optional partition-specific options (polling interval, error backoff). Used only when <paramref name="partitioner"/> is provided.</param>
	public OutboxBackgroundService(
		IOutboxPublisher publisher,
		IOptions<OutboxProcessingOptions> options,
		IServiceProvider serviceProvider,
		ILogger<OutboxBackgroundService> logger,
		BackgroundServiceHealthState? healthState = null,
		IProcessingGate? gate = null,
		IOutboxPartitioner? partitioner = null,
		IOptions<Partitioning.OutboxPartitionOptions>? partitionOptions = null)
	{
		_publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_healthState = healthState;
		_gate = gate;
		_partitioner = partitioner;
		_partitionOptions = partitionOptions;
	}

	/// <inheritdoc/>
	public override async Task StopAsync(CancellationToken cancellationToken)
	{
		_healthState?.MarkStopped();

		using var drainActivity = BackgroundServiceActivitySource.StartDrain("outbox");

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

		_healthState?.MarkStarted();

		// Partitioned mode: run N parallel processor loops (one per partition × processors-per-partition)
		if (_partitioner is not null)
		{
			var processorsPerPartition = _partitionOptions?.Value.ProcessorCountPerPartition ?? 1;
			var totalProcessors = _partitioner.PartitionCount * processorsPerPartition;
			LogPartitionedOutboxStarting(totalProcessors);

			var tasks = new Task[totalProcessors];
			var taskIndex = 0;
			for (var i = 0; i < _partitioner.PartitionCount; i++)
			{
				for (var p = 0; p < processorsPerPartition; p++)
				{
					var partitionId = i;
					var processorIndex = p;
					tasks[taskIndex++] = ExecutePartitionAsync(partitionId, processorIndex, stoppingToken);
				}
			}

			await Task.WhenAll(tasks).ConfigureAwait(false);
			_healthState?.MarkStopped();
			LogBackgroundServiceStopped();
			return;
		}

		// Single-processor mode (default)
		LogBackgroundServiceStarting(_options.Value.PollingInterval);

		while (!stoppingToken.IsCancellationRequested)
		{
			try
			{
				// Check processing gate (e.g., leader election)
				if (_gate is not null && !_gate.ShouldProcess)
				{
					LogSkippedNotLeader();
				}
				else
				{
					using var cycleActivity = BackgroundServiceActivitySource.StartProcessingCycle("outbox", "pending");
					await ProcessOutboxAsync(stoppingToken).ConfigureAwait(false);
				}
			}
			catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
			{
				// Normal shutdown, don't log as error
				break;
			}
			catch (Exception ex)
			{
				LogBackgroundServiceError(ex);
				BackgroundServiceMetrics.RecordProcessingError(
					BackgroundServiceTypes.Outbox,
					ex.GetType().Name);
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

		_healthState?.MarkStopped();
		LogBackgroundServiceStopped();
	}

	/// <summary>
	/// Runs a single partition's processing loop using a scoped <see cref="IOutboxProcessor"/>.
	/// </summary>
	private async Task ExecutePartitionAsync(int partitionId, int processorIndex, CancellationToken stoppingToken)
	{
		LogPartitionStarted(partitionId);
		var scope = _serviceProvider.CreateAsyncScope();

		// Use partition-specific intervals when configured, otherwise fall back to global processing options
		var pollingInterval = _partitionOptions?.Value.PollingInterval ?? _options.Value.PollingInterval;
		var errorBackoff = _partitionOptions?.Value.ErrorBackoffInterval
			?? (_options.Value.PollingInterval + _options.Value.PollingInterval);

		try
		{
			var processor = scope.ServiceProvider.GetRequiredService<IOutboxProcessor>();
			processor.Init($"partitioned-{partitionId}-{processorIndex}");

			while (!stoppingToken.IsCancellationRequested)
			{
				try
				{
					if (_gate is not null && !_gate.ShouldProcess)
					{
						await Task.Delay(pollingInterval, stoppingToken).ConfigureAwait(false);
						continue;
					}

					var dispatched = await processor.DispatchPendingMessagesAsync(stoppingToken).ConfigureAwait(false);

					if (dispatched > 0)
					{
						LogPartitionDispatched(partitionId, dispatched);
						BackgroundServiceMetrics.RecordMessagesProcessed(
							BackgroundServiceTypes.Outbox,
							BackgroundServiceOperations.Pending,
							dispatched);
					}
					else
					{
						await Task.Delay(pollingInterval, stoppingToken).ConfigureAwait(false);
					}
				}
				catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
				{
					break;
				}
#pragma warning disable CA1031 // Partition processor must not crash on individual message failures
				catch (Exception ex)
#pragma warning restore CA1031
				{
					LogPartitionError(ex, partitionId);
					BackgroundServiceMetrics.RecordProcessingError(
						BackgroundServiceTypes.Outbox,
						ex.GetType().Name);
					await Task.Delay(errorBackoff, stoppingToken).ConfigureAwait(false);
				}
			}
		}
		finally
		{
			await scope.DisposeAsync().ConfigureAwait(false);
			LogPartitionStopped(partitionId);
		}
	}

	private async Task ProcessOutboxAsync(CancellationToken cancellationToken)
	{
		var stopwatch = ValueStopwatch.StartNew();
		var totalSuccess = 0;
		var totalFailure = 0;

		// Process pending messages
		var pendingResult = await _publisher.PublishPendingMessagesAsync(cancellationToken).ConfigureAwait(false);
		totalSuccess += pendingResult.SuccessCount;
		totalFailure += pendingResult.FailureCount;

		if (pendingResult.SuccessCount > 0 || pendingResult.FailureCount > 0)
		{
			LogProcessedPendingMessages(
					pendingResult.SuccessCount,
					pendingResult.FailureCount);

			BackgroundServiceMetrics.RecordMessagesProcessed(
				BackgroundServiceTypes.Outbox,
				BackgroundServiceOperations.Pending,
				pendingResult.SuccessCount);

			BackgroundServiceMetrics.RecordMessagesFailed(
				BackgroundServiceTypes.Outbox,
				BackgroundServiceOperations.Pending,
				pendingResult.FailureCount);
		}

		// Process scheduled messages if enabled
		if (_options.Value.ProcessScheduledMessages)
		{
			var scheduledResult = await _publisher.PublishScheduledMessagesAsync(cancellationToken).ConfigureAwait(false);
			totalSuccess += scheduledResult.SuccessCount;
			totalFailure += scheduledResult.FailureCount;

			if (scheduledResult.SuccessCount > 0 || scheduledResult.FailureCount > 0)
			{
				LogProcessedScheduledMessages(
						scheduledResult.SuccessCount,
						scheduledResult.FailureCount);

				BackgroundServiceMetrics.RecordMessagesProcessed(
					BackgroundServiceTypes.Outbox,
					BackgroundServiceOperations.Scheduled,
					scheduledResult.SuccessCount);

				BackgroundServiceMetrics.RecordMessagesFailed(
					BackgroundServiceTypes.Outbox,
					BackgroundServiceOperations.Scheduled,
					scheduledResult.FailureCount);
			}
		}

		// Retry failed messages if enabled
		if (_options.Value.RetryFailedMessages)
		{
			var retryResult = await _publisher.RetryFailedMessagesAsync(
				_options.Value.MaxRetries,
				cancellationToken).ConfigureAwait(false);
			totalSuccess += retryResult.SuccessCount;
			totalFailure += retryResult.FailureCount;

			if (retryResult.SuccessCount > 0 || retryResult.FailureCount > 0)
			{
				LogRetriedFailedMessages(
						retryResult.SuccessCount,
						retryResult.FailureCount);

				BackgroundServiceMetrics.RecordMessagesProcessed(
					BackgroundServiceTypes.Outbox,
					BackgroundServiceOperations.Retry,
					retryResult.SuccessCount);

				BackgroundServiceMetrics.RecordMessagesFailed(
					BackgroundServiceTypes.Outbox,
					BackgroundServiceOperations.Retry,
					retryResult.FailureCount);
			}
		}

		// Record cycle metrics
		var result = totalFailure > 0
			? (totalSuccess > 0 ? BackgroundServiceResults.Partial : BackgroundServiceResults.Error)
			: (totalSuccess > 0 ? BackgroundServiceResults.Success : BackgroundServiceResults.Empty);

		BackgroundServiceMetrics.RecordProcessingCycle(
			BackgroundServiceTypes.Outbox, result);

		BackgroundServiceMetrics.RecordProcessingDuration(
			BackgroundServiceTypes.Outbox, stopwatch.Elapsed.TotalMilliseconds);

		// Update health state
		_healthState?.RecordCycle(totalSuccess, totalFailure);
	}

	[LoggerMessage(OutboxEventId.OutboxBackgroundServiceDisabled, LogLevel.Information,
			"Outbox background service is disabled.")]
	private partial void LogBackgroundServiceDisabled();

	[LoggerMessage(OutboxEventId.OutboxBackgroundServiceStarting, LogLevel.Information,
			"Outbox background service starting with polling interval {PollingInterval}.")]
	private partial void LogBackgroundServiceStarting(TimeSpan pollingInterval);

	[LoggerMessage(OutboxEventId.OutboxBackgroundServiceError, LogLevel.Error,
			"Error processing outbox messages.")]
	private partial void LogBackgroundServiceError(Exception exception);

	[LoggerMessage(OutboxEventId.OutboxBackgroundServiceStopped, LogLevel.Information,
			"Outbox background service stopped.")]
	private partial void LogBackgroundServiceStopped();

	[LoggerMessage(OutboxEventId.OutboxBackgroundProcessedPending, LogLevel.Debug,
			"Processed pending messages: {SuccessCount} succeeded, {FailureCount} failed.")]
	private partial void LogProcessedPendingMessages(int successCount, int failureCount);

	[LoggerMessage(OutboxEventId.OutboxBackgroundProcessedScheduled, LogLevel.Debug,
			"Processed scheduled messages: {SuccessCount} succeeded, {FailureCount} failed.")]
	private partial void LogProcessedScheduledMessages(int successCount, int failureCount);

	[LoggerMessage(OutboxEventId.OutboxBackgroundRetriedFailed, LogLevel.Debug,
			"Retried failed messages: {SuccessCount} succeeded, {FailureCount} failed.")]
	private partial void LogRetriedFailedMessages(int successCount, int failureCount);

	[LoggerMessage(OutboxEventId.OutboxBackgroundServiceDrainTimeout, LogLevel.Warning,
			"Outbox background service drain timeout exceeded ({DrainTimeout}).")]
	private partial void LogDrainTimeoutExceeded(TimeSpan drainTimeout);

	[LoggerMessage(OutboxEventId.OutboxBackgroundSkippedNotLeader, LogLevel.Debug,
			"Skipped outbox processing cycle -- not the leader.")]
	private partial void LogSkippedNotLeader();

	[LoggerMessage(OutboxEventId.OutboxBackgroundServiceStarting + 10, LogLevel.Information,
			"Partitioned outbox processor starting: {PartitionCount} partitions")]
	private partial void LogPartitionedOutboxStarting(int partitionCount);

	[LoggerMessage(OutboxEventId.OutboxBackgroundServiceStarting + 11, LogLevel.Debug,
			"Partition {PartitionId} processor started")]
	private partial void LogPartitionStarted(int partitionId);

	[LoggerMessage(OutboxEventId.OutboxBackgroundServiceStarting + 12, LogLevel.Debug,
			"Partition {PartitionId} dispatched {MessageCount} messages")]
	private partial void LogPartitionDispatched(int partitionId, int messageCount);

	[LoggerMessage(OutboxEventId.OutboxBackgroundServiceStarting + 13, LogLevel.Error,
			"Error processing partition {PartitionId}")]
	private partial void LogPartitionError(Exception exception, int partitionId);

	[LoggerMessage(OutboxEventId.OutboxBackgroundServiceStarting + 14, LogLevel.Debug,
			"Partition {PartitionId} processor stopped")]
	private partial void LogPartitionStopped(int partitionId);
}
