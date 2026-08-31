// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Resilience.Polly;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;

using MsOptions = Microsoft.Extensions.Options.Options;

using CircuitState = Excalibur.Dispatch.Resilience.CircuitState;

namespace Excalibur.Dispatch.Middleware.Tests.Resilience;

/// <summary>
/// Locks the <see cref="SemaphoreSlim"/> gate that serialises one breaker instance's counter updates.
/// </summary>
/// <remarks>
/// Each assertion is on the DECISION the counters drive — the circuit opening — not on the presence of a
/// persisted blob. A blob-existence assertion cannot fail when increments are lost: the write still
/// happens, it just carries a smaller number. Reaching the threshold cannot be faked, so a dropped
/// increment leaves the circuit closed and the test red.
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Resilience)]
public sealed class DistributedCircuitBreakerConcurrencyShould : IAsyncDisposable
{
	private DistributedCircuitBreaker? _circuitBreaker;

	public async ValueTask DisposeAsync()
	{
		if (_circuitBreaker != null)
		{
			await _circuitBreaker.DisposeAsync();
			_circuitBreaker = null;
		}
	}

	[Fact]
	public async Task Count_every_concurrent_failure_so_the_threshold_is_reached()
	{
		const int concurrency = 50;
		_circuitBreaker = CreateCircuitBreaker(
			"failure-concurrency",
			new DistributedCircuitBreakerOptions
			{
				// Reaching this requires all 50 increments to survive. One lost increment leaves the
				// circuit closed.
				ConsecutiveFailureThreshold = concurrency,
				MinimumThroughput = int.MaxValue,       // isolate the consecutive-failure arm
				SyncInterval = System.Threading.Timeout.InfiniteTimeSpan,
				BreakDuration = TimeSpan.FromMinutes(5),
			});

		await Task.WhenAll(Enumerable.Range(0, concurrency)
			.Select(_ => _circuitBreaker.RecordFailureAsync(
				CancellationToken.None, new InvalidOperationException("test")))
			.ToArray()).ConfigureAwait(false);

		(await _circuitBreaker.GetStateAsync(CancellationToken.None)).ShouldBe(
			CircuitState.Open,
			"all 50 concurrent failure recordings must be counted; a lost increment never reaches the threshold");
	}

	[Fact]
	public async Task Count_every_concurrent_attempt_in_the_rolling_window()
	{
		const int concurrency = 40;
		_circuitBreaker = CreateCircuitBreaker(
			"window-concurrency",
			new DistributedCircuitBreakerOptions
			{
				ConsecutiveFailureThreshold = int.MaxValue, // isolate the windowed-ratio arm
				MinimumThroughput = concurrency,            // needs every attempt to land in the window
				FailureRatio = 1.0,
				SamplingDuration = TimeSpan.FromMinutes(5),
				SyncInterval = System.Threading.Timeout.InfiniteTimeSpan,
				BreakDuration = TimeSpan.FromMinutes(5),
			});

		await Task.WhenAll(Enumerable.Range(0, concurrency)
			.Select(_ => _circuitBreaker.RecordFailureAsync(
				CancellationToken.None, new InvalidOperationException("test")))
			.ToArray()).ConfigureAwait(false);

		(await _circuitBreaker.GetStateAsync(CancellationToken.None)).ShouldBe(
			CircuitState.Open,
			"every concurrent attempt must land in the rolling window; a lost bucket write never reaches MinimumThroughput");
	}

	[Fact]
	public async Task Survive_interleaved_success_and_failure_without_corruption()
	{
		_circuitBreaker = CreateCircuitBreaker(
			"interleaved-test",
			new DistributedCircuitBreakerOptions
			{
				ConsecutiveFailureThreshold = 200,
				SuccessThresholdToClose = 200,
				MinimumThroughput = int.MaxValue,
				SyncInterval = System.Threading.Timeout.InfiniteTimeSpan,
			});

		var successes = Enumerable.Range(0, 25)
			.Select(_ => _circuitBreaker.RecordSuccessAsync(CancellationToken.None));
		var failures = Enumerable.Range(0, 25)
			.Select(_ => _circuitBreaker.RecordFailureAsync(
				CancellationToken.None, new InvalidOperationException("test")));

		await Task.WhenAll(successes.Concat(failures)).ConfigureAwait(false);

		// Neither threshold is anywhere near reached, so the circuit must still be closed — a corrupted
		// counter (a torn read-modify-write) is the only way to trip one of them here.
		(await _circuitBreaker.GetStateAsync(CancellationToken.None)).ShouldBe(CircuitState.Closed);
	}

	private static DistributedCircuitBreaker CreateCircuitBreaker(
		string name,
		DistributedCircuitBreakerOptions options)
	{
		IDistributedCache cache = new MemoryDistributedCache(MsOptions.Create(new MemoryDistributedCacheOptions()));
		var logger = A.Fake<ILogger<DistributedCircuitBreaker>>();
		return new DistributedCircuitBreaker(name, cache, MsOptions.Create(options), logger);
	}
}
