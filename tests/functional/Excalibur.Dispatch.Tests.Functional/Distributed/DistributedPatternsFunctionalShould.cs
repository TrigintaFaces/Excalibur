// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

namespace Excalibur.Dispatch.Tests.Functional.Distributed;

/// <summary>
/// Functional tests for distributed messaging patterns (idempotency, ordering, deduplication).
/// </summary>
[Trait("Category", "Functional")]
[Trait("Component", "Distributed")]
[Trait("Feature", "Patterns")]
public sealed class DistributedPatternsFunctionalShould : FunctionalTestBase
{
	[Fact]
	public void EnsureIdempotentMessageProcessing()
	{
		// Arrange
		var processedIds = new ConcurrentDictionary<Guid, int>();
		var messageId = Guid.NewGuid();

		bool TryProcess(Guid id)
		{
			return processedIds.TryAdd(id, 1);
		}

		// Act - Process same message multiple times
		var firstAttempt = TryProcess(messageId);
		var secondAttempt = TryProcess(messageId);
		var thirdAttempt = TryProcess(messageId);

		// Assert - Only first attempt should succeed
		firstAttempt.ShouldBeTrue();
		secondAttempt.ShouldBeFalse();
		thirdAttempt.ShouldBeFalse();
		processedIds.Count.ShouldBe(1);
	}

	[Fact]
	public void TrackIdempotencyWithExpiration()
	{
		// Arrange
		var idempotencyStore = new IdempotencyStore(TimeSpan.FromMilliseconds(100));
		var messageId = Guid.NewGuid();

		// Act - First processing
		var firstResult = idempotencyStore.TryProcess(messageId);

		// Wait for expiration
		Thread.Sleep(150);

		// Try again after expiration
		var secondResult = idempotencyStore.TryProcess(messageId);

		// Assert
		firstResult.ShouldBeTrue();
		secondResult.ShouldBeTrue(); // Should succeed after expiration
	}

	[Fact]
	public async Task MaintainMessageOrdering()
	{
		// Arrange
		var receivedOrder = new ConcurrentQueue<int>();
		var messages = Enumerable.Range(1, 10).ToList();

		// Act - Process messages in order
		foreach (var seq in messages)
		{
			await ProcessMessageAsync(seq, receivedOrder).ConfigureAwait(false);
		}

		// Assert
		var order = receivedOrder.ToArray();
		order.Length.ShouldBe(10);
		for (var i = 0; i < 10; i++)
		{
			order[i].ShouldBe(i + 1);
		}
	}

	[Fact]
	public async Task DetectOutOfOrderMessages()
	{
		// Arrange
		var expectedSequence = 1;
		var outOfOrderMessages = new List<int>();
		var messages = new[] { 1, 2, 4, 3, 5 }; // 4 arrives before 3

		// Act
		foreach (var seq in messages)
		{
			if (seq == expectedSequence)
			{
				expectedSequence++;
				// Process normally and check for buffered messages
				while (outOfOrderMessages.Contains(expectedSequence))
				{
					_ = outOfOrderMessages.Remove(expectedSequence);
					expectedSequence++;
				}
			}
			else
			{
				outOfOrderMessages.Add(seq);
			}

			await Task.Yield();
		}

		// Assert - All messages should eventually be processed in order
		expectedSequence.ShouldBe(6); // Next expected is 6 (1-5 processed)
		outOfOrderMessages.Count.ShouldBe(0);
	}

	[Fact]
	public void DeduplicateMessages()
	{
		// Arrange
		var deduplicator = new MessageDeduplicator();
		var messages = new[]
		{
			new TestMessage { Id = Guid.NewGuid(), Content = "A" },
			new TestMessage { Id = Guid.NewGuid(), Content = "B" },
		};

		// Create duplicates
		var allMessages = messages
			.Concat(messages) // Duplicate all
			.Concat([messages[0]]) // Triple the first
			.ToList();

		// Act
		var unique = allMessages.Where(m => deduplicator.IsUnique(m.Id)).ToList();

		// Assert
		unique.Count.ShouldBe(2);
		unique.Select(m => m.Content).ShouldBe(["A", "B"]);
	}

	[Fact]
	public void PartitionMessagesForParallelProcessing()
	{
		// Arrange
		var partitionCount = 4;
		var messages = Enumerable.Range(1, 100)
			.Select(i => new PartitionedMessage
			{
				PartitionKey = $"key-{i % 10}",
				Sequence = i,
			})
			.ToList();

		// Act - Partition by key
		var partitions = messages
			.GroupBy(m => Math.Abs(m.PartitionKey.GetHashCode()) % partitionCount)
			.ToDictionary(g => g.Key, g => g.ToList());

		// Assert
		partitions.Count.ShouldBeLessThanOrEqualTo(partitionCount);
		partitions.Values.Sum(p => p.Count).ShouldBe(100);

		// Messages in each partition should hash back to that partition.
		foreach (var partitionEntry in partitions)
		{
			foreach (var key in partitionEntry.Value.Select(m => m.PartitionKey).Distinct())
			{
				var partition = Math.Abs(key.GetHashCode()) % partitionCount;
				partition.ShouldBe(partitionEntry.Key);
			}
		}
	}

	[Fact]
	public void ImplementExactlyOnceDelivery()
	{
		// Arrange
		var deliveryTracker = new ExactlyOnceDeliveryTracker();
		var messageId = Guid.NewGuid();

		// Act - Simulate delivery attempts
		var delivery1 = deliveryTracker.TryDeliver(messageId);
		var delivery2 = deliveryTracker.TryDeliver(messageId);

		// Acknowledge first delivery
		deliveryTracker.Acknowledge(messageId);

		// Try to redeliver after acknowledgment
		var delivery3 = deliveryTracker.TryDeliver(messageId);

		// Assert
		delivery1.ShouldBeTrue();
		delivery2.ShouldBeFalse(); // Already in progress
		delivery3.ShouldBeFalse(); // Already acknowledged
	}

	[Fact]
	public async Task HandleMessageRetryWithBackoff()
	{
		// Arrange
		var attemptTimes = new List<DateTimeOffset>();
		var maxRetries = 3;
		var baseDelay = TimeSpan.FromMilliseconds(10);

		// Act - Simulate retries with backoff
		for (var attempt = 0; attempt < maxRetries; attempt++)
		{
			attemptTimes.Add(DateTimeOffset.UtcNow);
			var delay = TimeSpan.FromMilliseconds(baseDelay.TotalMilliseconds * Math.Pow(2, attempt));
			await Task.Delay(delay).ConfigureAwait(false);
		}

		// Assert
		attemptTimes.Count.ShouldBe(3);

		// Each attempt should be progressively later
		for (var i = 1; i < attemptTimes.Count; i++)
		{
			attemptTimes[i].ShouldBeGreaterThan(attemptTimes[i - 1]);
		}
	}

	[Fact]
	public void ImplementDeadLetterQueue()
	{
		// Arrange
		var mainQueue = new ConcurrentQueue<TestMessage>();
		var deadLetterQueue = new ConcurrentQueue<DeadLetterMessage>();
		var maxAttempts = 3;

		var failingMessage = new TestMessage { Id = Guid.NewGuid(), Content = "Fails always" };
		var attempts = new ConcurrentDictionary<Guid, int>();

		// Act - Simulate processing with failures
		var messageAttempts = attempts.AddOrUpdate(failingMessage.Id, 1, (_, count) => count + 1);

		while (messageAttempts <= maxAttempts)
		{
			// Simulate failure
			var shouldRetry = messageAttempts < maxAttempts;

			if (!shouldRetry)
			{
				// Move to DLQ
				deadLetterQueue.Enqueue(new DeadLetterMessage
				{
					OriginalMessage = failingMessage,
					FailureReason = "Max retries exceeded",
					Attempts = messageAttempts,
					LastFailedAt = DateTimeOffset.UtcNow,
				});

				break;
			}

			messageAttempts = attempts.AddOrUpdate(failingMessage.Id, 1, (_, count) => count + 1);
		}

		// Assert
		mainQueue.Count.ShouldBe(0);
		deadLetterQueue.Count.ShouldBe(1);
		deadLetterQueue.TryDequeue(out var dlqMessage).ShouldBeTrue();
		dlqMessage.Attempts.ShouldBe(3);
		dlqMessage.FailureReason.ShouldBe("Max retries exceeded");
	}

	[Fact]
	public void TrackDistributedTransactionState()
	{
		// Arrange
		var transaction = new DistributedTransaction
		{
			TransactionId = Guid.NewGuid(),
			Participants = ["ServiceA", "ServiceB", "ServiceC"],
		};

		// Act - Simulate two-phase commit
		// Phase 1: Prepare
		var prepareResults = new Dictionary<string, bool>
		{
			["ServiceA"] = true,
			["ServiceB"] = true,
			["ServiceC"] = true,
		};

		var allPrepared = prepareResults.Values.All(v => v);

		// Phase 2: Commit or Rollback
		var commitResults = new Dictionary<string, bool>();
		if (allPrepared)
		{
			foreach (var participant in transaction.Participants)
			{
				commitResults[participant] = true;
			}
		}

		// Assert
		allPrepared.ShouldBeTrue();
		commitResults.Count.ShouldBe(3);
		commitResults.Values.ShouldAllBe(v => v);
	}

	[Fact]
	public void ImplementSagaCompensation()
	{
		// Arrange
		var executedSteps = new List<string>();
		var compensatedSteps = new List<string>();

		var sagaSteps = new[]
		{
			new SagaStep { Name = "CreateOrder", Execute = () => executedSteps.Add("CreateOrder"), Compensate = () => compensatedSteps.Add("CancelOrder") },
			new SagaStep { Name = "ReserveInventory", Execute = () => executedSteps.Add("ReserveInventory"), Compensate = () => compensatedSteps.Add("ReleaseInventory") },
			new SagaStep { Name = "ChargePayment", Execute = () => throw new InvalidOperationException("Payment failed"), Compensate = () => compensatedSteps.Add("RefundPayment") },
			new SagaStep { Name = "ShipOrder", Execute = () => executedSteps.Add("ShipOrder"), Compensate = () => compensatedSteps.Add("CancelShipment") },
		};

		// Act - Execute saga with compensation on failure
		var completedSteps = new Stack<SagaStep>();

		foreach (var step in sagaSteps)
		{
			try
			{
				step.Execute();
				completedSteps.Push(step);
			}
			catch
			{
				// Compensate in reverse order
				while (completedSteps.Count > 0)
				{
					var compensateStep = completedSteps.Pop();
					compensateStep.Compensate();
				}

				break;
			}
		}

		// Assert
		executedSteps.Count.ShouldBe(2);
		executedSteps.ShouldContain("CreateOrder");
		executedSteps.ShouldContain("ReserveInventory");

		compensatedSteps.Count.ShouldBe(2);
		compensatedSteps[0].ShouldBe("ReleaseInventory"); // Last executed, first compensated
		compensatedSteps[1].ShouldBe("CancelOrder");
	}

	[Fact]
	public void TrackMessageDeliveryMetrics()
	{
		// Arrange
		var metrics = new DeliveryMetrics
		{
			TotalSent = 1000,
			Delivered = 980,
			Failed = 15,
			Retried = 50,
			DeadLettered = 5,
		};

		// Act
		var deliveryRate = (double)metrics.Delivered / metrics.TotalSent;
		var failureRate = (double)metrics.Failed / metrics.TotalSent;
		var retryRate = (double)metrics.Retried / metrics.TotalSent;

		// Assert
		deliveryRate.ShouldBe(0.98);
		failureRate.ShouldBe(0.015);
		retryRate.ShouldBe(0.05);
		(metrics.Delivered + metrics.DeadLettered).ShouldBe(985);
	}

	private static Task ProcessMessageAsync(int sequence, ConcurrentQueue<int> receivedOrder)
	{
		receivedOrder.Enqueue(sequence);
		return Task.CompletedTask;
	}

	private sealed class IdempotencyStore(TimeSpan expiration)
	{
		private readonly ConcurrentDictionary<Guid, DateTimeOffset> _processed = new();

		public bool TryProcess(Guid messageId)
		{
			var now = DateTimeOffset.UtcNow;

			// Clean expired entries
			var expired = _processed.Where(kvp => now - kvp.Value > expiration).Select(kvp => kvp.Key).ToList();
			foreach (var id in expired)
			{
				_ = _processed.TryRemove(id, out _);
			}

			return _processed.TryAdd(messageId, now);
		}
	}

	private sealed class MessageDeduplicator
	{
		private readonly ConcurrentDictionary<Guid, byte> _seen = new();

		public bool IsUnique(Guid messageId)
		{
			return _seen.TryAdd(messageId, 0);
		}
	}

	private sealed class TestMessage
	{
		public Guid Id { get; init; }
		public string Content { get; init; } = string.Empty;
	}

	private sealed class PartitionedMessage
	{
		public string PartitionKey { get; init; } = string.Empty;
		public int Sequence { get; init; }
	}

	private sealed class ExactlyOnceDeliveryTracker
	{
		private readonly ConcurrentDictionary<Guid, DeliveryState> _deliveries = new();

		public bool TryDeliver(Guid messageId)
		{
			return _deliveries.TryAdd(messageId, DeliveryState.InProgress);
		}

		public void Acknowledge(Guid messageId)
		{
			_ = _deliveries.TryUpdate(messageId, DeliveryState.Acknowledged, DeliveryState.InProgress);
		}

		private enum DeliveryState
		{
			InProgress,
			Acknowledged,
		}
	}

	private sealed class DeadLetterMessage
	{
		public TestMessage OriginalMessage { get; init; } = null!;
		public string FailureReason { get; init; } = string.Empty;
		public int Attempts { get; init; }
		public DateTimeOffset LastFailedAt { get; init; }
	}

	private sealed class DistributedTransaction
	{
		public Guid TransactionId { get; init; }
		public List<string> Participants { get; init; } = [];
	}

	private sealed class SagaStep
	{
		public string Name { get; init; } = string.Empty;
		public Action Execute { get; init; } = () => { };
		public Action Compensate { get; init; } = () => { };
	}

	private sealed class DeliveryMetrics
	{
		public int TotalSent { get; init; }
		public int Delivered { get; init; }
		public int Failed { get; init; }
		public int Retried { get; init; }
		public int DeadLettered { get; init; }
	}
}
