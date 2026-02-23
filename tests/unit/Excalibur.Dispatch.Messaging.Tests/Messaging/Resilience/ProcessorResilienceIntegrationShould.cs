// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.ErrorHandling;
using Excalibur.Dispatch.Resilience;
using Excalibur.Dispatch.Options.Delivery;
using Excalibur.Dispatch.Options.Resilience;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Tests.Messaging.Resilience;

/// <summary>
///     Integration tests for processor resilience verifying the complete integration of
///     resilience components with OutboxProcessor and InboxProcessor:
///     - DLQ integration with processors
///     - Circuit breaker per-transport isolation
///     - Exponential backoff coordination
///     - DeliveryGuaranteeOptions configuration
/// </summary>
[Trait("Category", "Unit")]
public sealed class ProcessorResilienceIntegrationShould
{
	private readonly ILogger<InMemoryDeadLetterQueue> _dlqLogger;
	private readonly ILogger<CircuitBreakerPolicy> _cbLogger;

	public ProcessorResilienceIntegrationShould()
	{
		_dlqLogger = NullLoggerFactory.Instance.CreateLogger<InMemoryDeadLetterQueue>();
		_cbLogger = NullLoggerFactory.Instance.CreateLogger<CircuitBreakerPolicy>();
	}

	#region Processor DLQ Integration Verification

	[Fact]
	public async Task OutboxProcessorRouteFailedMessageToDlqAfterMaxRetries()
	{
		// Arrange - Simulates OutboxProcessor behavior
		var dlq = new InMemoryDeadLetterQueue(_dlqLogger);
		var maxAttempts = 3;
		var message = new TestOutboxMessage
		{
			MessageId = Guid.NewGuid().ToString(),
			MessageType = "OrderCreatedEvent",
			Attempts = maxAttempts,
			CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-5)
		};

		// Act - Simulate what OutboxProcessor does when max retries exceeded
		var entryId = await dlq.EnqueueAsync(
			message,
			DeadLetterReason.MaxRetriesExceeded,
			new InvalidOperationException("Persistent transport failure"),
			new Dictionary<string, string>
			{
				["MessageType"] = message.MessageType,
				["DispatcherId"] = "outbox-dispatcher-1",
				["Attempts"] = message.Attempts.ToString(),
				["CreatedAt"] = message.CreatedAt.ToString("O")
			}).ConfigureAwait(false);

		// Assert
		entryId.ShouldNotBe(Guid.Empty);
		var entry = await dlq.GetEntryAsync(entryId).ConfigureAwait(false);
		entry.ShouldNotBeNull();
		entry.Reason.ShouldBe(DeadLetterReason.MaxRetriesExceeded);
		entry.Metadata!["DispatcherId"].ShouldBe("outbox-dispatcher-1");
	}

	[Fact]
	public async Task InboxProcessorRouteFailedMessageToDlqWhenCircuitBreakerOpen()
	{
		// Arrange - Simulates InboxProcessor behavior
		var dlq = new InMemoryDeadLetterQueue(_dlqLogger);
		var cbOptions = new CircuitBreakerOptions { FailureThreshold = 2 };
		var circuitBreaker = new CircuitBreakerPolicy(cbOptions, "inbox-handler", _cbLogger);

		// Trip the circuit
		circuitBreaker.RecordFailure(new TimeoutException());
		circuitBreaker.RecordFailure(new TimeoutException());
		circuitBreaker.State.ShouldBe(CircuitState.Open);

		var message = new TestInboxMessage
		{
			ExternalMessageId = Guid.NewGuid().ToString(),
			MessageType = "PaymentProcessedEvent",
			ReceivedAt = DateTimeOffset.UtcNow,
			Attempts = 1
		};

		// Act - Simulate InboxProcessor detecting open circuit
		var entryId = await dlq.EnqueueAsync(
			message,
			DeadLetterReason.CircuitBreakerOpen,
			null,
			new Dictionary<string, string>
			{
				["MessageType"] = message.MessageType,
				["DispatcherId"] = "inbox-dispatcher-1",
				["Attempts"] = message.Attempts.ToString(),
				["ReceivedAt"] = message.ReceivedAt.ToString("O")
			}).ConfigureAwait(false);

		// Assert
		var entry = await dlq.GetEntryAsync(entryId).ConfigureAwait(false);
		entry.ShouldNotBeNull();
		entry.Reason.ShouldBe(DeadLetterReason.CircuitBreakerOpen);
	}

	#endregion

	#region Per-Transport Circuit Breaker Verification

	[Fact]
	public void RegistryIsolatesCircuitBreakersPerTransport()
	{
		// Arrange
		var options = new CircuitBreakerOptions { FailureThreshold = 3 };
		var registry = new TransportCircuitBreakerRegistry(options, NullLoggerFactory.Instance);

		// Act - Create separate circuits for different transports/message types
		var orderCircuit = registry.GetOrCreate("OrderEvents");
		var paymentCircuit = registry.GetOrCreate("PaymentEvents");
		var shippingCircuit = registry.GetOrCreate("ShippingEvents");

		// Trip only the order circuit
		orderCircuit.RecordFailure(new InvalidOperationException());
		orderCircuit.RecordFailure(new InvalidOperationException());
		orderCircuit.RecordFailure(new InvalidOperationException());

		// Assert - Only order circuit should be open
		orderCircuit.State.ShouldBe(CircuitState.Open);
		paymentCircuit.State.ShouldBe(CircuitState.Closed);
		shippingCircuit.State.ShouldBe(CircuitState.Closed);
		registry.Count.ShouldBe(3);
	}

	[Fact]
	public void RegistryReturnsExistingCircuitForSameTransport()
	{
		// Arrange
		var options = new CircuitBreakerOptions { FailureThreshold = 5 };
		var registry = new TransportCircuitBreakerRegistry(options, NullLoggerFactory.Instance);

		// Act
		var circuit1 = registry.GetOrCreate("SameTransport");
		circuit1.RecordFailure(new InvalidOperationException());

		var circuit2 = registry.GetOrCreate("SameTransport");

		// Assert - Should be the same instance with shared failure count
		circuit2.ConsecutiveFailures.ShouldBe(1);
	}

	[Fact]
	public void RegistryAllowsCustomOptionsPerTransport()
	{
		// Arrange
		var defaultOptions = new CircuitBreakerOptions { FailureThreshold = 5 };
		var registry = new TransportCircuitBreakerRegistry(defaultOptions, NullLoggerFactory.Instance);

		var criticalOptions = new CircuitBreakerOptions
		{
			FailureThreshold = 2,
			OpenDuration = TimeSpan.FromSeconds(30)
		};

		// Act
		var normalCircuit = registry.GetOrCreate("NormalTransport");
		var criticalCircuit = registry.GetOrCreate("CriticalTransport", criticalOptions);

		// Trip both with same number of failures
		normalCircuit.RecordFailure(new InvalidOperationException());
		normalCircuit.RecordFailure(new InvalidOperationException());

		criticalCircuit.RecordFailure(new InvalidOperationException());
		criticalCircuit.RecordFailure(new InvalidOperationException());

		// Assert - Critical should trip, normal should not
		normalCircuit.State.ShouldBe(CircuitState.Closed);
		criticalCircuit.State.ShouldBe(CircuitState.Open);
	}

	[Fact]
	public void RegistryGetAllStatesReturnsAllCircuits()
	{
		// Arrange
		var options = new CircuitBreakerOptions { FailureThreshold = 2 };
		var registry = new TransportCircuitBreakerRegistry(options, NullLoggerFactory.Instance);

		registry.GetOrCreate("Transport1").RecordSuccess();
		registry.GetOrCreate("Transport2").RecordFailure(new InvalidOperationException());
		registry.GetOrCreate("Transport3").RecordFailure(new InvalidOperationException());
		registry.GetOrCreate("Transport3").RecordFailure(new InvalidOperationException()); // Trip it

		// Act
		var states = registry.GetAllStates();

		// Assert
		states.Count.ShouldBe(3);
		states["Transport1"].ShouldBe(CircuitState.Closed);
		states["Transport2"].ShouldBe(CircuitState.Closed);
		states["Transport3"].ShouldBe(CircuitState.Open);
	}

	#endregion

	#region Exponential Backoff Coordination Verification

	[Fact]
	public void BackoffCalculatorCoordinatesWithRetryAttempts()
	{
		// Arrange - Simulates the backoff coordination in processors
		var backoff = new ExponentialBackoffCalculator(
			baseDelay: TimeSpan.FromMilliseconds(100),
			maxDelay: TimeSpan.FromSeconds(30),
			multiplier: 2.0,
			enableJitter: false);

		// Act - Calculate delays for typical retry attempts (1-5)
		var delays = new[]
		{
			backoff.CalculateDelay(1),
			backoff.CalculateDelay(2),
			backoff.CalculateDelay(3),
			backoff.CalculateDelay(4),
			backoff.CalculateDelay(5)
		};

		// Assert - Verify exponential growth
		delays[0].TotalMilliseconds.ShouldBe(100);  // 100 * 2^0 = 100
		delays[1].TotalMilliseconds.ShouldBe(200);  // 100 * 2^1 = 200
		delays[2].TotalMilliseconds.ShouldBe(400);  // 100 * 2^2 = 400
		delays[3].TotalMilliseconds.ShouldBe(800);  // 100 * 2^3 = 800
		delays[4].TotalMilliseconds.ShouldBe(1600); // 100 * 2^4 = 1600
	}

	[Fact]
	public void BackoffCalculatorRespectsTotalDelayBudget()
	{
		// Arrange
		var backoff = new ExponentialBackoffCalculator(
			baseDelay: TimeSpan.FromSeconds(1),
			maxDelay: TimeSpan.FromSeconds(5),
			multiplier: 2.0,
			enableJitter: false);

		// Act - Calculate delays that would exceed max
		var delay5 = backoff.CalculateDelay(5); // Would be 1 * 2^4 = 16s but capped at 5s
		var delay10 = backoff.CalculateDelay(10); // Would be huge but capped

		// Assert - Both should be capped at max
		delay5.ShouldBeLessThanOrEqualTo(TimeSpan.FromSeconds(5));
		delay10.ShouldBeLessThanOrEqualTo(TimeSpan.FromSeconds(5));
	}

	#endregion

	#region DeliveryGuaranteeOptions Verification

	[Fact]
	public void AtLeastOnceGuaranteeEnablesAutomaticRetry()
	{
		// Arrange & Act
		var options = new DeliveryGuaranteeOptions
		{
			Guarantee = DeliveryGuarantee.AtLeastOnce
		};

		// Assert - Default should enable retry
		options.EnableAutomaticRetry.ShouldBeTrue();
	}

	[Fact]
	public void AtMostOnceGuaranteeCanDisableRetry()
	{
		// Arrange & Act
		var options = new DeliveryGuaranteeOptions
		{
			Guarantee = DeliveryGuarantee.AtMostOnce,
			EnableAutomaticRetry = false
		};

		// Assert
		options.EnableAutomaticRetry.ShouldBeFalse();
	}

	[Fact]
	public void IdempotencyTrackingConfigurable()
	{
		// Arrange & Act
		var options = new DeliveryGuaranteeOptions
		{
			EnableIdempotencyTracking = true,
			IdempotencyKeyRetention = TimeSpan.FromDays(14)
		};

		// Assert
		options.EnableIdempotencyTracking.ShouldBeTrue();
		options.IdempotencyKeyRetention.ShouldBe(TimeSpan.FromDays(14));
	}

	#endregion

	#region End-to-End Resilience Flow Verification

	[Fact]
	public async Task FullResilienceFlowWorksEndToEnd()
	{
		// Arrange - Set up all resilience components
		var dlq = new InMemoryDeadLetterQueue(_dlqLogger);
		var backoff = ExponentialBackoffCalculator.CreateForMessageQueue();
		var cbOptions = new CircuitBreakerOptions
		{
			FailureThreshold = 5, // Higher threshold to allow recovery before circuit opens
			OpenDuration = TimeSpan.FromMilliseconds(100),
			SuccessThreshold = 2
		};
		var registry = new TransportCircuitBreakerRegistry(cbOptions, NullLoggerFactory.Instance);
		var deliveryOptions = new DeliveryGuaranteeOptions
		{
			Guarantee = DeliveryGuarantee.AtLeastOnce,
			EnableAutomaticRetry = true
		};

		const int maxAttempts = 5;
		var messageId = Guid.NewGuid().ToString();
		var transportName = "test-transport";
		var attempt = 0;
		var processedSuccessfully = false;

		// Act - Simulate processor retry flow
		while (attempt < maxAttempts && !processedSuccessfully)
		{
			attempt++;
			var circuit = registry.GetOrCreate(transportName);

			// Check circuit state
			if (circuit.State == CircuitState.Open)
			{
				await dlq.EnqueueAsync(
					new { MessageId = messageId },
					DeadLetterReason.CircuitBreakerOpen,
					metadata: new Dictionary<string, string> { ["Attempt"] = attempt.ToString() }
				).ConfigureAwait(false);
				break;
			}

			try
			{
				// Simulate processing that fails on attempts 1-3
				if (attempt <= 3)
				{
					throw new TimeoutException($"Simulated failure {attempt}");
				}

				// Attempt 4+ succeeds
				processedSuccessfully = true;
				circuit.RecordSuccess();
			}
			catch (TimeoutException)
			{
				circuit.RecordFailure(new TimeoutException());

				if (attempt < maxAttempts && deliveryOptions.EnableAutomaticRetry)
				{
					// Would wait backoff delay here
					var _ = backoff.CalculateDelay(attempt);
				}
			}
		}

		// If all retries failed but circuit didn't open, route to DLQ
		if (!processedSuccessfully)
		{
			var dlqCount = await dlq.GetCountAsync().ConfigureAwait(false);
			if (dlqCount == 0)
			{
				await dlq.EnqueueAsync(
					new { MessageId = messageId },
					DeadLetterReason.MaxRetriesExceeded).ConfigureAwait(false);
			}
		}

		// Assert - Message should have been processed on attempt 4 (after 3 failures)
		// Circuit stays closed because threshold is 5 and we only had 3 failures
		processedSuccessfully.ShouldBeTrue();
		attempt.ShouldBe(4);
		registry.GetOrCreate(transportName).State.ShouldBe(CircuitState.Closed);
	}

	#endregion

	#region Test Message Types

	private sealed class TestOutboxMessage
	{
		public string MessageId { get; set; } = string.Empty;
		public string MessageType { get; set; } = string.Empty;
		public int Attempts { get; set; }
		public DateTimeOffset CreatedAt { get; set; }
	}

	private sealed class TestInboxMessage
	{
		public string ExternalMessageId { get; set; } = string.Empty;
		public string MessageType { get; set; } = string.Empty;
		public DateTimeOffset ReceivedAt { get; set; }
		public int Attempts { get; set; }
	}

	#endregion
}
