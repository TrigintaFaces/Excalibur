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
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Dispatch.Core")]
public sealed class TransportCircuitBreakerRegistryShould
{
	private static async Task WaitForStateAsync(ICircuitBreakerPolicy policy, CircuitState expectedState, TimeSpan timeout)
	{
		var scaledTimeout = global::Tests.Shared.Infrastructure.TestTimeouts.Scale(timeout);
		if (scaledTimeout < TimeSpan.FromSeconds(10))
		{
			scaledTimeout = TimeSpan.FromSeconds(10);
		}

		var stateObserved = await global::Tests.Shared.Infrastructure.WaitHelpers.WaitUntilAsync(
				() => policy.State == expectedState,
				scaledTimeout,
				TimeSpan.FromMilliseconds(100))
			.ConfigureAwait(false);

		if (!stateObserved && policy.State == expectedState)
		{
			stateObserved = true;
		}

		stateObserved.ShouldBeTrue($"Expected circuit state {expectedState} within {scaledTimeout}, actual state was {policy.State}.");
	}

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
	public async Task UseProvidedOptionsForNewBreaker()
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
			await breaker.FailAsync().ConfigureAwait(false);
		}

		breaker.State.ShouldBe(CircuitState.Closed); // Still closed (threshold is 10)

		await breaker.FailAsync(); // 10th failure
		breaker.State.ShouldBe(CircuitState.Open); // Now open
	}

	[Fact]
	public async Task UseDefaultOptionsWhenNotProvided()
	{
		// Arrange
		var defaultOptions = new CircuitBreakerOptions { FailureThreshold = 2 };
		var registry = new TransportCircuitBreakerRegistry(defaultOptions);

		// Act
		var breaker = registry.GetOrCreate("rabbitmq");

		// Assert - Verify it uses default options
		await breaker.FailAsync().ConfigureAwait(false);
		breaker.State.ShouldBe(CircuitState.Closed);

		await breaker.FailAsync(); // 2nd failure (threshold is 2)
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
	public async Task IsolateFailuresBetweenTransports()
	{
		// Arrange
		var options = new CircuitBreakerOptions { FailureThreshold = 2 };
		var registry = new TransportCircuitBreakerRegistry(options);

		var rabbitBreaker = registry.GetOrCreate("rabbitmq");
		var kafkaBreaker = registry.GetOrCreate("kafka");

		// Act - Open rabbit circuit
		await rabbitBreaker.FailAsync().ConfigureAwait(false);
		await rabbitBreaker.FailAsync().ConfigureAwait(false);

		// Assert - Kafka should still be closed
		rabbitBreaker.State.ShouldBe(CircuitState.Open);
		kafkaBreaker.State.ShouldBe(CircuitState.Closed);
	}

	[Fact]
	public async Task MaintainIndependentStatePerTransport()
	{
		// Arrange
		var options = new CircuitBreakerOptions
		{
			FailureThreshold = 1,
			OpenDuration = TimeSpan.FromMilliseconds(50),
		};
		var registry = new TransportCircuitBreakerRegistry(options);

		var rabbitBreaker = registry.GetOrCreate("rabbitmq");
		var kafkaBreaker = registry.GetOrCreate("kafka");

		// Open rabbit, leave kafka closed
		await rabbitBreaker.FailAsync().ConfigureAwait(false);
		rabbitBreaker.State.ShouldBe(CircuitState.Open);

		// Wait for half-open
		await WaitForStateAsync(rabbitBreaker, CircuitState.HalfOpen, TimeSpan.FromSeconds(2)).ConfigureAwait(false);

		// Act - Kafka still works, rabbit is half-open
		await kafkaBreaker.SucceedAsync().ConfigureAwait(false);
		rabbitBreaker.State.ShouldBe(CircuitState.HalfOpen);

		// Close rabbit
		await rabbitBreaker.SucceedAsync().ConfigureAwait(false);

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
	public async Task ResetAllBreakers()
	{
		// Arrange
		var options = new CircuitBreakerOptions { FailureThreshold = 1 };
		var registry = new TransportCircuitBreakerRegistry(options);

		var rabbitBreaker = registry.GetOrCreate("rabbitmq");
		var kafkaBreaker = registry.GetOrCreate("kafka");

		// Open both
		await rabbitBreaker.FailAsync().ConfigureAwait(false);
		await kafkaBreaker.FailAsync().ConfigureAwait(false);

		// Act
		registry.ResetAll();

		// Assert
		rabbitBreaker.State.ShouldBe(CircuitState.Closed);
		kafkaBreaker.State.ShouldBe(CircuitState.Closed);
	}

	#endregion ResetAll Tests

	#region GetAllStates Tests

	[Fact]
	public async Task ReturnAllTransportStates()
	{
		// Arrange
		var options = new CircuitBreakerOptions { FailureThreshold = 1 };
		var registry = new TransportCircuitBreakerRegistry(options);

		var rabbitBreaker = registry.GetOrCreate("rabbitmq");
		var kafkaBreaker = registry.GetOrCreate("kafka");

		await rabbitBreaker.FailAsync(); // Open rabbit

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

	#region Bounded Registry Tests

	// The circuit key reaches GetOrCreate from CircuitBreakerOptions.CircuitKeySelector, which is
	// consumer-supplied and may be derived from message content. An unbounded map would therefore
	// grow with traffic. Safety arm: the map stops growing at the cap. Liveness arm: below the cap
	// distinct keys still get distinct circuits, so the safety arm cannot pass by capping at one.

	[Fact]
	public void StopGrowingOnceTheCircuitCapIsReached()
	{
		var registry = new TransportCircuitBreakerRegistry();

		for (var i = 0; i < TransportCircuitBreakerRegistry.MaxBreakers + 500; i++)
		{
			_ = registry.GetOrCreate($"key-{i}");
		}

		registry.Count.ShouldBeLessThanOrEqualTo(TransportCircuitBreakerRegistry.MaxBreakers + 1);
		registry.GetTransportNames().ShouldContain(TransportCircuitBreakerRegistry.OverflowKey);
	}

	[Fact]
	public void ShareTheOverflowCircuitForKeysPastTheCap()
	{
		var registry = new TransportCircuitBreakerRegistry();

		for (var i = 0; i < TransportCircuitBreakerRegistry.MaxBreakers; i++)
		{
			_ = registry.GetOrCreate($"key-{i}");
		}

		var first = registry.GetOrCreate("overflowed-a");
		var second = registry.GetOrCreate("overflowed-b");

		first.ShouldBeSameAs(second);
	}

	[Fact]
	public void GiveDistinctCircuitsToDistinctKeysBelowTheCap()
	{
		var registry = new TransportCircuitBreakerRegistry();

		var a = registry.GetOrCreate("rabbitmq");
		var b = registry.GetOrCreate("kafka");

		a.ShouldNotBeSameAs(b);
		registry.Count.ShouldBe(2);
	}

	#endregion Bounded Registry Tests
}

