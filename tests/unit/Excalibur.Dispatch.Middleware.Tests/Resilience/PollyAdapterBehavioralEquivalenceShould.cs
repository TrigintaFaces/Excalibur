// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Resilience;
using Excalibur.Dispatch.Options.Resilience;
using Excalibur.Dispatch.Resilience.Polly;
using CircuitState = Excalibur.Dispatch.Resilience.CircuitState;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

using Polly;

namespace Excalibur.Dispatch.Middleware.Tests.Resilience;

/// <summary>
/// Integration tests verifying behavioral equivalence between Polly adapters and default implementations.
/// Sprint 45 (bd-9xis): Behavioral equivalence tests.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Dispatch.Core")]
public sealed class PollyAdapterBehavioralEquivalenceShould : IDisposable
{
	private readonly List<IDisposable> _disposables = [];

	public void Dispose()
	{
		foreach (var disposable in _disposables)
		{
			disposable.Dispose();
		}
	}

	#region Circuit Breaker Behavioral Equivalence

	[Fact]
	public void BothCircuitBreakersStartInClosedState()
	{
		// Arrange
		var options = new CircuitBreakerOptions { ConsecutiveFailureThreshold = 5, MinimumThroughput = 5 };
		var loggerFactory = NullLoggerFactory.Instance;

		var defaultCb = new CircuitBreakerPolicy(options, "default", loggerFactory.CreateLogger<CircuitBreakerPolicy>());
		var pollyCb = new PollyCircuitBreakerPolicyAdapter(options, "polly", loggerFactory.CreateLogger<PollyCircuitBreakerPolicyAdapter>());
		_disposables.Add(pollyCb);

		// Assert
		((int)defaultCb.State).ShouldBe((int)CircuitState.Closed);
		((int)pollyCb.State).ShouldBe((int)Excalibur.Dispatch.Resilience.CircuitState.Closed);
	}

	[Fact]
	public async Task BothCircuitBreakersTrackConsecutiveFailures()
	{
		// Arrange
		var options = new CircuitBreakerOptions { ConsecutiveFailureThreshold = 5, MinimumThroughput = 5 };
		var loggerFactory = NullLoggerFactory.Instance;

		var defaultCb = new CircuitBreakerPolicy(options, "default", loggerFactory.CreateLogger<CircuitBreakerPolicy>());
		var pollyCb = new PollyCircuitBreakerPolicyAdapter(options, "polly", loggerFactory.CreateLogger<PollyCircuitBreakerPolicyAdapter>());
		_disposables.Add(pollyCb);

		// Act
		for (var i = 0; i < 3; i++)
		{
			await defaultCb.FailAsync(new InvalidOperationException($"Error {i}"));
			await pollyCb.FailAsync(new InvalidOperationException($"Error {i}"));
		}

		// Assert
		defaultCb.ConsecutiveFailures.ShouldBe(3);
		pollyCb.ConsecutiveFailures.ShouldBe(3);
	}

	[Fact]
	public async Task BothCircuitBreakersResetFailureCountOnSuccess()
	{
		// Arrange
		var options = new CircuitBreakerOptions { ConsecutiveFailureThreshold = 5, MinimumThroughput = 5 };
		var loggerFactory = NullLoggerFactory.Instance;

		var defaultCb = new CircuitBreakerPolicy(options, "default", loggerFactory.CreateLogger<CircuitBreakerPolicy>());
		var pollyCb = new PollyCircuitBreakerPolicyAdapter(options, "polly", loggerFactory.CreateLogger<PollyCircuitBreakerPolicyAdapter>());
		_disposables.Add(pollyCb);

		// Record failures
		await defaultCb.FailAsync().ConfigureAwait(false);
		await defaultCb.FailAsync().ConfigureAwait(false);
		await pollyCb.FailAsync().ConfigureAwait(false);
		await pollyCb.FailAsync().ConfigureAwait(false);

		// Act
		await defaultCb.SucceedAsync().ConfigureAwait(false);
		await pollyCb.SucceedAsync().ConfigureAwait(false);

		// Assert
		defaultCb.ConsecutiveFailures.ShouldBe(0);
		pollyCb.ConsecutiveFailures.ShouldBe(0);
	}

	[Fact]
	public async Task BothCircuitBreakersResetToClosedState()
	{
		// Arrange
		var options = new CircuitBreakerOptions { ConsecutiveFailureThreshold = 2, MinimumThroughput = 2 };
		var loggerFactory = NullLoggerFactory.Instance;

		var defaultCb = new CircuitBreakerPolicy(options, "default", loggerFactory.CreateLogger<CircuitBreakerPolicy>());
		var pollyCb = new PollyCircuitBreakerPolicyAdapter(options, "polly", loggerFactory.CreateLogger<PollyCircuitBreakerPolicyAdapter>());
		_disposables.Add(pollyCb);

		// Open the circuits by exceeding threshold
		await defaultCb.FailAsync().ConfigureAwait(false);
		await defaultCb.FailAsync().ConfigureAwait(false);
		await pollyCb.FailAsync().ConfigureAwait(false);
		await pollyCb.FailAsync().ConfigureAwait(false);

		// Act
		await defaultCb.ResetAsync(CancellationToken.None).ConfigureAwait(false);
		await pollyCb.ResetAsync(CancellationToken.None).ConfigureAwait(false);

		// Assert
		((int)defaultCb.State).ShouldBe((int)CircuitState.Closed);
		((int)await pollyCb.WaitForStateAsync(CircuitState.Closed).ConfigureAwait(false))
			.ShouldBe((int)Excalibur.Dispatch.Resilience.CircuitState.Closed);
		defaultCb.ConsecutiveFailures.ShouldBe(0);
		pollyCb.ConsecutiveFailures.ShouldBe(0);
	}

	[Fact]
	public async Task BothCircuitBreakersExecuteSuccessfully()
	{
		// Arrange
		var options = new CircuitBreakerOptions();
		var loggerFactory = NullLoggerFactory.Instance;

		var defaultCb = new CircuitBreakerPolicy(options, "default", loggerFactory.CreateLogger<CircuitBreakerPolicy>());
		var pollyCb = new PollyCircuitBreakerPolicyAdapter(options, "polly", loggerFactory.CreateLogger<PollyCircuitBreakerPolicyAdapter>());
		_disposables.Add(pollyCb);

		// Act
		var defaultResult = await defaultCb.ExecuteAsync(async ct =>
		{
			await global::Tests.Shared.Infrastructure.TestTiming.PauseAsync(1, ct).ConfigureAwait(false);
			return 42;
		}, CancellationToken.None).ConfigureAwait(false);

		var pollyResult = await pollyCb.ExecuteAsync(async ct =>
		{
			await global::Tests.Shared.Infrastructure.TestTiming.PauseAsync(1, ct).ConfigureAwait(false);
			return 42;
		}, CancellationToken.None).ConfigureAwait(false);

		// Assert
		defaultResult.ShouldBe(42);
		pollyResult.ShouldBe(42);
	}

	[Fact]
	public async Task BothCircuitBreakersRecordTheFailureOnException()
	{
		// Arrange
		var options = new CircuitBreakerOptions { ConsecutiveFailureThreshold = 10, MinimumThroughput = 10 };
		var loggerFactory = NullLoggerFactory.Instance;

		var defaultCb = new CircuitBreakerPolicy(options, "default", loggerFactory.CreateLogger<CircuitBreakerPolicy>());
		var pollyCb = new PollyCircuitBreakerPolicyAdapter(options, "polly", loggerFactory.CreateLogger<PollyCircuitBreakerPolicyAdapter>());
		_disposables.Add(pollyCb);

		// Act
		try
		{
			_ = await defaultCb.ExecuteAsync<int>(ct => throw new InvalidOperationException("Test"), CancellationToken.None).ConfigureAwait(false);
		}
		catch (InvalidOperationException)
		{
		}

		try
		{
			_ = await pollyCb.ExecuteAsync<int>(ct => throw new InvalidOperationException("Test"), CancellationToken.None).ConfigureAwait(false);
		}
		catch (InvalidOperationException)
		{
		}

		// Assert
		defaultCb.ConsecutiveFailures.ShouldBe(1);
		pollyCb.ConsecutiveFailures.ShouldBe(1);
	}

	[Fact]
	public async Task NeitherCircuitBreakerIsRetunedByMutatingTheOptionsAfterConstruction()
	{
		// CircuitBreakerOptions has settable properties, so a consumer can hold the instance it passed
		// and change it while the policies built from it are live. The two providers used to disagree
		// about what that means: the Polly adapter copies the values into a pipeline at construction, so
		// the mutation did nothing to it, while the core policy re-read the caller's instance on every
		// call and was silently retuned mid-flight. One instance, one mutation, two behaviours.
		//
		// The contract is SNAPSHOT-AT-CONSTRUCTION for both. This is the lock for it, and it is written
		// against observable behaviour rather than against the fields, because a doc note saying "do not
		// mutate after construction" does not settle it -- the setter is the thing that says you may.
		var options = new CircuitBreakerOptions
		{
			ConsecutiveFailureThreshold = 4,
			MinimumThroughput = 4,
			FailureRatio = 1.0,
			SamplingDuration = TimeSpan.FromSeconds(30),
			BreakDuration = TimeSpan.FromSeconds(30),
		};
		var loggerFactory = NullLoggerFactory.Instance;

		var defaultCb = new CircuitBreakerPolicy(options, "default", loggerFactory.CreateLogger<CircuitBreakerPolicy>());
		var pollyCb = new PollyCircuitBreakerPolicyAdapter(options, "polly", loggerFactory.CreateLogger<PollyCircuitBreakerPolicyAdapter>());
		_disposables.Add(pollyCb);

		// The mutation, after BOTH policies exist: every trigger halved.
		options.ConsecutiveFailureThreshold = 2;
		options.MinimumThroughput = 2;
		options.FailureRatio = 0.1;

		// SAFETY -- three failures is below the threshold both were CONSTRUCTED with and above the one the
		// mutated instance now carries. A provider reading the caller's instance live opens here.
		for (var i = 0; i < 3; i++)
		{
			await defaultCb.FailAsync().ConfigureAwait(false);
			await pollyCb.FailAsync().ConfigureAwait(false);
		}

		defaultCb.State.ShouldBe(CircuitState.Closed);
		pollyCb.State.ShouldBe(CircuitState.Closed);

		// LIVENESS -- the partner arm. Without it a policy that never opens at all would satisfy the
		// assertions above, and "the mutation was ignored" would be indistinguishable from "nothing works".
		// The fourth failure reaches the threshold the policies were constructed with, and both open.
		await defaultCb.FailAsync().ConfigureAwait(false);
		await pollyCb.FailAsync().ConfigureAwait(false);

		(await defaultCb.WaitForStateAsync(CircuitState.Open).ConfigureAwait(false)).ShouldBe(CircuitState.Open);
		(await pollyCb.WaitForStateAsync(CircuitState.Open).ConfigureAwait(false)).ShouldBe(CircuitState.Open);
	}

	[Fact]
	public async Task TheCoreBreakerHoldsItsConstructedBreakDurationWhenTheOptionsAreShortenedAfterwards()
	{
		// Partner to the test above, for the OTHER value the core policy used to re-read on every call.
		// ConsecutiveFailureThreshold decides WHEN the circuit opens; BreakDuration decides how long it
		// stays open, and a live read of it moved the half-open deadline underneath a circuit that was
		// already open. Both reads are gone, so both are locked.
		//
		// Scope, stated rather than implied: this arm binds the CORE provider only. The Polly adapter's
		// break duration lives inside a pipeline built at construction and it exposes no clock to inject,
		// so its snapshot is structural -- there is no reachable expression of a live re-read to assert
		// against. The core provider is the one that had the defect, and it is the one with a seam.
		var clock = new FakeTimeProvider();
		var constructedBreakDuration = TimeSpan.FromSeconds(30);
		var options = new CircuitBreakerOptions
		{
			ConsecutiveFailureThreshold = 2,
			BreakDuration = constructedBreakDuration,
		};

		var defaultCb = new CircuitBreakerPolicy(
			options,
			"default",
			NullLoggerFactory.Instance.CreateLogger<CircuitBreakerPolicy>(),
			shouldHandle: null,
			timeProvider: clock);

		await defaultCb.FailAsync().ConfigureAwait(false);
		await defaultCb.FailAsync().ConfigureAwait(false);
		defaultCb.State.ShouldBe(CircuitState.Open);

		// The mutation, while the circuit is already open: the break collapses to its shortest legal value.
		options.BreakDuration = TimeSpan.FromMilliseconds(500);

		// SAFETY -- one second is well past the MUTATED duration and nowhere near the constructed one.
		// A policy re-reading the caller's instance admits its half-open probe here.
		clock.Advance(TimeSpan.FromSeconds(1));
		defaultCb.State.ShouldBe(CircuitState.Open);

		// LIVENESS -- and it is not simply stuck open forever: the constructed deadline still arrives.
		clock.Advance(constructedBreakDuration);
		defaultCb.State.ShouldBe(CircuitState.HalfOpen);
	}
	#endregion Circuit Breaker Behavioral Equivalence

	#region Backoff Calculator Behavioral Equivalence

	[Fact]
	public void BothCalculatorsHandleZeroAttempt()
	{
		// Arrange
		var pollyCalc = new PollyBackoffCalculatorAdapter(
			DelayBackoffType.Exponential,
			TimeSpan.FromMilliseconds(100),
			TimeSpan.FromSeconds(30),
			false,
			2.0);

		// Act - Polly adapter is lenient and returns zero for invalid attempts
		var pollyDelay = pollyCalc.CalculateDelay(0);

		// Assert - Polly adapter gracefully handles edge cases
		pollyDelay.ShouldBe(TimeSpan.Zero);

		// Note: ExponentialBackoffCalculator throws ArgumentOutOfRangeException for attempt <= 0
		// This is a known behavioral difference - Polly adapter is more lenient
	}

	[Fact]
	public void BothCalculatorsHandleNegativeAttempt()
	{
		// Arrange
		var pollyCalc = new PollyBackoffCalculatorAdapter(
			DelayBackoffType.Exponential,
			TimeSpan.FromMilliseconds(100),
			TimeSpan.FromSeconds(30),
			false,
			2.0);

		// Act - Polly adapter is lenient and returns zero for invalid attempts
		var pollyDelay = pollyCalc.CalculateDelay(-1);

		// Assert - Polly adapter gracefully handles edge cases
		pollyDelay.ShouldBe(TimeSpan.Zero);

		// Note: ExponentialBackoffCalculator throws ArgumentOutOfRangeException for attempt <= 0
		// This is a known behavioral difference - Polly adapter is more lenient
	}

	[Fact]
	public void BothCalculatorsRespectMaxDelay()
	{
		// Arrange
		var maxDelay = TimeSpan.FromMilliseconds(500);
		var defaultCalc = new ExponentialBackoffCalculator(
			TimeSpan.FromMilliseconds(100),
			maxDelay,
			2.0,
			false);

		var pollyCalc = new PollyBackoffCalculatorAdapter(
			DelayBackoffType.Exponential,
			TimeSpan.FromMilliseconds(100),
			maxDelay,
			false,
			2.0);

		// Act - attempt 10 would give huge delay without cap
		var defaultDelay = defaultCalc.CalculateDelay(10);
		var pollyDelay = pollyCalc.CalculateDelay(10);

		// Assert
		defaultDelay.ShouldBeLessThanOrEqualTo(maxDelay);
		pollyDelay.ShouldBeLessThanOrEqualTo(maxDelay);
	}

	[Fact]
	public void BothCalculatorsProduceExponentialGrowthWithoutJitter()
	{
		// Arrange
		var defaultCalc = new ExponentialBackoffCalculator(
			TimeSpan.FromMilliseconds(100),
			TimeSpan.FromSeconds(30),
			2.0,
			false);

		var pollyCalc = new PollyBackoffCalculatorAdapter(
			DelayBackoffType.Exponential,
			TimeSpan.FromMilliseconds(100),
			TimeSpan.FromSeconds(30),
			false,
			2.0);

		// Act & Assert - both should follow 100, 200, 400, 800, 1600 pattern
		for (var attempt = 1; attempt <= 5; attempt++)
		{
			var expectedMs = 100 * Math.Pow(2, attempt - 1);
			var defaultDelay = defaultCalc.CalculateDelay(attempt);
			var pollyDelay = pollyCalc.CalculateDelay(attempt);

			defaultDelay.TotalMilliseconds.ShouldBe(expectedMs, 0.1);
			pollyDelay.TotalMilliseconds.ShouldBe(expectedMs, 0.1);
		}
	}

	[Fact]
	public void BothCalculatorsProducePositiveDelaysForPositiveAttempts()
	{
		// Arrange
		var defaultCalc = new ExponentialBackoffCalculator(
			TimeSpan.FromMilliseconds(100),
			TimeSpan.FromSeconds(30),
			2.0,
			true);

		var pollyCalc = new PollyBackoffCalculatorAdapter(
			DelayBackoffType.Exponential,
			TimeSpan.FromMilliseconds(100),
			TimeSpan.FromSeconds(30),
			true,
			2.0);

		// Act & Assert
		for (var attempt = 1; attempt <= 10; attempt++)
		{
			var defaultDelay = defaultCalc.CalculateDelay(attempt);
			var pollyDelay = pollyCalc.CalculateDelay(attempt);

			defaultDelay.ShouldBeGreaterThan(TimeSpan.Zero);
			pollyDelay.ShouldBeGreaterThan(TimeSpan.Zero);
		}
	}

	#endregion Backoff Calculator Behavioral Equivalence

	#region Transport Registry Behavioral Equivalence

	[Fact]
	public void BothRegistriesStartEmpty()
	{
		// Arrange
		var options = new CircuitBreakerOptions();
		var loggerFactory = NullLoggerFactory.Instance;

		var defaultRegistry = new TransportCircuitBreakerRegistry(options, loggerFactory);
		var pollyRegistry = new PollyTransportCircuitBreakerRegistry(options, loggerFactory.CreateLogger<PollyTransportCircuitBreakerRegistry>());
		_disposables.Add(pollyRegistry);

		// Assert
		defaultRegistry.Count.ShouldBe(0);
		pollyRegistry.Count.ShouldBe(0);
	}

	[Fact]
	public void BothRegistriesCreateOnGetOrCreate()
	{
		// Arrange
		var options = new CircuitBreakerOptions();
		var loggerFactory = NullLoggerFactory.Instance;

		var defaultRegistry = new TransportCircuitBreakerRegistry(options, loggerFactory);
		var pollyRegistry = new PollyTransportCircuitBreakerRegistry(options, loggerFactory.CreateLogger<PollyTransportCircuitBreakerRegistry>());
		_disposables.Add(pollyRegistry);

		// Act
		var defaultCb = defaultRegistry.GetOrCreate("rabbitmq");
		var pollyCb = pollyRegistry.GetOrCreate("rabbitmq");

		// Assert
		_ = defaultCb.ShouldNotBeNull();
		_ = pollyCb.ShouldNotBeNull();
		defaultRegistry.Count.ShouldBe(1);
		pollyRegistry.Count.ShouldBe(1);
	}

	[Fact]
	public void BothRegistriesReturnSameInstanceOnRepeatedCalls()
	{
		// Arrange
		var options = new CircuitBreakerOptions();
		var loggerFactory = NullLoggerFactory.Instance;

		var defaultRegistry = new TransportCircuitBreakerRegistry(options, loggerFactory);
		var pollyRegistry = new PollyTransportCircuitBreakerRegistry(options, loggerFactory.CreateLogger<PollyTransportCircuitBreakerRegistry>());
		_disposables.Add(pollyRegistry);

		// Act
		var defaultCb1 = defaultRegistry.GetOrCreate("rabbitmq");
		var defaultCb2 = defaultRegistry.GetOrCreate("rabbitmq");
		var pollyCb1 = pollyRegistry.GetOrCreate("rabbitmq");
		var pollyCb2 = pollyRegistry.GetOrCreate("rabbitmq");

		// Assert
		defaultCb1.ShouldBeSameAs(defaultCb2);
		pollyCb1.ShouldBeSameAs(pollyCb2);
	}

	[Fact]
	public void BothRegistriesAreCaseInsensitive()
	{
		// Arrange
		var options = new CircuitBreakerOptions();
		var loggerFactory = NullLoggerFactory.Instance;

		var defaultRegistry = new TransportCircuitBreakerRegistry(options, loggerFactory);
		var pollyRegistry = new PollyTransportCircuitBreakerRegistry(options, loggerFactory.CreateLogger<PollyTransportCircuitBreakerRegistry>());
		_disposables.Add(pollyRegistry);

		// Act
		var defaultCb1 = defaultRegistry.GetOrCreate("RabbitMQ");
		var defaultCb2 = defaultRegistry.GetOrCreate("rabbitmq");
		var pollyCb1 = pollyRegistry.GetOrCreate("RabbitMQ");
		var pollyCb2 = pollyRegistry.GetOrCreate("rabbitmq");

		// Assert
		defaultCb1.ShouldBeSameAs(defaultCb2);
		pollyCb1.ShouldBeSameAs(pollyCb2);
		defaultRegistry.Count.ShouldBe(1);
		pollyRegistry.Count.ShouldBe(1);
	}

	[Fact]
	public async Task BothRegistriesIsolateTransports()
	{
		// Arrange
		var options = new CircuitBreakerOptions();
		var loggerFactory = NullLoggerFactory.Instance;

		var defaultRegistry = new TransportCircuitBreakerRegistry(options, loggerFactory);
		var pollyRegistry = new PollyTransportCircuitBreakerRegistry(options, loggerFactory.CreateLogger<PollyTransportCircuitBreakerRegistry>());
		_disposables.Add(pollyRegistry);

		// Act
		var defaultRabbitmq = defaultRegistry.GetOrCreate("rabbitmq");
		var defaultKafka = defaultRegistry.GetOrCreate("kafka");
		var pollyRabbitmq = pollyRegistry.GetOrCreate("rabbitmq");
		var pollyKafka = pollyRegistry.GetOrCreate("kafka");

		await defaultRabbitmq.FailAsync().ConfigureAwait(false);
		await pollyRabbitmq.FailAsync().ConfigureAwait(false);

		// Assert - kafka should be unaffected
		((ICircuitBreakerDiagnostics)defaultRabbitmq).ConsecutiveFailures.ShouldBe(1);
		((ICircuitBreakerDiagnostics)defaultKafka).ConsecutiveFailures.ShouldBe(0);
		((ICircuitBreakerDiagnostics)pollyRabbitmq).ConsecutiveFailures.ShouldBe(1);
		((ICircuitBreakerDiagnostics)pollyKafka).ConsecutiveFailures.ShouldBe(0);
	}

	[Fact]
	public async Task BothRegistriesResetAll()
	{
		// Arrange
		var options = new CircuitBreakerOptions();
		var loggerFactory = NullLoggerFactory.Instance;

		var defaultRegistry = new TransportCircuitBreakerRegistry(options, loggerFactory);
		var pollyRegistry = new PollyTransportCircuitBreakerRegistry(options, loggerFactory.CreateLogger<PollyTransportCircuitBreakerRegistry>());
		_disposables.Add(pollyRegistry);

		// Create and add failures
		var defaultRabbitmq = defaultRegistry.GetOrCreate("rabbitmq");
		var defaultKafka = defaultRegistry.GetOrCreate("kafka");
		var pollyRabbitmq = pollyRegistry.GetOrCreate("rabbitmq");
		var pollyKafka = pollyRegistry.GetOrCreate("kafka");

		await defaultRabbitmq.FailAsync().ConfigureAwait(false);
		await defaultKafka.FailAsync().ConfigureAwait(false);
		await pollyRabbitmq.FailAsync().ConfigureAwait(false);
		await pollyKafka.FailAsync().ConfigureAwait(false);

		// Act
		await defaultRegistry.ResetAllAsync(CancellationToken.None).ConfigureAwait(false);
		await pollyRegistry.ResetAllAsync(CancellationToken.None).ConfigureAwait(false);

		// Assert
		((ICircuitBreakerDiagnostics)defaultRabbitmq).ConsecutiveFailures.ShouldBe(0);
		((ICircuitBreakerDiagnostics)defaultKafka).ConsecutiveFailures.ShouldBe(0);
		((ICircuitBreakerDiagnostics)pollyRabbitmq).ConsecutiveFailures.ShouldBe(0);
		((ICircuitBreakerDiagnostics)pollyKafka).ConsecutiveFailures.ShouldBe(0);
	}

	[Fact]
	public void BothRegistriesRemoveCircuitBreakers()
	{
		// Arrange
		var options = new CircuitBreakerOptions();
		var loggerFactory = NullLoggerFactory.Instance;

		var defaultRegistry = new TransportCircuitBreakerRegistry(options, loggerFactory);
		var pollyRegistry = new PollyTransportCircuitBreakerRegistry(options, loggerFactory.CreateLogger<PollyTransportCircuitBreakerRegistry>());
		_disposables.Add(pollyRegistry);

		_ = defaultRegistry.GetOrCreate("rabbitmq");
		_ = pollyRegistry.GetOrCreate("rabbitmq");

		// Act
		var defaultRemoved = defaultRegistry.Remove("rabbitmq");
		var pollyRemoved = pollyRegistry.Remove("rabbitmq");

		// Assert
		defaultRemoved.ShouldBeTrue();
		pollyRemoved.ShouldBeTrue();
		defaultRegistry.Count.ShouldBe(0);
		pollyRegistry.Count.ShouldBe(0);
	}

	#endregion Transport Registry Behavioral Equivalence
}

