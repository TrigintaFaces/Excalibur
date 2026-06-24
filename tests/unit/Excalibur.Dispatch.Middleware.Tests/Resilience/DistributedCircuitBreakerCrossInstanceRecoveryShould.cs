// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json;

using Excalibur.Dispatch.Resilience.Polly;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;

using MsOptions = Microsoft.Extensions.Options.Options;

using CircuitState = Excalibur.Dispatch.Resilience.CircuitState;

namespace Excalibur.Dispatch.Middleware.Tests.Resilience;

/// <summary>
/// Engage-test (regression lock) for bd-0snskv (S842, ADR-336 Wave 2 silent-failure class):
/// a <see cref="DistributedCircuitBreaker"/> that learns of a cross-instance HalfOpen transition
/// ONLY through the shared store (not via its own <c>ExecuteAsync</c>) must still auto-close when
/// the operator drives recovery through the manual <see cref="DistributedCircuitBreaker.RecordSuccessAsync"/>
/// path.
///
/// <para><b>The bug.</b> The HalfOpen->Closed gate in <c>RecordSuccessAsync</c> reads the instance-local
/// <c>_lastKnownState</c> field. That field is only refreshed from the authoritative store by
/// <c>ExecuteAsync</c> (single distributed fetch) and by the periodic background sync timer. On the
/// manual record path, a breaker whose local view is stale (still <c>Closed</c>) never satisfies the
/// gate, so the circuit is wedged HalfOpen forever and never recovers — even though the shared store
/// says HalfOpen and the success threshold is met.</para>
///
/// <para><b>Why this is the only RED-provable seam (the MS-7 trap).</b> An <c>ExecuteAsync</c>-driven
/// scenario is vacuous: <c>ExecuteAsync</c> refreshes <c>_lastKnownState</c> from the store before the
/// gate, so it masks the bug and stays GREEN on the broken code. The bug lives exclusively on the
/// manual <c>RecordSuccessAsync</c> path with a stale local view, which is exactly what this test drives.</para>
///
/// <para><b>Determinism (no wall-clock, no sleep barrier).</b> The sync timer's due-time is hard-wired to
/// <see cref="TimeSpan.Zero"/>, so it fires exactly once at construction regardless of
/// <c>SyncInterval</c>; setting <c>SyncInterval = Timeout.InfiniteTimeSpan</c> kills every subsequent
/// periodic fire. That single t=0 fire is a threadpool callback and is racy against our store mutation,
/// so we close the race with a controllable barrier: a counting cache decorator lets us poll until the
/// t=0 sync's state read has been observed, and only THEN do we flip the shared store to HalfOpen. After
/// that flip no further sync can run, so the local view is guaranteed stale — a real condition, not a
/// timing guess.</para>
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Resilience)]
public sealed class DistributedCircuitBreakerCrossInstanceRecoveryShould
{
	[Fact]
	public async Task Close_from_half_open_via_manual_record_when_half_open_was_learned_only_from_the_shared_store()
	{
		// Arrange — a controllable shared store that counts state-key reads so we can barrier on the
		// single t=0 background sync before mutating it.
		const string breakerName = "0snskv-cross-instance-recovery";
		var stateKey = $"circuit-breaker:{breakerName}:state";
		var metricsKey = $"circuit-breaker:{breakerName}:metrics";

		var cache = new CountingDistributedCache(
			new MemoryDistributedCache(MsOptions.Create(new MemoryDistributedCacheOptions())),
			stateKey);

		var options = new DistributedCircuitBreakerOptions
		{
			ConsecutiveFailureThreshold = 100,            // irrelevant here; keep failures from tripping anything
			SuccessThresholdToClose = 2,                  // require 2 consecutive successes (seed 1, +1 here)
			SyncInterval = System.Threading.Timeout.InfiniteTimeSpan, // only the single t=0 fire remains; no periodic re-sync
			MetricsRetention = TimeSpan.FromMinutes(5),
		};

		await using var breaker = new DistributedCircuitBreaker(
			breakerName,
			cache,
			MsOptions.Create(options),
			NullLogger<DistributedCircuitBreaker>.Instance);

		// Barrier: the store starts empty -> the t=0 sync reads Closed -> _lastKnownState stays Closed.
		// Poll on the OBSERVED state read (a real condition) — not a wall-clock sleep — so that the flip
		// below is deterministically ordered AFTER the only sync that could refresh the local view.
		var deadline = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(10);
		while (cache.StateReadCount == 0 && DateTimeOffset.UtcNow < deadline)
		{
			await Task.Delay(10, CancellationToken.None);
		}

		cache.StateReadCount.ShouldBeGreaterThanOrEqualTo(
			1,
			"the t=0 background sync must have read the (empty/Closed) store before we flip it to HalfOpen");

		// Act 1 — another instance has moved the SHARED store to HalfOpen with the success count one short
		// of the close threshold. This breaker never executed through ExecuteAsync, so its local view is a
		// stale Closed; with SyncInterval=Infinite no further sync will correct it.
		await SeedAsync(cache, stateKey, new DistributedCircuitState
		{
			State = CircuitState.HalfOpen,
			TransitionedAt = DateTimeOffset.UtcNow,
			InstanceId = "other-instance",
		});
		await SeedAsync(cache, metricsKey, new DistributedCircuitMetrics
		{
			ConsecutiveSuccesses = options.SuccessThresholdToClose - 1, // 1 — the next success reaches the gate
		});

		// Act 2 — operator drives recovery via the MANUAL record path (NOT ExecuteAsync).
		await breaker.RecordSuccessAsync(CancellationToken.None);

		// Assert (bd-0snskv lock) — the success that reaches the threshold against a HalfOpen shared store
		// MUST close the circuit. Pre-fix: the gate reads stale local Closed -> never transitions -> the
		// store stays HalfOpen -> RED. Post-fix: the gate reads the authoritative HalfOpen store -> closes -> GREEN.
		var finalState = await breaker.GetStateAsync(CancellationToken.None);
		finalState.ShouldBe(
			CircuitState.Closed,
			"a threshold-reaching success on the manual record path must close a circuit whose HalfOpen state " +
			"was learned only from the shared store (bd-0snskv — manual path must not read a stale local view)");
	}

	private static Task SeedAsync(IDistributedCache cache, string key, DistributedCircuitState state) =>
		cache.SetStringAsync(
			key,
			JsonSerializer.Serialize(state, DistributedCircuitJsonContext.Default.DistributedCircuitState),
			CancellationToken.None);

	private static Task SeedAsync(IDistributedCache cache, string key, DistributedCircuitMetrics metrics) =>
		cache.SetStringAsync(
			key,
			JsonSerializer.Serialize(metrics, DistributedCircuitJsonContext.Default.DistributedCircuitMetrics),
			CancellationToken.None);

	/// <summary>
	/// An <see cref="IDistributedCache"/> decorator that delegates to an inner cache while counting reads
	/// of a single watched key. Used purely as a deterministic test barrier: it lets the test observe that
	/// the circuit breaker's one-shot t=0 background sync has read the state key before the test mutates it.
	/// </summary>
	private sealed class CountingDistributedCache(IDistributedCache inner, string watchedKey) : IDistributedCache
	{
		private int _stateReadCount;

		public int StateReadCount => Volatile.Read(ref _stateReadCount);

		public byte[]? Get(string key)
		{
			CountIfWatched(key);
			return inner.Get(key);
		}

		public Task<byte[]?> GetAsync(string key, CancellationToken token = default)
		{
			CountIfWatched(key);
			return inner.GetAsync(key, token);
		}

		public void Set(string key, byte[] value, DistributedCacheEntryOptions options) =>
			inner.Set(key, value, options);

		public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default) =>
			inner.SetAsync(key, value, options, token);

		public void Refresh(string key) => inner.Refresh(key);

		public Task RefreshAsync(string key, CancellationToken token = default) => inner.RefreshAsync(key, token);

		public void Remove(string key) => inner.Remove(key);

		public Task RemoveAsync(string key, CancellationToken token = default) => inner.RemoveAsync(key, token);

		private void CountIfWatched(string key)
		{
			if (string.Equals(key, watchedKey, StringComparison.Ordinal))
			{
				_ = Interlocked.Increment(ref _stateReadCount);
			}
		}
	}
}
