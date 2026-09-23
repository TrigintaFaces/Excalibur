// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

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
			ConsecutiveFailureThreshold = 10,
			BreakDuration = TimeSpan.FromMinutes(2),
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
		var defaultOptions = new CircuitBreakerOptions { ConsecutiveFailureThreshold = 2 };
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
		var options = new CircuitBreakerOptions { ConsecutiveFailureThreshold = 2 };
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
			ConsecutiveFailureThreshold = 1,
			BreakDuration = TimeSpan.FromMilliseconds(50),
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
		var options = new CircuitBreakerOptions { ConsecutiveFailureThreshold = 1 };
		var registry = new TransportCircuitBreakerRegistry(options);

		var rabbitBreaker = registry.GetOrCreate("rabbitmq");
		var kafkaBreaker = registry.GetOrCreate("kafka");

		// Open both
		await rabbitBreaker.FailAsync().ConfigureAwait(false);
		await kafkaBreaker.FailAsync().ConfigureAwait(false);

		// Act
		await registry.ResetAllAsync(CancellationToken.None).ConfigureAwait(false);

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
		var options = new CircuitBreakerOptions { ConsecutiveFailureThreshold = 1 };
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
	//
	// These arms previously certified the opposite contract. One was called
	// ShareTheOverflowCircuitForKeysPastTheCap and asserted that two different keys past the cap
	// receive the SAME circuit -- a test whose name stated the defect as the requirement. Sharing a
	// circuit between keys means one dependency's failures open another's circuit, so the arms now
	// bind isolation, which is the property the registry exists to provide.

	[Fact]
	public void StopGrowingOnceTheCircuitCapIsReached()
	{
		var registry = new TransportCircuitBreakerRegistry();

		for (var i = 0; i < TransportCircuitBreakerRegistry.MaxBreakers + 500; i++)
		{
			_ = registry.GetOrCreate($"key-{i}");
		}

		registry.Count.ShouldBeLessThanOrEqualTo(TransportCircuitBreakerRegistry.MaxBreakers);
	}

	[Fact]
	public void NeverShareOneCircuitBetweenTwoKeysPastTheCap()
	{
		// The replacement for the arm that asserted sharing. Filling the cap with IDLE circuits means
		// the registry can evict, so both of these keys get a retained circuit of their own -- but the
		// assertion deliberately binds only NOT-SHARED, because that is the guarantee. It holds whether
		// the key was served by eviction or by the unretained fallback.
		var registry = new TransportCircuitBreakerRegistry();

		for (var i = 0; i < TransportCircuitBreakerRegistry.MaxBreakers; i++)
		{
			_ = registry.GetOrCreate($"key-{i}");
		}

		var first = registry.GetOrCreate("overflowed-a");
		var second = registry.GetOrCreate("overflowed-b");

		first.ShouldNotBeSameAs(second);
	}

	[Fact]
	public async Task PreferAnIdleCircuitOverOneThatIsOpenWhenEvicting()
	{
		// Renamed from NeverEvictACircuitThatIsOpen, which asserted an absolute the contract does not
		// make: when NOTHING is idle the least recently used circuit is evicted whatever its state,
		// because keeping the map coherent matters more than keeping one circuit. What IS guaranteed is
		// the preference -- an idle circuit is always taken first -- so that the drop-a-protective-circuit
		// branch is reached only when every circuit is actively protecting something.
		//
		// The reason the preference is load-bearing: evicting an OPEN circuit sends traffic straight back
		// at the dependency it is shielding. The oldest entry by recency is deliberately the open one, so
		// a recency-only policy would choose it first.
		var options = new CircuitBreakerOptions { ConsecutiveFailureThreshold = 1 };
		var registry = new TransportCircuitBreakerRegistry(options);

		var protectedCircuit = registry.GetOrCreate("must-survive");
		await protectedCircuit.FailAsync().ConfigureAwait(false);
		protectedCircuit.State.ShouldBe(CircuitState.Open);

		for (var i = 0; i < TransportCircuitBreakerRegistry.MaxBreakers + 200; i++)
		{
			_ = registry.GetOrCreate($"filler-{i}");
		}

		registry.TryGet("must-survive").ShouldBeSameAs(protectedCircuit);
		registry.TryGet("must-survive")!.State.ShouldBe(CircuitState.Open);
	}

	[Fact]
	public async Task KeepTheCallersOwnOptionsForAKeyServedPastTheCap()
	{
		// The third edge: the shared overflow circuit was built from whichever caller created it FIRST,
		// so every later key silently ran on a stranger's thresholds. A key served past the cap must
		// honour the options passed FOR IT.
		//
		// The ordering here is load-bearing and my first version of this arm got it wrong: it made the
		// key under test the first caller past the cap, which the old code served with that caller's own
		// options -- so the arm passed against the very defect it was written to catch. Options theft
		// only manifests from the SECOND overflowing caller onward. "earlier-overflow" must therefore
		// come first, with a threshold high enough that inheriting it would keep the circuit closed.
		var registry = new TransportCircuitBreakerRegistry(new CircuitBreakerOptions { ConsecutiveFailureThreshold = 5 });

		for (var i = 0; i < TransportCircuitBreakerRegistry.MaxBreakers; i++)
		{
			_ = registry.GetOrCreate($"key-{i}");
		}

		_ = registry.GetOrCreate("earlier-overflow", new CircuitBreakerOptions { ConsecutiveFailureThreshold = 50 });

		var mine = registry.GetOrCreate("mine", new CircuitBreakerOptions { ConsecutiveFailureThreshold = 1 });
		await mine.FailAsync().ConfigureAwait(false);

		// One failure opens it only if MY threshold of 1 was used. Inheriting the earlier caller's 50
		// leaves it closed, which is exactly what the pre-fix registry did.
		mine.State.ShouldBe(CircuitState.Open);
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

	[Fact]
	public void LetTryGetFindEveryCircuitGetOrCreateHandedOutPastTheCap()
	{
		// The history constraint: GetOrCreate(k) and TryGet(k) must agree, here on the ordinary
		// past-the-cap path where an idle circuit was available to evict.
		//
		// SCOPE, stated because I checked and it is not what I first assumed: this arm does NOT bind the
		// withdrawn create-but-do-not-store remedy. Every filler here is idle, so idle eviction always
		// succeeds and the unstored branch is never reached -- I verified that by reintroducing the
		// withdrawn remedy, and this arm stayed GREEN while StayCoherentAndBoundedWhenEveryCircuitIsOpen
		// went red. That arm is the lock for the unstored defect; this one is the cheap sibling covering
		// the common path.
		var registry = new TransportCircuitBreakerRegistry();

		for (var i = 0; i < TransportCircuitBreakerRegistry.MaxBreakers; i++)
		{
			_ = registry.GetOrCreate($"key-{i}");
		}

		var handedOut = registry.GetOrCreate("served-past-the-cap");

		registry.TryGet("served-past-the-cap").ShouldBeSameAs(handedOut);
	}

	[Fact]
	public async Task StayCoherentAndBoundedWhenEveryCircuitIsOpen()
	{
		// The pathological case the preference cannot serve: the cap fully occupied by OPEN circuits, so
		// no idle victim exists. The registry evicts anyway. What must survive is coherence and the
		// bound -- not any particular circuit.
		//
		// THIS IS THE ARM THAT BINDS THE WITHDRAWN REMEDY. Reintroducing create-but-do-not-store turns
		// it RED (TryGet returns null for a key GetOrCreate just reported success for) while every other
		// arm here stays green, because this is the only scenario that reaches the branch at all.
		var registry = new TransportCircuitBreakerRegistry(new CircuitBreakerOptions { ConsecutiveFailureThreshold = 1 });

		for (var i = 0; i < TransportCircuitBreakerRegistry.MaxBreakers; i++)
		{
			var circuit = registry.GetOrCreate($"open-{i}");
			await circuit.FailAsync().ConfigureAwait(false);
		}

		var late = registry.GetOrCreate("arrives-last");

		registry.TryGet("arrives-last").ShouldBeSameAs(late);
		registry.Count.ShouldBeLessThanOrEqualTo(TransportCircuitBreakerRegistry.MaxBreakers);
	}
	#endregion Bounded Registry Tests
}

