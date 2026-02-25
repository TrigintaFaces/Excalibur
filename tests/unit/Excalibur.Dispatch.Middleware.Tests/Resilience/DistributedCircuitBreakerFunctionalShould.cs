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
/// Functional tests for <see cref="DistributedCircuitBreaker"/> verifying
/// state transitions, failure tracking, and recovery behavior.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Resilience")]
public sealed class DistributedCircuitBreakerFunctionalShould : IAsyncDisposable
{
	private readonly MemoryDistributedCache _cache;
	private readonly DistributedCircuitBreakerOptions _options;
	private readonly DistributedCircuitBreaker _sut;

	public DistributedCircuitBreakerFunctionalShould()
	{
		_cache = new MemoryDistributedCache(MsOptions.Create(new MemoryDistributedCacheOptions()));
		_options = new DistributedCircuitBreakerOptions
		{
			FailureRatio = 0.5,
			MinimumThroughput = 2,
			ConsecutiveFailureThreshold = 3,
			BreakDuration = TimeSpan.FromSeconds(10),
			SuccessThresholdToClose = 2,
			SyncInterval = TimeSpan.FromHours(1), // prevent timer
			MetricsRetention = TimeSpan.FromMinutes(5),
		};
		_sut = new DistributedCircuitBreaker(
			"test-breaker",
			_cache,
			MsOptions.Create(_options),
			NullLogger<DistributedCircuitBreaker>.Instance);
	}

	public async ValueTask DisposeAsync() => await _sut.DisposeAsync().ConfigureAwait(false);

	[Fact]
	public async Task Start_in_closed_state()
	{
		var state = await _sut.GetStateAsync(CancellationToken.None);

		state.ShouldBe(CircuitState.Closed);
	}

	[Fact]
	public async Task Execute_operations_successfully_in_closed_state()
	{
		var result = await _sut.ExecuteAsync(
			() => Task.FromResult(42),
			CancellationToken.None);

		result.ShouldBe(42);
	}

	[Fact]
	public async Task Record_success_and_remain_closed()
	{
		await _sut.RecordSuccessAsync(CancellationToken.None);
		await _sut.RecordSuccessAsync(CancellationToken.None);

		var state = await _sut.GetStateAsync(CancellationToken.None);
		state.ShouldBe(CircuitState.Closed);
	}

	[Fact]
	public async Task Open_circuit_after_consecutive_failure_threshold()
	{
		// Record failures to exceed consecutive threshold (3)
		for (var i = 0; i < 4; i++)
		{
			await _sut.RecordFailureAsync(CancellationToken.None,
				new InvalidOperationException($"failure {i}"));
		}

		var state = await _sut.GetStateAsync(CancellationToken.None);
		state.ShouldBe(CircuitState.Open);
	}

	[Fact]
	public async Task Reset_clears_all_state()
	{
		// Open the circuit
		for (var i = 0; i < 5; i++)
		{
			await _sut.RecordFailureAsync(CancellationToken.None, new InvalidOperationException("fail"));
		}

		// Reset
		await _sut.ResetAsync(CancellationToken.None);

		var state = await _sut.GetStateAsync(CancellationToken.None);
		state.ShouldBe(CircuitState.Closed);
	}

	[Fact]
	public void Expose_circuit_breaker_name()
	{
		_sut.Name.ShouldBe("test-breaker");
	}

	[Fact]
	public void Throw_for_null_name()
	{
		Should.Throw<ArgumentNullException>(() => new DistributedCircuitBreaker(
			null!,
			_cache,
			MsOptions.Create(_options),
			NullLogger<DistributedCircuitBreaker>.Instance));
	}

	[Fact]
	public void Throw_for_null_cache()
	{
		Should.Throw<ArgumentNullException>(() => new DistributedCircuitBreaker(
			"test",
			null!,
			MsOptions.Create(_options),
			NullLogger<DistributedCircuitBreaker>.Instance));
	}

	[Fact]
	public void Throw_for_null_options()
	{
		Should.Throw<ArgumentNullException>(() => new DistributedCircuitBreaker(
			"test",
			_cache,
			null!,
			NullLogger<DistributedCircuitBreaker>.Instance));
	}

	[Fact]
	public void Throw_for_null_logger()
	{
		Should.Throw<ArgumentNullException>(() => new DistributedCircuitBreaker(
			"test",
			_cache,
			MsOptions.Create(_options),
			null!));
	}

	[Fact]
	public async Task Execute_throws_for_null_operation()
	{
		await Should.ThrowAsync<ArgumentNullException>(
			() => _sut.ExecuteAsync<int>(null!, CancellationToken.None));
	}

	[Fact]
	public async Task Handle_dispose_idempotently()
	{
		await _sut.DisposeAsync();
		await _sut.DisposeAsync(); // Should not throw
	}

	[Fact]
	public async Task Record_failure_resets_consecutive_successes()
	{
		// Start with successes
		await _sut.RecordSuccessAsync(CancellationToken.None);
		await _sut.RecordSuccessAsync(CancellationToken.None);

		// Now record failures
		for (var i = 0; i < 4; i++)
		{
			await _sut.RecordFailureAsync(CancellationToken.None, new InvalidOperationException("fail"));
		}

		// Circuit should open because consecutive failures exceeded threshold
		var state = await _sut.GetStateAsync(CancellationToken.None);
		state.ShouldBe(CircuitState.Open);
	}
}
