// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Resilience;
using Excalibur.Dispatch.Options.Resilience;
using Excalibur.Dispatch.Resilience.Polly;
using CircuitState = Excalibur.Dispatch.Resilience.CircuitState;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using Polly;

namespace Excalibur.Dispatch.Middleware.Tests.Resilience;

/// <summary>
/// Integration tests verifying behavioral equivalence between Polly adapters and default implementations.
/// Sprint 45 (bd-9xis): Behavioral equivalence tests.
/// </summary>
[Trait("Category", "Unit")]
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
		var options = new CircuitBreakerOptions { FailureThreshold = 5 };
		var loggerFactory = NullLoggerFactory.Instance;

		var defaultCb = new CircuitBreakerPolicy(options, "default", loggerFactory.CreateLogger<CircuitBreakerPolicy>());
		var pollyCb = new PollyCircuitBreakerPolicyAdapter(options, "polly", loggerFactory.CreateLogger<PollyCircuitBreakerPolicyAdapter>());
		_disposables.Add(pollyCb);

		// Assert
		((int)defaultCb.State).ShouldBe((int)CircuitState.Closed);
		((int)pollyCb.State).ShouldBe((int)Excalibur.Dispatch.Resilience.CircuitState.Closed);
	}

	[Fact]
	public void BothCircuitBreakersTrackConsecutiveFailures()
	{
		// Arrange
		var options = new CircuitBreakerOptions { FailureThreshold = 5 };
		var loggerFactory = NullLoggerFactory.Instance;

		var defaultCb = new CircuitBreakerPolicy(options, "default", loggerFactory.CreateLogger<CircuitBreakerPolicy>());
		var pollyCb = new PollyCircuitBreakerPolicyAdapter(options, "polly", loggerFactory.CreateLogger<PollyCircuitBreakerPolicyAdapter>());
		_disposables.Add(pollyCb);

		// Act
		for (var i = 0; i < 3; i++)
		{
			defaultCb.RecordFailure(new InvalidOperationException($"Error {i}"));
			pollyCb.RecordFailure(new InvalidOperationException($"Error {i}"));
		}

		// Assert
		defaultCb.ConsecutiveFailures.ShouldBe(3);
		pollyCb.ConsecutiveFailures.ShouldBe(3);
	}

	[Fact]
	public void BothCircuitBreakersResetFailureCountOnSuccess()
	{
		// Arrange
		var options = new CircuitBreakerOptions { FailureThreshold = 5 };
		var loggerFactory = NullLoggerFactory.Instance;

		var defaultCb = new CircuitBreakerPolicy(options, "default", loggerFactory.CreateLogger<CircuitBreakerPolicy>());
		var pollyCb = new PollyCircuitBreakerPolicyAdapter(options, "polly", loggerFactory.CreateLogger<PollyCircuitBreakerPolicyAdapter>());
		_disposables.Add(pollyCb);

		// Record failures
		defaultCb.RecordFailure();
		defaultCb.RecordFailure();
		pollyCb.RecordFailure();
		pollyCb.RecordFailure();

		// Act
		defaultCb.RecordSuccess();
		pollyCb.RecordSuccess();

		// Assert
		defaultCb.ConsecutiveFailures.ShouldBe(0);
		pollyCb.ConsecutiveFailures.ShouldBe(0);
	}

	[Fact]
	public void BothCircuitBreakersResetToClosedState()
	{
		// Arrange
		var options = new CircuitBreakerOptions { FailureThreshold = 2 };
		var loggerFactory = NullLoggerFactory.Instance;

		var defaultCb = new CircuitBreakerPolicy(options, "default", loggerFactory.CreateLogger<CircuitBreakerPolicy>());
		var pollyCb = new PollyCircuitBreakerPolicyAdapter(options, "polly", loggerFactory.CreateLogger<PollyCircuitBreakerPolicyAdapter>());
		_disposables.Add(pollyCb);

		// Open the circuits by exceeding threshold
		defaultCb.RecordFailure();
		defaultCb.RecordFailure();
		pollyCb.RecordFailure();
		pollyCb.RecordFailure();

		// Act
		defaultCb.Reset();
		pollyCb.Reset();

		// Assert
		((int)defaultCb.State).ShouldBe((int)CircuitState.Closed);
		((int)pollyCb.State).ShouldBe((int)Excalibur.Dispatch.Resilience.CircuitState.Closed);
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
	public async Task BothCircuitBreakersRecordFailureOnException()
	{
		// Arrange
		var options = new CircuitBreakerOptions { FailureThreshold = 10 };
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
	public void BothRegistriesIsolateTransports()
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

		defaultRabbitmq.RecordFailure();
		pollyRabbitmq.RecordFailure();

		// Assert - kafka should be unaffected
		((ICircuitBreakerDiagnostics)defaultRabbitmq).ConsecutiveFailures.ShouldBe(1);
		((ICircuitBreakerDiagnostics)defaultKafka).ConsecutiveFailures.ShouldBe(0);
		((ICircuitBreakerDiagnostics)pollyRabbitmq).ConsecutiveFailures.ShouldBe(1);
		((ICircuitBreakerDiagnostics)pollyKafka).ConsecutiveFailures.ShouldBe(0);
	}

	[Fact]
	public void BothRegistriesResetAll()
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

		defaultRabbitmq.RecordFailure();
		defaultKafka.RecordFailure();
		pollyRabbitmq.RecordFailure();
		pollyKafka.RecordFailure();

		// Act
		defaultRegistry.ResetAll();
		pollyRegistry.ResetAll();

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

