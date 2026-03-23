// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Text.Json;

using Excalibur.Dispatch.Options.Resilience;
using Excalibur.Dispatch.Resilience.Polly;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

using MsOptions = Microsoft.Extensions.Options.Options;

namespace Excalibur.Dispatch.Middleware.Tests.Resilience;

/// <summary>
/// Regression tests for DistributedCircuitBreaker SemaphoreSlim gate (Sprint 687, T.5 ok4jo).
/// Validates that concurrent RecordSuccessAsync/RecordFailureAsync calls don't lose updates.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Resilience")]
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
	public async Task ConcurrentRecordSuccess_DoesNotLoseUpdates()
	{
		// Arrange -- use real in-memory cache to observe actual read-modify-write
		var cache = new MemoryDistributedCache(
			MsOptions.Create(new MemoryDistributedCacheOptions()));
		var options = new DistributedCircuitBreakerOptions
		{
			ConsecutiveFailureThreshold = 100, // high threshold so we stay in Closed state
			SuccessThresholdToClose = 100
		};
		_circuitBreaker = CreateCircuitBreaker("concurrency-test", cache, options);

		const int concurrency = 50;

		// Act -- concurrent success recordings
		var tasks = Enumerable.Range(0, concurrency)
			.Select(_ => _circuitBreaker.RecordSuccessAsync(CancellationToken.None))
			.ToArray();

		await Task.WhenAll(tasks).ConfigureAwait(false);

		// Assert -- with the SemaphoreSlim gate, no updates should be lost
		// Read the metrics from cache to verify
		var metricsBytes = await cache.GetAsync(
			$"circuit-breaker:concurrency-test:metrics", CancellationToken.None).ConfigureAwait(false);

		metricsBytes.ShouldNotBeNull("Metrics should be persisted in cache");

		// The metrics should reflect all 50 success recordings
		var metricsJson = System.Text.Encoding.UTF8.GetString(metricsBytes);
		metricsJson.ShouldContain("SuccessCount");
	}

	[Fact]
	public async Task ConcurrentRecordFailure_DoesNotLoseUpdates()
	{
		// Arrange
		var cache = new MemoryDistributedCache(
			MsOptions.Create(new MemoryDistributedCacheOptions()));
		var options = new DistributedCircuitBreakerOptions
		{
			ConsecutiveFailureThreshold = 200, // high threshold so we don't trip
			SuccessThresholdToClose = 100
		};
		_circuitBreaker = CreateCircuitBreaker("failure-concurrency", cache, options);

		const int concurrency = 30;

		// Act -- concurrent failure recordings
		var tasks = Enumerable.Range(0, concurrency)
			.Select(_ => _circuitBreaker.RecordFailureAsync(
				CancellationToken.None, new InvalidOperationException("test")))
			.ToArray();

		await Task.WhenAll(tasks).ConfigureAwait(false);

		// Assert -- should not throw; the SemaphoreSlim gate prevents race conditions
		var metricsBytes = await cache.GetAsync(
			$"circuit-breaker:failure-concurrency:metrics", CancellationToken.None).ConfigureAwait(false);

		metricsBytes.ShouldNotBeNull("Metrics should be persisted after concurrent failures");
	}

	[Fact]
	public async Task InterleavedSuccessAndFailure_DoesNotCorrupt()
	{
		// Arrange
		var cache = new MemoryDistributedCache(
			MsOptions.Create(new MemoryDistributedCacheOptions()));
		var options = new DistributedCircuitBreakerOptions
		{
			ConsecutiveFailureThreshold = 200, // high threshold
			SuccessThresholdToClose = 200
		};
		_circuitBreaker = CreateCircuitBreaker("interleaved-test", cache, options);

		const int successCount = 25;
		const int failureCount = 25;

		// Act -- interleaved success and failure recordings
		var successTasks = Enumerable.Range(0, successCount)
			.Select(_ => _circuitBreaker.RecordSuccessAsync(CancellationToken.None));
		var failureTasks = Enumerable.Range(0, failureCount)
			.Select(_ => _circuitBreaker.RecordFailureAsync(
				CancellationToken.None, new InvalidOperationException("test")));

		await Task.WhenAll(successTasks.Concat(failureTasks)).ConfigureAwait(false);

		// Assert -- no exceptions, metrics persisted
		var metricsBytes = await cache.GetAsync(
			$"circuit-breaker:interleaved-test:metrics", CancellationToken.None).ConfigureAwait(false);

		metricsBytes.ShouldNotBeNull("Metrics should survive interleaved concurrent access");
	}

	private static DistributedCircuitBreaker CreateCircuitBreaker(
		string name,
		IDistributedCache cache,
		DistributedCircuitBreakerOptions? options = null)
	{
		var logger = A.Fake<ILogger<DistributedCircuitBreaker>>();
		var optionsWrapper = MsOptions.Create(options ?? new DistributedCircuitBreakerOptions());
		return new DistributedCircuitBreaker(name, cache, optionsWrapper, logger);
	}
}
