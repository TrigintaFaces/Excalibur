// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.ErrorHandling;
using Excalibur.Dispatch.Resilience;
using Excalibur.Dispatch.Options.Resilience;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Tests.Messaging.Resilience;

/// <summary>
///     Integration tests for resilience components verifying the interaction of
///     Dead Letter Queue, Circuit Breaker, and Exponential Backoff components.
/// </summary>
[Trait("Category", "Unit")]
public sealed class ResilienceIntegrationShould
{
	private readonly ILogger<InMemoryDeadLetterQueue> _dlqLogger;
	private readonly ILogger<CircuitBreakerPolicy> _cbLogger;

	public ResilienceIntegrationShould()
	{
		_dlqLogger = NullLoggerFactory.Instance.CreateLogger<InMemoryDeadLetterQueue>();
		_cbLogger = NullLoggerFactory.Instance.CreateLogger<CircuitBreakerPolicy>();
	}

	#region Integration Scenario: Circuit Breaker Opens and Message Goes to DLQ

	[Fact]
	public async Task RouteMessageToDlqWhenCircuitBreakerOpens()
	{
		// Arrange - Setup DLQ and Circuit Breaker
		var dlq = new InMemoryDeadLetterQueue(_dlqLogger);
		var options = new CircuitBreakerOptions { FailureThreshold = 2 };
		var circuitBreaker = new CircuitBreakerPolicy(options, "order-service", _cbLogger);

		// Act - Simulate failures until circuit opens
		circuitBreaker.RecordFailure(new TimeoutException("Service timeout"));
		circuitBreaker.RecordFailure(new TimeoutException("Service timeout"));
		circuitBreaker.State.ShouldBe(CircuitState.Open);

		// Try to process a message when circuit is open
		var message = new TestOrderMessage { OrderId = Guid.NewGuid(), Amount = 99.99m };
		Guid? dlqEntryId = null;

		try
		{
			await circuitBreaker.ExecuteAsync(async ct =>
			{
				await Task.Delay(1, ct).ConfigureAwait(false);
				return "processed";
			}).ConfigureAwait(false);
		}
		catch (CircuitBreakerOpenException ex)
		{
			// Route to DLQ
			dlqEntryId = await dlq.EnqueueAsync(
				message,
				DeadLetterReason.CircuitBreakerOpen,
				ex,
				new Dictionary<string, string>
				{
					["circuit_name"] = ex.CircuitName ?? "unknown",
					["retry_after_seconds"] = ex.RetryAfter?.TotalSeconds.ToString() ?? "unknown",
				}).ConfigureAwait(false);
		}

		// Assert
		dlqEntryId.ShouldNotBeNull();
		var entry = await dlq.GetEntryAsync(dlqEntryId.Value).ConfigureAwait(false);
		entry.ShouldNotBeNull();
		entry.Reason.ShouldBe(DeadLetterReason.CircuitBreakerOpen);
		entry.Metadata.ShouldNotBeNull();
		entry.Metadata["circuit_name"].ShouldBe("order-service");
	}

	#endregion Integration Scenario: Circuit Breaker Opens and Message Goes to DLQ

	#region Integration Scenario: Retry with Backoff then DLQ

	[Fact]
	public async Task RetryWithExponentialBackoffThenDlq()
	{
		// Arrange
		var dlq = new InMemoryDeadLetterQueue(_dlqLogger);
		var backoffCalculator = new ExponentialBackoffCalculator(
			baseDelay: TimeSpan.FromMilliseconds(10),
			maxDelay: TimeSpan.FromSeconds(1),
			multiplier: 2.0,
			enableJitter: false);

		var message = new TestPaymentMessage { PaymentId = Guid.NewGuid() };
		const int maxAttempts = 3;
		var attemptCount = 0;
		var delays = new List<TimeSpan>();
		var stopwatch = Stopwatch.StartNew();
		Exception? lastException = null;

		// Act - Simulate retry loop with backoff
		for (var attempt = 1; attempt <= maxAttempts; attempt++)
		{
			if (attempt > 1)
			{
				var delay = backoffCalculator.CalculateDelay(attempt - 1);
				delays.Add(delay);
				await Task.Delay(delay).ConfigureAwait(false);
			}

			try
			{
				attemptCount++;
				// Simulate persistent failure
				throw new InvalidOperationException($"Processing failed on attempt {attempt}");
			}
			catch (InvalidOperationException ex)
			{
				lastException = ex;
			}
		}

		// After all retries exhausted, send to DLQ
		var entryId = await dlq.EnqueueAsync(
			message,
			DeadLetterReason.MaxRetriesExceeded,
			lastException,
			new Dictionary<string, string>
			{
				["total_attempts"] = attemptCount.ToString(),
				["total_delay_ms"] = delays.Sum(d => d.TotalMilliseconds).ToString(),
			}).ConfigureAwait(false);

		stopwatch.Stop();

		// Assert
		attemptCount.ShouldBe(3);
		delays.Count.ShouldBe(2); // 2 delays (before attempt 2 and 3)
		delays[0].TotalMilliseconds.ShouldBe(10); // 10ms * 2^0
		delays[1].TotalMilliseconds.ShouldBe(20); // 10ms * 2^1

		var entry = await dlq.GetEntryAsync(entryId).ConfigureAwait(false);
		entry.ShouldNotBeNull();
		entry.Reason.ShouldBe(DeadLetterReason.MaxRetriesExceeded);
		entry.Metadata!["total_attempts"].ShouldBe("3");
	}

	#endregion Integration Scenario: Retry with Backoff then DLQ

	#region Integration Scenario: Per-Transport Circuit Breaker Isolation with DLQ

	[Fact]
	public async Task IsolateTransportFailuresWithPerTransportCircuitBreakers()
	{
		// Arrange
		var dlq = new InMemoryDeadLetterQueue(_dlqLogger);
		var options = new CircuitBreakerOptions { FailureThreshold = 2 };
		var registry = new TransportCircuitBreakerRegistry(options, NullLoggerFactory.Instance);

		// Act - Open RabbitMQ circuit, keep Kafka closed
		var rabbitBreaker = registry.GetOrCreate("rabbitmq");
		rabbitBreaker.RecordFailure(new TimeoutException("RabbitMQ timeout"));
		rabbitBreaker.RecordFailure(new TimeoutException("RabbitMQ timeout"));

		var kafkaBreaker = registry.GetOrCreate("kafka");
		kafkaBreaker.RecordSuccess(); // Kafka is healthy

		// Verify isolation
		rabbitBreaker.State.ShouldBe(CircuitState.Open);
		kafkaBreaker.State.ShouldBe(CircuitState.Closed);

		// Route RabbitMQ message to DLQ
		var rabbitMessage = new TestEventMessage { EventId = Guid.NewGuid() };
		var entryId = await dlq.EnqueueAsync(
			rabbitMessage,
			DeadLetterReason.CircuitBreakerOpen,
			metadata: new Dictionary<string, string>
			{
				["transport"] = "rabbitmq",
			}).ConfigureAwait(false);

		// Kafka message processes successfully (simulated)
		var kafkaProcessed = false;
		await kafkaBreaker.ExecuteAsync(async ct =>
		{
			kafkaProcessed = true;
			await Task.Delay(1, ct).ConfigureAwait(false);
			return true;
		}).ConfigureAwait(false);

		// Assert
		kafkaProcessed.ShouldBeTrue();
		var entry = await dlq.GetEntryAsync(entryId).ConfigureAwait(false);
		entry.ShouldNotBeNull();
		entry.Metadata!["transport"].ShouldBe("rabbitmq");

		var states = registry.GetAllStates();
		states["rabbitmq"].ShouldBe(CircuitState.Open);
		states["kafka"].ShouldBe(CircuitState.Closed);
	}

	#endregion Integration Scenario: Per-Transport Circuit Breaker Isolation with DLQ

	#region Integration Scenario: Poison Message Detection and DLQ

	[Fact]
	public async Task DetectPoisonMessageAndRouteToDlq()
	{
		// Arrange
		var dlq = new InMemoryDeadLetterQueue(_dlqLogger);
		var backoffCalculator = ExponentialBackoffCalculator.CreateForTransientFailures();
		var maxAttempts = 3;

		// Simulate a poison message that always fails with the same error
		var poisonMessage = new TestPoisonMessage { CorruptData = "Invalid JSON: {broken}" };
		var attemptCount = 0;
		var sameExceptionEveryTime = true;
		string? lastExceptionType = null;

		// Act - Try to process with retries
		for (var attempt = 1; attempt <= maxAttempts; attempt++)
		{
			if (attempt > 1)
			{
				await Task.Delay(backoffCalculator.CalculateDelay(attempt - 1)).ConfigureAwait(false);
			}

			try
			{
				attemptCount++;
				// Always fails with same exception - this is a poison message
				throw new FormatException("Cannot deserialize message");
			}
			catch (FormatException ex)
			{
				if (lastExceptionType != null && lastExceptionType != ex.GetType().Name)
				{
					sameExceptionEveryTime = false;
				}

				lastExceptionType = ex.GetType().Name;
			}
		}

		// Detect it's a poison message (same exception every time)
		var reason = sameExceptionEveryTime
			? DeadLetterReason.PoisonMessage
			: DeadLetterReason.MaxRetriesExceeded;

		var entryId = await dlq.EnqueueAsync(
			poisonMessage,
			reason,
			new FormatException("Cannot deserialize message"),
			new Dictionary<string, string>
			{
				["detection_method"] = "consistent_exception_type",
				["exception_type"] = lastExceptionType!,
			}).ConfigureAwait(false);

		// Assert
		var entry = await dlq.GetEntryAsync(entryId).ConfigureAwait(false);
		entry.ShouldNotBeNull();
		entry.Reason.ShouldBe(DeadLetterReason.PoisonMessage);
		entry.Metadata!["detection_method"].ShouldBe("consistent_exception_type");
	}

	#endregion Integration Scenario: Poison Message Detection and DLQ

	#region Integration Scenario: Circuit Recovery and DLQ Replay

	[Fact]
	public async Task RecoverCircuitAndReplayDlqMessages()
	{
		// Arrange
		var replayedMessages = new List<object>();
		var dlq = new InMemoryDeadLetterQueue(_dlqLogger, msg =>
		{
			replayedMessages.Add(msg);
			return Task.CompletedTask;
		});

		var options = new CircuitBreakerOptions
		{
			FailureThreshold = 1,
			OpenDuration = TimeSpan.FromMilliseconds(50),
			SuccessThreshold = 1,
		};
		var circuitBreaker = new CircuitBreakerPolicy(options, "payment-service", _cbLogger);

		// Act - Open circuit and queue message
		circuitBreaker.RecordFailure(new TimeoutException("Service down"));
		circuitBreaker.State.ShouldBe(CircuitState.Open);

		var message = new TestPaymentMessage { PaymentId = Guid.NewGuid() };
		var entryId = await dlq.EnqueueAsync(
			message,
			DeadLetterReason.CircuitBreakerOpen).ConfigureAwait(false);

		// Wait for circuit to transition to half-open
		await Task.Delay(100).ConfigureAwait(false);
		circuitBreaker.State.ShouldBe(CircuitState.HalfOpen);

		// Record success to close circuit
		circuitBreaker.RecordSuccess();
		circuitBreaker.State.ShouldBe(CircuitState.Closed);

		// Replay DLQ messages now that circuit is closed
		var filter = DeadLetterQueryFilter.ByReason(DeadLetterReason.CircuitBreakerOpen);
		var replayCount = await dlq.ReplayBatchAsync(filter).ConfigureAwait(false);

		// Assert
		replayCount.ShouldBe(1);
		replayedMessages.Count.ShouldBe(1);

		var entry = await dlq.GetEntryAsync(entryId).ConfigureAwait(false);
		entry!.IsReplayed.ShouldBeTrue();
		entry.ReplayedAt.ShouldNotBeNull();
	}

	#endregion Integration Scenario: Circuit Recovery and DLQ Replay

	#region Integration Scenario: Multiple Failure Reasons in DLQ with Filtering

	[Fact]
	public async Task FilterDlqEntriesByMultipleCriteria()
	{
		// Arrange
		var dlq = new InMemoryDeadLetterQueue(_dlqLogger);

		// Enqueue various failure types
		await dlq.EnqueueAsync(
			new TestOrderMessage { OrderId = Guid.NewGuid() },
			DeadLetterReason.MaxRetriesExceeded).ConfigureAwait(false);

		await dlq.EnqueueAsync(
			new TestOrderMessage { OrderId = Guid.NewGuid() },
			DeadLetterReason.CircuitBreakerOpen).ConfigureAwait(false);

		await dlq.EnqueueAsync(
			new TestPaymentMessage { PaymentId = Guid.NewGuid() },
			DeadLetterReason.MaxRetriesExceeded).ConfigureAwait(false);

		await dlq.EnqueueAsync(
			new TestPaymentMessage { PaymentId = Guid.NewGuid() },
			DeadLetterReason.ValidationFailed).ConfigureAwait(false);

		await dlq.EnqueueAsync(
			new TestPoisonMessage { CorruptData = "bad" },
			DeadLetterReason.PoisonMessage).ConfigureAwait(false);

		// Act & Assert - Filter by reason
		var maxRetriesEntries = await dlq.GetEntriesAsync(
			DeadLetterQueryFilter.ByReason(DeadLetterReason.MaxRetriesExceeded)).ConfigureAwait(false);
		maxRetriesEntries.Count.ShouldBe(2);

		// Filter by message type
		var orderEntries = await dlq.GetEntriesAsync(
			DeadLetterQueryFilter.ByMessageType("TestOrderMessage")).ConfigureAwait(false);
		orderEntries.Count.ShouldBe(2);

		// Count by reason
		var circuitBreakerCount = await dlq.GetCountAsync(
			DeadLetterQueryFilter.ByReason(DeadLetterReason.CircuitBreakerOpen)).ConfigureAwait(false);
		circuitBreakerCount.ShouldBe(1);

		var poisonCount = await dlq.GetCountAsync(
			DeadLetterQueryFilter.ByReason(DeadLetterReason.PoisonMessage)).ConfigureAwait(false);
		poisonCount.ShouldBe(1);

		// Total count
		var totalCount = await dlq.GetCountAsync().ConfigureAwait(false);
		totalCount.ShouldBe(5);
	}

	#endregion Integration Scenario: Multiple Failure Reasons in DLQ with Filtering

	#region Component Verification: Backoff Calculator Presets

	[Fact]
	public void VerifyBackoffPresetConfigurations()
	{
		// Arrange & Act
		var highThroughput = ExponentialBackoffCalculator.CreateForHighThroughput();
		var messageQueue = ExponentialBackoffCalculator.CreateForMessageQueue();
		var transientFailures = ExponentialBackoffCalculator.CreateForTransientFailures();

		// Assert - High throughput has short delays
		var htDelay1 = highThroughput.CalculateDelay(1);
		htDelay1.TotalMilliseconds.ShouldBeInRange(50, 150); // ~100ms base with 50% jitter

		// Message queue has medium delays
		var mqDelay1 = messageQueue.CalculateDelay(1);
		mqDelay1.TotalMilliseconds.ShouldBeInRange(750, 1250); // ~1s base with 25% jitter

		// Transient failures has very short delays
		var tfDelay1 = transientFailures.CalculateDelay(1);
		tfDelay1.TotalMilliseconds.ShouldBeInRange(35, 65); // ~50ms base with 30% jitter
	}

	#endregion Component Verification: Backoff Calculator Presets

	#region Component Verification: Circuit Breaker State Machine

	[Fact]
	public async Task VerifyCompleteCircuitBreakerStateTransitions()
	{
		// Arrange
		var options = new CircuitBreakerOptions
		{
			FailureThreshold = 2,
			OpenDuration = TimeSpan.FromMilliseconds(50),
			SuccessThreshold = 2,
		};
		var circuitBreaker = new CircuitBreakerPolicy(options, "test", _cbLogger);
		var stateTransitions = new List<(CircuitState From, CircuitState To)>();

		circuitBreaker.StateChanged += (_, args) =>
			stateTransitions.Add((args.PreviousState, args.NewState));

		// Act - Full cycle: Closed -> Open -> HalfOpen -> Closed
		// Step 1: Closed (initial)
		circuitBreaker.State.ShouldBe(CircuitState.Closed);

		// Step 2: Failures -> Open
		circuitBreaker.RecordFailure();
		circuitBreaker.RecordFailure();
		circuitBreaker.State.ShouldBe(CircuitState.Open);

		// Step 3: Wait -> HalfOpen
		await Task.Delay(100).ConfigureAwait(false);
		circuitBreaker.State.ShouldBe(CircuitState.HalfOpen);

		// Step 4: Successes -> Closed
		circuitBreaker.RecordSuccess();
		circuitBreaker.RecordSuccess();
		circuitBreaker.State.ShouldBe(CircuitState.Closed);

		// Assert
		stateTransitions.Count.ShouldBe(3);
		stateTransitions[0].ShouldBe((CircuitState.Closed, CircuitState.Open));
		stateTransitions[1].ShouldBe((CircuitState.Open, CircuitState.HalfOpen));
		stateTransitions[2].ShouldBe((CircuitState.HalfOpen, CircuitState.Closed));
	}

	#endregion Component Verification: Circuit Breaker State Machine

	#region Component Verification: Transport Registry Thread Safety

	[Fact]
	public async Task VerifyTransportRegistryThreadSafety()
	{
		// Arrange
		var registry = new TransportCircuitBreakerRegistry();
		var transportNames = new[] { "rabbitmq", "kafka", "azure-servicebus", "redis", "grpc" };
		const int operationsPerTransport = 100;

		// Act - Concurrent operations on registry
		var tasks = transportNames
			.SelectMany(name =>
				Enumerable.Range(0, operationsPerTransport).Select(_ => Task.Run(() =>
				{
					var breaker = registry.GetOrCreate(name);
					breaker.RecordSuccess();
					return breaker.State;
				})))
			.ToList();

		var results = await Task.WhenAll(tasks).ConfigureAwait(false);

		// Assert
		results.Length.ShouldBe(transportNames.Length * operationsPerTransport);
		registry.Count.ShouldBe(transportNames.Length);

		foreach (var name in transportNames)
		{
			var breaker = registry.TryGet(name);
			breaker.ShouldNotBeNull();
			breaker.State.ShouldBe(CircuitState.Closed);
		}
	}

	#endregion Component Verification: Transport Registry Thread Safety

	#region Test Message Types

	private sealed class TestOrderMessage
	{
		public Guid OrderId { get; init; }
		public decimal Amount { get; init; }
	}

	private sealed class TestPaymentMessage
	{
		public Guid PaymentId { get; init; }
	}

	private sealed class TestEventMessage
	{
		public Guid EventId { get; init; }
	}

	private sealed class TestPoisonMessage
	{
		public string CorruptData { get; init; } = string.Empty;
	}

	#endregion Test Message Types
}
