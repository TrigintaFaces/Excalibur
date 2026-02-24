// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.CloudNative;
using Excalibur.Dispatch.Options.Resilience;

namespace Excalibur.Dispatch.Tests.Messaging.CloudNative;

/// <summary>
///     Tests for the <see cref="CircuitBreakerPattern" /> class.
/// </summary>
[Trait("Category", "Unit")]
public sealed class CircuitBreakerPatternShould : IAsyncDisposable
{
	private readonly CircuitBreakerOptions _options;
	private readonly CircuitBreakerPattern _sut;

	public CircuitBreakerPatternShould()
	{
		_options = new CircuitBreakerOptions
		{
			FailureThreshold = 3,
			SuccessThreshold = 2,
			OpenDuration = TimeSpan.FromMilliseconds(100),
			OperationTimeout = TimeSpan.FromSeconds(5),
			MaxHalfOpenTests = 2,
		};
		_sut = new CircuitBreakerPattern("test-breaker", _options);
	}

	[Fact]
	public void ThrowForNullName() =>
		Should.Throw<ArgumentNullException>(() => new CircuitBreakerPattern(null!, _options));

	[Fact]
	public void ThrowForNullOptions() =>
		Should.Throw<ArgumentNullException>(() => new CircuitBreakerPattern("test", null!));

	[Fact]
	public void StartInClosedState()
	{
		_sut.State.ShouldBe(ResilienceState.Closed);
		_sut.Name.ShouldBe("test-breaker");
	}

	[Fact]
	public void HaveHealthyStatusWhenClosed()
	{
		_sut.HealthStatus.ShouldBe(PatternHealthStatus.Healthy);
	}

	[Fact]
	public void ExposeConfiguration()
	{
		var config = _sut.Configuration;
		config.ShouldContainKey("FailureThreshold");
		config["FailureThreshold"].ShouldBe(3);
	}

	[Fact]
	public async Task ExecuteOperationSuccessfully()
	{
		var result = await _sut.ExecuteAsync(
			() => Task.FromResult(42),
			CancellationToken.None).ConfigureAwait(false);
		result.ShouldBe(42);
		_sut.State.ShouldBe(ResilienceState.Closed);
	}

	[Fact]
	public async Task TransitionToOpenAfterFailureThreshold()
	{
		for (var i = 0; i < _options.FailureThreshold; i++)
		{
			await _sut.ExecuteAsync<int>(
				() => throw new InvalidOperationException("fail"),
				() => Task.FromResult(-1),
				CancellationToken.None).ConfigureAwait(false);
		}

		_sut.State.ShouldBe(ResilienceState.Open);
		_sut.HealthStatus.ShouldBe(PatternHealthStatus.Unhealthy);
	}

	[Fact]
	public async Task ExecuteFallbackWhenOpen()
	{
		// Trip the breaker
		for (var i = 0; i < _options.FailureThreshold; i++)
		{
			await _sut.ExecuteAsync<int>(
				() => throw new InvalidOperationException("fail"),
				() => Task.FromResult(-1),
				CancellationToken.None).ConfigureAwait(false);
		}

		// Next call should use fallback
		var result = await _sut.ExecuteAsync(
			() => Task.FromResult(42),
			() => Task.FromResult(-999),
			CancellationToken.None).ConfigureAwait(false);

		result.ShouldBe(-999);
	}

	[Fact]
	public async Task ThrowCircuitBreakerOpenExceptionWhenOpenAndNoFallback()
	{
		// Trip the breaker
		for (var i = 0; i < _options.FailureThreshold; i++)
		{
			await _sut.ExecuteAsync<int>(
				() => throw new InvalidOperationException("fail"),
				() => Task.FromResult(-1),
				CancellationToken.None).ConfigureAwait(false);
		}

		await Should.ThrowAsync<CircuitBreakerOpenException>(
			() => _sut.ExecuteAsync(() => Task.FromResult(42), CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task TransitionToHalfOpenAfterOpenDuration()
	{
		// Trip the breaker
		for (var i = 0; i < _options.FailureThreshold; i++)
		{
			await _sut.ExecuteAsync<int>(
				() => throw new InvalidOperationException("fail"),
				() => Task.FromResult(-1),
				CancellationToken.None).ConfigureAwait(false);
		}

		_sut.State.ShouldBe(ResilienceState.Open);

		// Wait for open duration
		await global::Tests.Shared.Infrastructure.TestTiming.DelayAsync(_options.OpenDuration + TimeSpan.FromMilliseconds(50)).ConfigureAwait(false);

		// Next call should allow through (transition to HalfOpen)
		var result = await _sut.ExecuteAsync(
			() => Task.FromResult(42),
			() => Task.FromResult(-1),
			CancellationToken.None).ConfigureAwait(false);

		result.ShouldBe(42);
		// After success in half-open, if not enough successes yet, should remain half-open or transition
		(_sut.State == ResilienceState.HalfOpen || _sut.State == ResilienceState.Closed).ShouldBeTrue();
	}

	[Fact]
	public void ResetToClosedState()
	{
		_sut.Reset();
		_sut.State.ShouldBe(ResilienceState.Closed);
	}

	[Fact]
	public async Task TrackMetrics()
	{
		await _sut.ExecuteAsync(
			() => Task.FromResult(1),
			CancellationToken.None).ConfigureAwait(false);

		var metrics = _sut.GetCircuitBreakerMetrics();
		metrics.TotalRequests.ShouldBeGreaterThan(0);
		metrics.SuccessfulRequests.ShouldBeGreaterThan(0);
		metrics.CurrentState.ShouldBe(ResilienceState.Closed);
	}

	[Fact]
	public async Task TrackPatternMetrics()
	{
		await _sut.ExecuteAsync(
			() => Task.FromResult(1),
			CancellationToken.None).ConfigureAwait(false);

		var metrics = _sut.GetMetrics();
		metrics.TotalOperations.ShouldBeGreaterThan(0);
		metrics.SuccessfulOperations.ShouldBeGreaterThan(0);
	}

	[Fact]
	public async Task InitializeAndStartAndStop()
	{
		var config = new Dictionary<string, object>(StringComparer.Ordinal);
		await _sut.InitializeAsync(config, CancellationToken.None).ConfigureAwait(false);
		await _sut.StartAsync(CancellationToken.None).ConfigureAwait(false);
		await _sut.StopAsync(CancellationToken.None).ConfigureAwait(false);
	}

	[Fact]
	public void SubscribeAndUnsubscribeObservers()
	{
		var observer = A.Fake<IPatternObserver>();

		Should.NotThrow(() => _sut.Subscribe(observer));
		Should.NotThrow(() => _sut.Unsubscribe(observer));
	}

	[Fact]
	public void ThrowForNullObserverOnSubscribe() =>
		Should.Throw<ArgumentNullException>(() => _sut.Subscribe(null!));

	[Fact]
	public void ThrowForNullObserverOnUnsubscribe() =>
		Should.Throw<ArgumentNullException>(() => _sut.Unsubscribe(null!));

	[Fact]
	public async Task ThrowForNullOperation() =>
		await Should.ThrowAsync<ArgumentNullException>(
			() => _sut.ExecuteAsync<int>(null!, () => Task.FromResult(0), CancellationToken.None)).ConfigureAwait(false);

	[Fact]
	public async Task ThrowForNullFallback() =>
		await Should.ThrowAsync<ArgumentNullException>(
			() => _sut.ExecuteAsync(() => Task.FromResult(0), null!, CancellationToken.None)).ConfigureAwait(false);

	public async ValueTask DisposeAsync() => await _sut.DisposeAsync().ConfigureAwait(false);
}
