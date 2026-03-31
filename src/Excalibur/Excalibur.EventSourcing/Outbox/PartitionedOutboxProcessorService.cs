// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.Metrics;
using Excalibur.Dispatch.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.Outbox;

/// <summary>
/// Background service that runs one outbox processor per partition.
/// Each partition processes independently for maximum throughput.
/// </summary>
internal sealed class PartitionedOutboxProcessorService : BackgroundService
{
	private static readonly Meter Meter = new("Excalibur.EventSourcing.Outbox.Partitioned", "1.0.0");

	private static readonly Counter<long> MessagesProcessed = Meter.CreateCounter<long>(
		"excalibur.eventsourcing.outbox.partition.messages_processed",
		description: "Messages processed per partition");

	private static readonly Counter<long> DlqMessages = Meter.CreateCounter<long>(
		"excalibur.eventsourcing.outbox.partition.dlq_messages",
		description: "Messages sent to per-partition DLQ");

	private readonly IOutboxPartitioner _partitioner;
	private readonly OutboxPartitionOptions _options;
	private readonly IServiceProvider _serviceProvider;
	private readonly ILogger<PartitionedOutboxProcessorService> _logger;

	internal PartitionedOutboxProcessorService(
		IOutboxPartitioner partitioner,
		IOptions<OutboxPartitionOptions> options,
		IServiceProvider serviceProvider,
		ILogger<PartitionedOutboxProcessorService> logger)
	{
		ArgumentNullException.ThrowIfNull(partitioner);
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(serviceProvider);
		ArgumentNullException.ThrowIfNull(logger);

		_partitioner = partitioner;
		_options = options.Value;
		_serviceProvider = serviceProvider;
		_logger = logger;
	}

	/// <inheritdoc />
	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		_logger.PartitionedOutboxStarting(_partitioner.PartitionCount, _options.ProcessorCountPerPartition);

		// Start one task per partition
		var tasks = new Task[_partitioner.PartitionCount];
		for (var i = 0; i < _partitioner.PartitionCount; i++)
		{
			var partitionId = i;
			tasks[i] = ProcessPartitionAsync(partitionId, stoppingToken);
		}

		await Task.WhenAll(tasks).ConfigureAwait(false);

		_logger.PartitionedOutboxStopped();
	}

	private async Task ProcessPartitionAsync(int partitionId, CancellationToken stoppingToken)
	{
		_logger.PartitionProcessorStarted(partitionId);

		// Create a scoped IOutboxProcessor per partition so each partition
		// processes its own subset of messages independently.
		var scope = _serviceProvider.CreateAsyncScope();
		try
		{
			var processor = scope.ServiceProvider.GetRequiredService<IOutboxProcessor>();
			processor.Init($"partitioned-{partitionId}");

			while (!stoppingToken.IsCancellationRequested)
			{
				try
				{
					var dispatched = await processor.DispatchPendingMessagesAsync(stoppingToken).ConfigureAwait(false);

					if (dispatched > 0)
					{
						MessagesProcessed.Add(dispatched, new KeyValuePair<string, object?>("partition", partitionId));
						_logger.PartitionDispatched(partitionId, dispatched);
					}
					else
					{
						// No messages available -- back off before next poll
						await Task.Delay(_options.PollingInterval, stoppingToken).ConfigureAwait(false);
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
					_logger.PartitionProcessingError(ex, partitionId);
					DlqMessages.Add(1, new KeyValuePair<string, object?>("partition", partitionId));
					await Task.Delay(_options.ErrorBackoffInterval, stoppingToken).ConfigureAwait(false);
				}
			}
		}
		finally
		{
			await scope.DisposeAsync().ConfigureAwait(false);
		}

		_logger.PartitionProcessorStopped(partitionId);
	}
}
