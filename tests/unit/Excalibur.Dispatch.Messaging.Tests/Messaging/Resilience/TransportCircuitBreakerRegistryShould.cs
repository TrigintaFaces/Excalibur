// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Resilience;
using Excalibur.Dispatch.Options.Resilience;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Tests.Messaging.Resilience;

/// <summary>
/// Tests for the <see cref="TransportCircuitBreakerRegistry"/> class.
/// Epic 6 (bd-rj9o): Integration tests for per-transport circuit breaker isolation.
/// </summary>
[Trait("Category", "Unit")]
public sealed class TransportCircuitBreakerRegistryShould
{
	#region GetOrCreate Tests

	[Fact]
	public void CreateNewCircuitBreakerForTransport()
	{
		// Arrange
		var registry = new TransportCircuitBreakerRegistry();

		// Act
		var breaker = registry.GetOrCreate("rabbitmq");

		// Assert
		_ = breaker.ShouldNotBeNull();
		breaker.State.ShouldBe(CircuitState.Closed);
	}

	[Fact]
	public void ReturnSameCircuitBreakerForSameTransport()
	{
		// Arrange
		var registry = new TransportCircuitBreakerRegistry();

		// Act
		var breaker1 = registry.GetOrCreate("rabbitmq");
		var breaker2 = registry.GetOrCreate("rabbitmq");

		// Assert
		breaker1.ShouldBeSameAs(breaker2);
	}

	[Fact]
	public void CreateDifferentBreakersForDifferentTransports()
	{
		// Arrange
		var registry = new TransportCircuitBreakerRegistry();

		// Act
		var rabbitBreaker = registry.GetOrCreate("rabbitmq");
		var kafkaBreaker = registry.GetOrCreate("kafka");
		var azureBreaker = registry.GetOrCreate("azure-servicebus");

		// Assert
		rabbitBreaker.ShouldNotBeSameAs(kafkaBreaker);
		rabbitBreaker.ShouldNotBeSameAs(azureBreaker);
		kafkaBreaker.ShouldNotBeSameAs(azureBreaker);
	}

	[Fact]
	public void BeCaseInsensitiveForTransportNames()
	{
		// Arrange
		var registry = new TransportCircuitBreakerRegistry();

		// Act
		var breaker1 = registry.GetOrCreate("RabbitMQ");
		var breaker2 = registry.GetOrCreate("rabbitmq");
		var breaker3 = registry.GetOrCreate("RABBITMQ");

		// Assert
		breaker1.ShouldBeSameAs(breaker2);
		breaker2.ShouldBeSameAs(breaker3);
	}

	[Fact]
	public void UseProvidedOptionsForNewBreaker()
	{
		// Arrange
		var registry = new TransportCircuitBreakerRegistry();
		var options = new CircuitBreakerOptions
		{
			FailureThreshold = 10,
			OpenDuration = TimeSpan.FromMinutes(2),
		};

		// Act
		var breaker = registry.GetOrCreate("rabbitmq", options);

		// Assert - Verify it uses the provided options by testing behavior
		for (var i = 0; i < 9; i++)
		{
			breaker.RecordFailure();
		}

		breaker.State.ShouldBe(CircuitState.Closed); // Still closed (threshold is 10)

		breaker.RecordFailure(); // 10th failure
		breaker.State.ShouldBe(CircuitState.Open); // Now open
	}

	[Fact]
	public void UseDefaultOptionsWhenNotProvided()
	{
		// Arrange
		var defaultOptions = new CircuitBreakerOptions { FailureThreshold = 2 };
		var registry = new TransportCircuitBreakerRegistry(defaultOptions);

		// Act
		var breaker = registry.GetOrCreate("rabbitmq");

		// Assert - Verify it uses default options
		breaker.RecordFailure();
		breaker.State.ShouldBe(CircuitState.Closed);

		breaker.RecordFailure(); // 2nd failure (threshold is 2)
		breaker.State.ShouldBe(CircuitState.Open);
	}

	[Fact]
	public void ThrowOnNullTransportName()
	{
		// Arrange
		var registry = new TransportCircuitBreakerRegistry();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => registry.GetOrCreate(null!));
	}

	[Fact]
	public void ThrowOnEmptyTransportName()
	{
		// Arrange
		var registry = new TransportCircuitBreakerRegistry();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => registry.GetOrCreate(string.Empty));
	}

	[Fact]
	public void ThrowOnWhitespaceTransportName()
	{
		// Arrange
		var registry = new TransportCircuitBreakerRegistry();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => registry.GetOrCreate("   "));
	}

	#endregion GetOrCreate Tests

	#region TryGet Tests

	[Fact]
	public void ReturnNullForUnknownTransport()
	{
		// Arrange
		var registry = new TransportCircuitBreakerRegistry();

		// Act
		var breaker = registry.TryGet("unknown");

		// Assert
		breaker.ShouldBeNull();
	}

	[Fact]
	public void ReturnExistingBreaker()
	{
		// Arrange
		var registry = new TransportCircuitBreakerRegistry();
		var created = registry.GetOrCreate("rabbitmq");

		// Act
		var retrieved = registry.TryGet("rabbitmq");

		// Assert
		_ = retrieved.ShouldNotBeNull();
		retrieved.ShouldBeSameAs(created);
	}

	[Fact]
	public void TryGetBeCaseInsensitive()
	{
		// Arrange
		var registry = new TransportCircuitBreakerRegistry();
		_ = registry.GetOrCreate("RabbitMQ");

		// Act
		var breaker = registry.TryGet("rabbitmq");

		// Assert
		_ = breaker.ShouldNotBeNull();
	}

	#endregion TryGet Tests

	#region Per-Transport Isolation Tests

	[Fact]
	public void IsolateFailuresBetweenTransports()
	{
		// Arrange
		var options = new CircuitBreakerOptions { FailureThreshold = 2 };
		var registry = new TransportCircuitBreakerRegistry(options);

		var rabbitBreaker = registry.GetOrCreate("rabbitmq");
		var kafkaBreaker = registry.GetOrCreate("kafka");

		// Act - Open rabbit circuit
		rabbitBreaker.RecordFailure();
		rabbitBreaker.RecordFailure();

		// Assert - Kafka should still be closed
		rabbitBreaker.State.ShouldBe(CircuitState.Open);
		kafkaBreaker.State.ShouldBe(CircuitState.Closed);
	}

	[Fact]
	public void MaintainIndependentStatePerTransport()
	{
		// Arrange
		var options = new CircuitBreakerOptions
		{
			FailureThreshold = 1,
			OpenDuration = TimeSpan.FromMilliseconds(50),
			SuccessThreshold = 1,
		};
		var registry = new TransportCircuitBreakerRegistry(options);

		var rabbitBreaker = registry.GetOrCreate("rabbitmq");
		var kafkaBreaker = registry.GetOrCreate("kafka");

		// Open rabbit, leave kafka closed
		rabbitBreaker.RecordFailure();
		rabbitBreaker.State.ShouldBe(CircuitState.Open);

		// Wait for half-open
		Thread.Sleep(100);

		// Act - Kafka still works, rabbit is half-open
		kafkaBreaker.RecordSuccess();
		rabbitBreaker.State.ShouldBe(CircuitState.HalfOpen);

		// Close rabbit
		rabbitBreaker.RecordSuccess();

		// Assert - Both now closed but independent
		rabbitBreaker.State.ShouldBe(CircuitState.Closed);
		kafkaBreaker.State.ShouldBe(CircuitState.Closed);
		((ICircuitBreakerDiagnostics)kafkaBreaker).ConsecutiveFailures.ShouldBe(0);
	}

	#endregion Per-Transport Isolation Tests

	#region Remove Tests

	[Fact]
	public void RemoveExistingBreaker()
	{
		// Arrange
		var registry = new TransportCircuitBreakerRegistry();
		_ = registry.GetOrCreate("rabbitmq");

		// Act
		var removed = registry.Remove("rabbitmq");

		// Assert
		removed.ShouldBeTrue();
		registry.TryGet("rabbitmq").ShouldBeNull();
	}

	[Fact]
	public void ReturnFalseWhenRemovingNonExistent()
	{
		// Arrange
		var registry = new TransportCircuitBreakerRegistry();

		// Act
		var removed = registry.Remove("unknown");

		// Assert
		removed.ShouldBeFalse();
	}

	[Fact]
	public void RemoveBeCaseInsensitive()
	{
		// Arrange
		var registry = new TransportCircuitBreakerRegistry();
		_ = registry.GetOrCreate("RabbitMQ");

		// Act
		var removed = registry.Remove("rabbitmq");

		// Assert
		removed.ShouldBeTrue();
	}

	#endregion Remove Tests

	#region ResetAll Tests

	[Fact]
	public void ResetAllBreakers()
	{
		// Arrange
		var options = new CircuitBreakerOptions { FailureThreshold = 1 };
		var registry = new TransportCircuitBreakerRegistry(options);

		var rabbitBreaker = registry.GetOrCreate("rabbitmq");
		var kafkaBreaker = registry.GetOrCreate("kafka");

		// Open both
		rabbitBreaker.RecordFailure();
		kafkaBreaker.RecordFailure();

		// Act
		registry.ResetAll();

		// Assert
		rabbitBreaker.State.ShouldBe(CircuitState.Closed);
		kafkaBreaker.State.ShouldBe(CircuitState.Closed);
	}

	#endregion ResetAll Tests

	#region GetAllStates Tests

	[Fact]
	public void ReturnAllTransportStates()
	{
		// Arrange
		var options = new CircuitBreakerOptions { FailureThreshold = 1 };
		var registry = new TransportCircuitBreakerRegistry(options);

		var rabbitBreaker = registry.GetOrCreate("rabbitmq");
		var kafkaBreaker = registry.GetOrCreate("kafka");

		rabbitBreaker.RecordFailure(); // Open rabbit

		// Act
		var states = registry.GetAllStates();

		// Assert
		states.Count.ShouldBe(2);
		states["rabbitmq"].ShouldBe(CircuitState.Open);
		states["kafka"].ShouldBe(CircuitState.Closed);
	}

	[Fact]
	public void ReturnEmptyDictionaryWhenNoBreakers()
	{
		// Arrange
		var registry = new TransportCircuitBreakerRegistry();

		// Act
		var states = registry.GetAllStates();

		// Assert
		states.ShouldBeEmpty();
	}

	#endregion GetAllStates Tests

	#region GetTransportNames Tests

	[Fact]
	public void ReturnAllRegisteredTransportNames()
	{
		// Arrange
		var registry = new TransportCircuitBreakerRegistry();
		_ = registry.GetOrCreate("rabbitmq");
		_ = registry.GetOrCreate("kafka");
		_ = registry.GetOrCreate("azure-servicebus");

		// Act
		var names = registry.GetTransportNames().ToList();

		// Assert
		names.Count.ShouldBe(3);
		names.ShouldContain("rabbitmq");
		names.ShouldContain("kafka");
		names.ShouldContain("azure-servicebus");
	}

	#endregion GetTransportNames Tests

	#region Count Tests

	[Fact]
	public void ReturnCorrectCount()
	{
		// Arrange
		var registry = new TransportCircuitBreakerRegistry();

		// Assert initial
		registry.Count.ShouldBe(0);

		// Add some
		_ = registry.GetOrCreate("rabbitmq");
		registry.Count.ShouldBe(1);

		_ = registry.GetOrCreate("kafka");
		registry.Count.ShouldBe(2);

		// Remove one
		_ = registry.Remove("rabbitmq");
		registry.Count.ShouldBe(1);
	}

	#endregion Count Tests

	#region Logger Factory Tests

	[Fact]
	public void UseProvidedLoggerFactory()
	{
		// Arrange
		var loggerFactory = NullLoggerFactory.Instance;
		var registry = new TransportCircuitBreakerRegistry(loggerFactory: loggerFactory);

		// Act - Should not throw
		var breaker = registry.GetOrCreate("rabbitmq");

		// Assert
		_ = breaker.ShouldNotBeNull();
	}

	#endregion Logger Factory Tests
}
