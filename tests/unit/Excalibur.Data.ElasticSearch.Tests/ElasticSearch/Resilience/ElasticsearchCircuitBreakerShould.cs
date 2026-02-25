// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch;
using Excalibur.Data.ElasticSearch.Resilience;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Data.Tests.ElasticSearch.Resilience;

[Trait("Category", "Unit")]
[Trait("Component", "Data")]
public sealed class ElasticsearchCircuitBreakerShould : IDisposable
{
	private readonly ElasticsearchCircuitBreaker _sut;

	public ElasticsearchCircuitBreakerShould()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new ElasticsearchConfigurationOptions
		{
			Resilience = new ElasticsearchResilienceOptions
			{
				CircuitBreaker = new CircuitBreakerOptions
				{
					Enabled = true,
					FailureThreshold = 3,
					MinimumThroughput = 5,
					BreakDuration = TimeSpan.FromSeconds(5),
					SamplingDuration = TimeSpan.FromMinutes(1),
					FailureRateThreshold = 0.5,
				},
			},
		});

		_sut = new ElasticsearchCircuitBreaker(options, NullLogger<ElasticsearchCircuitBreaker>.Instance);
	}

	[Fact]
	public void StartInClosedState()
	{
		// Assert
		_sut.State.ShouldBe(CircuitBreakerState.Closed);
		_sut.IsOpen.ShouldBeFalse();
		_sut.IsHalfOpen.ShouldBeFalse();
	}

	[Fact]
	public void HaveZeroConsecutiveFailuresInitially()
	{
		// Assert
		_sut.ConsecutiveFailures.ShouldBe(0);
	}

	[Fact]
	public void HaveZeroFailureRateInitially()
	{
		// Assert
		_sut.FailureRate.ShouldBe(0.0);
	}

	[Fact]
	public async Task RecordSuccessAndResetConsecutiveFailures()
	{
		// Arrange — record some failures first
		await _sut.RecordFailureAsync();
		await _sut.RecordFailureAsync();
		_sut.ConsecutiveFailures.ShouldBe(2);

		// Act
		await _sut.RecordSuccessAsync();

		// Assert
		_sut.ConsecutiveFailures.ShouldBe(0);
	}

	[Fact]
	public async Task IncrementConsecutiveFailuresOnFailure()
	{
		// Act
		await _sut.RecordFailureAsync();
		await _sut.RecordFailureAsync();

		// Assert
		_sut.ConsecutiveFailures.ShouldBe(2);
	}

	[Fact]
	public async Task OpenCircuitAfterThresholdExceeded()
	{
		// Act — exceed failure threshold of 3
		await _sut.RecordFailureAsync();
		await _sut.RecordFailureAsync();
		await _sut.RecordFailureAsync();

		// Assert
		_sut.State.ShouldBe(CircuitBreakerState.Open);
		_sut.IsOpen.ShouldBeTrue();
	}

	[Fact]
	public async Task ExecuteOperationWhenCircuitIsDisabled()
	{
		// Arrange
		var options = Microsoft.Extensions.Options.Options.Create(new ElasticsearchConfigurationOptions
		{
			Resilience = new ElasticsearchResilienceOptions
			{
				CircuitBreaker = new CircuitBreakerOptions { Enabled = false },
			},
		});

		using var sut = new ElasticsearchCircuitBreaker(options, NullLogger<ElasticsearchCircuitBreaker>.Instance);

		// Act
		var result = await sut.ExecuteAsync(() => Task.FromResult(42), CancellationToken.None);

		// Assert
		result.ShouldBe(42);
	}

	[Fact]
	public async Task ThrowWhenCircuitIsOpenDuringExecute()
	{
		// Arrange — open the circuit
		await _sut.RecordFailureAsync();
		await _sut.RecordFailureAsync();
		await _sut.RecordFailureAsync();
		_sut.IsOpen.ShouldBeTrue();

		// Act & Assert
		await Should.ThrowAsync<InvalidOperationException>(
			() => _sut.ExecuteAsync(() => Task.FromResult(42), CancellationToken.None));
	}

	[Fact]
	public async Task RecordSuccessOnSuccessfulExecution()
	{
		// Arrange
		await _sut.RecordFailureAsync();
		_sut.ConsecutiveFailures.ShouldBe(1);

		// Act
		var result = await _sut.ExecuteAsync(() => Task.FromResult(42), CancellationToken.None);

		// Assert
		result.ShouldBe(42);
		_sut.ConsecutiveFailures.ShouldBe(0);
	}

	[Fact]
	public async Task RecordFailureOnFailedExecution()
	{
		// Act & Assert
		await Should.ThrowAsync<InvalidOperationException>(
			() => _sut.ExecuteAsync<int>(
				() => throw new InvalidOperationException("test"),
				CancellationToken.None));

		_sut.ConsecutiveFailures.ShouldBe(1);
	}

	[Fact]
	public async Task ThrowObjectDisposedExceptionAfterDispose()
	{
		// Arrange
		_sut.Dispose();

		// Act & Assert
		await Should.ThrowAsync<ObjectDisposedException>(
			() => _sut.ExecuteAsync(() => Task.FromResult(42), CancellationToken.None));
	}

	[Fact]
	public async Task NotRecordWhenDisabled()
	{
		// Arrange
		var options = Microsoft.Extensions.Options.Options.Create(new ElasticsearchConfigurationOptions
		{
			Resilience = new ElasticsearchResilienceOptions
			{
				CircuitBreaker = new CircuitBreakerOptions { Enabled = false },
			},
		});

		using var sut = new ElasticsearchCircuitBreaker(options, NullLogger<ElasticsearchCircuitBreaker>.Instance);

		// Act
		await sut.RecordSuccessAsync();
		await sut.RecordFailureAsync();

		// Assert — should remain at initial state since recording is disabled
		sut.State.ShouldBe(CircuitBreakerState.Closed);
	}

	[Fact]
	public void ThrowWhenOptionsIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new ElasticsearchCircuitBreaker(null!, NullLogger<ElasticsearchCircuitBreaker>.Instance));
	}

	[Fact]
	public void ThrowWhenLoggerIsNull()
	{
		// Arrange
		var options = Microsoft.Extensions.Options.Options.Create(new ElasticsearchConfigurationOptions());

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new ElasticsearchCircuitBreaker(options, null!));
	}

	[Fact]
	public void DisposeMultipleTimesWithoutError()
	{
		// Act & Assert — should not throw
		_sut.Dispose();
		_sut.Dispose();
	}

	[Fact]
	public async Task TransitionFromHalfOpenToClosedOnSuccess()
	{
		// Arrange — open the circuit
		await _sut.RecordFailureAsync();
		await _sut.RecordFailureAsync();
		await _sut.RecordFailureAsync();
		_sut.IsOpen.ShouldBeTrue();

		// Wait for the circuit to transition to half-open (break duration = 5s)
		// We use a circuit with very short break duration for this test
		var options = Microsoft.Extensions.Options.Options.Create(new ElasticsearchConfigurationOptions
		{
			Resilience = new ElasticsearchResilienceOptions
			{
				CircuitBreaker = new CircuitBreakerOptions
				{
					Enabled = true,
					FailureThreshold = 1,
					BreakDuration = TimeSpan.FromMilliseconds(50),
					SamplingDuration = TimeSpan.FromMinutes(1),
				},
			},
		});

		using var sut = new ElasticsearchCircuitBreaker(options, NullLogger<ElasticsearchCircuitBreaker>.Instance);

		// Open the circuit
		await sut.RecordFailureAsync();
		sut.IsOpen.ShouldBeTrue();

		// Wait for break duration to elapse
		await global::Tests.Shared.Infrastructure.TestTiming.PauseAsync(100);

		// Check that state transitioned to HalfOpen
		sut.State.ShouldBe(CircuitBreakerState.HalfOpen);

		// Record success to transition to Closed
		await sut.RecordSuccessAsync();
		sut.State.ShouldBe(CircuitBreakerState.Closed);
	}

	[Fact]
	public async Task ThrowNullOperationInExecuteAsync()
	{
		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(
			() => _sut.ExecuteAsync<int>(null!, CancellationToken.None));
	}

	public void Dispose() => _sut.Dispose();
}

