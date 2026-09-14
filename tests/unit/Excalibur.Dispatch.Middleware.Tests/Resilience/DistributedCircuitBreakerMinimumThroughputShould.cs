// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Resilience;
using Excalibur.Dispatch.Resilience.Polly;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;

using MsOptions = Microsoft.Extensions.Options.Options;

using CircuitState = Excalibur.Dispatch.Resilience.CircuitState;

namespace Excalibur.Dispatch.Middleware.Tests.Resilience;

/// <summary>
/// Breaker-level regression lock for bead <c>sfu1jv</c>: <see cref="DistributedCircuitBreaker.MinimumThroughput"/>
/// suppression was previously proven only at the window-arithmetic level (an inline copy of the rate
/// formula agreeing with itself). This drives the REAL breaker's production entry points
/// (<see cref="DistributedCircuitBreaker.RecordFailureAsync"/>, <see cref="DistributedCircuitBreaker.ExecuteAsync{T}"/>)
/// against a real <see cref="Microsoft.Extensions.Caching.Distributed.IDistributedCache"/>
/// (<see cref="MemoryDistributedCache"/>), so a breaker whose decision path never consults
/// <c>MinimumThroughput</c> fails this test even though a window-formula-only test would still pass.
/// </summary>
/// <remarks>
/// Each arm builds up the windowed failure count via <see cref="DistributedCircuitBreaker.RecordFailureAsync"/>
/// directly (not <c>ExecuteAsync</c>), so Polly's own local circuit breaker inside the SUT (a separate,
/// already-correct mechanism gating <c>ExecuteAsync</c>'s inner call) never observes these calls and cannot
/// confound the assertion. <c>ExecuteAsync</c> is used only for the final behavioral check -- does the
/// operation actually run, or is it short-circuited -- which is what "reading the state enum alone" would
/// miss.
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Resilience)]
public sealed class DistributedCircuitBreakerMinimumThroughputShould : IAsyncDisposable
{
	private readonly MemoryDistributedCache _cache = new(MsOptions.Create(new MemoryDistributedCacheOptions()));
	private DistributedCircuitBreaker? _sut;

	public async ValueTask DisposeAsync()
	{
		if (_sut is not null)
		{
			await _sut.DisposeAsync().ConfigureAwait(false);
		}
	}

	private DistributedCircuitBreaker CreateBreaker(int minimumThroughput, double failureRatio = 0.5)
	{
		var options = new DistributedCircuitBreakerOptions
		{
			FailureRatio = failureRatio,
			MinimumThroughput = minimumThroughput,
			// High: isolate the throughput/rate gate under test from the consecutive-burst fallback.
			ConsecutiveFailureThreshold = 1000,
			BreakDuration = TimeSpan.FromSeconds(30),
			SuccessThresholdToClose = 2,
			SyncInterval = TimeSpan.FromHours(1), // prevent the background sync timer from firing mid-test
		};
		_sut = new DistributedCircuitBreaker(
			$"min-throughput-{Guid.NewGuid():N}",
			_cache,
			MsOptions.Create(options),
			NullLogger<DistributedCircuitBreaker>.Instance);
		return _sut;
	}

	// Safety (clause 1): N-1 failing attempts, all failures, must NOT open the circuit -- and a
	// subsequent call must actually EXECUTE, not just report a Closed state enum.
	[Fact]
	public async Task StayClosed_WhenFailuresAreOneBelowMinimumThroughput()
	{
		const int minimumThroughput = 5;
		var breaker = CreateBreaker(minimumThroughput);

		for (var i = 0; i < minimumThroughput - 1; i++)
		{
			await breaker.RecordFailureAsync(CancellationToken.None, new InvalidOperationException($"failure {i}"));
		}

		(await breaker.GetStateAsync(CancellationToken.None)).ShouldBe(CircuitState.Closed);

		var executed = false;
		var result = await breaker.ExecuteAsync(
			() =>
			{
				executed = true;
				return Task.FromResult(42);
			},
			CancellationToken.None);

		executed.ShouldBeTrue("below MinimumThroughput, the breaker must not suppress calls -- reading Closed off the state enum alone would pass even for a breaker that short-circuits everything");
		result.ShouldBe(42);
	}

	// Liveness (clause 2, the arm that makes the pair non-vacuous): the Nth failing attempt crosses
	// MinimumThroughput at a failure rate at/above FailureRatio -- the circuit DOES open, and the next
	// call IS short-circuited.
	[Fact]
	public async Task OpenAndShortCircuit_WhenFailuresReachMinimumThroughput()
	{
		const int minimumThroughput = 5;
		var breaker = CreateBreaker(minimumThroughput);

		for (var i = 0; i < minimumThroughput; i++)
		{
			await breaker.RecordFailureAsync(CancellationToken.None, new InvalidOperationException($"failure {i}"));
		}

		(await breaker.GetStateAsync(CancellationToken.None)).ShouldBe(CircuitState.Open);

		var executed = false;
		await Should.ThrowAsync<CircuitBreakerOpenException>(
			async () => await breaker.ExecuteAsync(
				() =>
				{
					executed = true;
					return Task.FromResult(42);
				},
				CancellationToken.None));

		executed.ShouldBeFalse("at/above MinimumThroughput with the ratio breached, the breaker must short-circuit -- the operation must not run");
	}

	// Clause 3: the boundary is asserted at exactly N-1 and N (both arms above use minimumThroughput-1 /
	// minimumThroughput directly, never "a comfortable distance" like 1 or minimumThroughput*2).

	// Clause 4: distinguishes the THROUGHPUT gate from the RATE gate. N attempts reached, but the
	// failure ratio stays below FailureRatio -- must stay closed even though throughput alone was hit.
	[Fact]
	public async Task StayClosed_WhenThroughputReachedButFailureRatioBelowThreshold()
	{
		const int minimumThroughput = 10;
		var breaker = CreateBreaker(minimumThroughput, failureRatio: 0.5);

		// 6 successes + 4 failures = 10 in-window attempts (== MinimumThroughput) at a 40% failure
		// ratio (< 50%). Throughput alone must not trip it.
		for (var i = 0; i < 6; i++)
		{
			await breaker.RecordSuccessAsync(CancellationToken.None);
		}

		for (var i = 0; i < 4; i++)
		{
			await breaker.RecordFailureAsync(CancellationToken.None, new InvalidOperationException($"failure {i}"));
		}

		(await breaker.GetStateAsync(CancellationToken.None)).ShouldBe(CircuitState.Closed,
			"throughput was reached but the failure ratio (40%) is below the configured threshold (50%) -- the throughput gate and the rate gate are separate conditions, both required");
	}
}
