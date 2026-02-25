// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Options.Delivery;

namespace Excalibur.Dispatch.Benchmarks.Patterns;

/// <summary>
/// Benchmarks for OutboxDeliveryGuarantee levels per AD-222-5.
/// </summary>
/// <remarks>
/// Sprint 222 Task 5q19 - Benchmark performance of each delivery guarantee level.
///
/// Expected Results:
/// - AtLeastOnce: Highest throughput (batch operations reduce database round-trips)
/// - MinimizedWindow: Lower throughput (one round-trip per message)
/// - TransactionalWhenApplicable: Variable (depends on transaction overhead)
/// </remarks>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.HostProcess)]
public class DeliveryGuaranteeBenchmarks
{
	private BenchmarkOutboxStore _store = null!;
	private OutboundMessage[] _messages = null!;
	private byte[] _testPayload = null!;

	[Params(OutboxDeliveryGuarantee.AtLeastOnce,
			OutboxDeliveryGuarantee.MinimizedWindow,
			OutboxDeliveryGuarantee.TransactionalWhenApplicable)]
	public OutboxDeliveryGuarantee GuaranteeLevel { get; set; }

	[Params(10, 100)]
	public int BatchSize { get; set; }

	[GlobalSetup]
	public void Setup()
	{
		_store = new BenchmarkOutboxStore(
			supportsTransactions: GuaranteeLevel == OutboxDeliveryGuarantee.TransactionalWhenApplicable);

		_testPayload = new byte[256];
		Random.Shared.NextBytes(_testPayload);

		_messages = new OutboundMessage[BatchSize];
		for (var i = 0; i < BatchSize; i++)
		{
			_messages[i] = CreateMessage($"msg-{i}");
		}
	}

	[IterationSetup]
	public void IterationSetup()
	{
		// Reset store state for each iteration
		_store.Reset();

		// Re-stage all messages
		foreach (var msg in _messages)
		{
			msg.Status = OutboxStatus.Staged;
			_store.StageMessageSync(msg);
		}
	}

	/// <summary>
	/// Benchmark: Process batch with configured delivery guarantee.
	/// </summary>
	[Benchmark(Baseline = true)]
	public async Task<int> ProcessBatch()
	{
		var messageIds = _messages.Select(m => m.Id).ToList();

		return GuaranteeLevel switch
		{
			OutboxDeliveryGuarantee.AtLeastOnce => await ProcessAtLeastOnceAsync(messageIds),
			OutboxDeliveryGuarantee.MinimizedWindow => await ProcessMinimizedWindowAsync(messageIds),
			OutboxDeliveryGuarantee.TransactionalWhenApplicable => await ProcessTransactionalAsync(messageIds),
			_ => throw new ArgumentOutOfRangeException()
		};
	}

	/// <summary>
	/// Benchmark: Throughput measurement (messages/second).
	/// </summary>
	[Benchmark]
	public async Task<int> ThroughputMeasurement()
	{
		var total = 0;

		// Process 3 batches to measure sustained throughput
		for (var batch = 0; batch < 3; batch++)
		{
			var messageIds = _messages.Select(m => m.Id).ToList();

			total += GuaranteeLevel switch
			{
				OutboxDeliveryGuarantee.AtLeastOnce => await ProcessAtLeastOnceAsync(messageIds),
				OutboxDeliveryGuarantee.MinimizedWindow => await ProcessMinimizedWindowAsync(messageIds),
				OutboxDeliveryGuarantee.TransactionalWhenApplicable => await ProcessTransactionalAsync(messageIds),
				_ => 0
			};

			// Reset for next batch
			foreach (var msg in _messages)
			{
				msg.Status = OutboxStatus.Staged;
			}

			_store.ResetSentStatus();
		}

		return total;
	}

	/// <summary>
	/// Benchmark: Failure recovery overhead.
	/// </summary>
	[Benchmark]
	public async Task<int> FailureRecoveryOverhead()
	{
		var messageIds = _messages.Select(m => m.Id).ToList();

		// Simulate partial failure (half messages succeed)
		var halfCount = messageIds.Count / 2;
		var succeededIds = messageIds.Take(halfCount).ToList();
		var failedIds = messageIds.Skip(halfCount).ToList();

		// Mark succeeded
		foreach (var id in succeededIds)
		{
			await _store.MarkSentAsync(id, CancellationToken.None);
		}

		// Mark failed (retry eligible)
		foreach (var id in failedIds)
		{
			await _store.MarkFailedAsync(id, "Simulated failure", 1, CancellationToken.None);
		}

		return succeededIds.Count;
	}

	/// <summary>
	/// AtLeastOnce: Batch completion after all messages dispatched.
	/// </summary>
	private async Task<int> ProcessAtLeastOnceAsync(List<string> messageIds)
	{
		// Simulate dispatch (in real processor, messages are published here)
		// Then batch mark all as sent
		foreach (var id in messageIds)
		{
			await _store.MarkSentAsync(id, CancellationToken.None);
		}

		return messageIds.Count;
	}

	/// <summary>
	/// MinimizedWindow: Individual completion per message.
	/// </summary>
	private async Task<int> ProcessMinimizedWindowAsync(List<string> messageIds)
	{
		var count = 0;

		foreach (var id in messageIds)
		{
			// Simulate dispatch, then immediately mark sent
			await _store.MarkSentAsync(id, CancellationToken.None);
			count++;
		}

		return count;
	}

	/// <summary>
	/// TransactionalWhenApplicable: Transactional batch completion.
	/// </summary>
	private async Task<int> ProcessTransactionalAsync(List<string> messageIds)
	{
		if (_store is ITransactionalOutboxStore txStore && txStore.SupportsTransactions)
		{
			await txStore.MarkSentTransactionalAsync(messageIds, CancellationToken.None);
		}
		else
		{
			// Fallback to individual
			foreach (var id in messageIds)
			{
				await _store.MarkSentAsync(id, CancellationToken.None);
			}
		}

		return messageIds.Count;
	}

	private OutboundMessage CreateMessage(string id)
	{
		return new OutboundMessage
		{
			Id = id,
			MessageType = "BenchmarkMessage",
			Payload = _testPayload,
			Destination = "benchmark-queue",
			CorrelationId = Guid.NewGuid().ToString(),
			CreatedAt = DateTimeOffset.UtcNow,
			Status = OutboxStatus.Staged
		};
	}

	/// <summary>
	/// In-memory outbox store for benchmarking.
	/// </summary>
	private sealed class BenchmarkOutboxStore : ITransactionalOutboxStore
	{
		private readonly Dictionary<string, OutboundMessage> _messages = new();
		private readonly bool _supportsTransactions;

		public BenchmarkOutboxStore(bool supportsTransactions)
		{
			_supportsTransactions = supportsTransactions;
		}

		public bool SupportsTransactions => _supportsTransactions;

		public void StageMessageSync(OutboundMessage message)
		{
			_messages[message.Id] = message;
		}

		public void Reset()
		{
			_messages.Clear();
		}

		public void ResetSentStatus()
		{
			foreach (var msg in _messages.Values)
			{
				msg.Status = OutboxStatus.Staged;
			}
		}

		public ValueTask StageMessageAsync(OutboundMessage message, CancellationToken cancellationToken)
		{
			_messages[message.Id] = message;
			return default;
		}

		public ValueTask MarkSentAsync(string messageId, CancellationToken cancellationToken)
		{
			if (_messages.TryGetValue(messageId, out var msg))
			{
				msg.Status = OutboxStatus.Sent;
			}

			return default;
		}

		public Task MarkSentTransactionalAsync(IReadOnlyList<string> messageIds, CancellationToken cancellationToken)
		{
			// Simulate transactional batch mark (slightly more overhead)
			foreach (var id in messageIds)
			{
				if (_messages.TryGetValue(id, out var msg))
				{
					msg.Status = OutboxStatus.Sent;
				}
			}

			return Task.CompletedTask;
		}

		public ValueTask MarkFailedAsync(string messageId, string errorMessage, int retryCount, CancellationToken cancellationToken)
		{
			if (_messages.TryGetValue(messageId, out var msg))
			{
				msg.Status = OutboxStatus.Failed;
				msg.RetryCount = retryCount;
			}

			return default;
		}

		// Unused interface members
		public ValueTask EnqueueAsync(IDispatchMessage message, IMessageContext context, CancellationToken cancellationToken) =>
			throw new NotImplementedException();

		public ValueTask<IEnumerable<OutboundMessage>> GetUnsentMessagesAsync(int batchSize, CancellationToken cancellationToken) =>
			new ValueTask<IEnumerable<OutboundMessage>>(_messages.Values.Where(m => m.Status == OutboxStatus.Staged).Take(batchSize));

		public ValueTask<IEnumerable<OutboundMessage>> GetFailedMessagesAsync(int maxRetries, DateTimeOffset? olderThan, int batchSize, CancellationToken cancellationToken) =>
			new ValueTask<IEnumerable<OutboundMessage>>(_messages.Values.Where(m => m.Status == OutboxStatus.Failed && m.RetryCount < maxRetries).Take(batchSize));

		public ValueTask<IEnumerable<OutboundMessage>> GetScheduledMessagesAsync(DateTimeOffset scheduledBefore, int batchSize, CancellationToken cancellationToken) =>
			new ValueTask<IEnumerable<OutboundMessage>>(Enumerable.Empty<OutboundMessage>());

		public ValueTask<int> CleanupSentMessagesAsync(DateTimeOffset olderThan, int batchSize, CancellationToken cancellationToken) =>
			new ValueTask<int>(0);

		public ValueTask<OutboxStatistics> GetStatisticsAsync(CancellationToken cancellationToken) =>
			new ValueTask<OutboxStatistics>(new OutboxStatistics());
	}
}
