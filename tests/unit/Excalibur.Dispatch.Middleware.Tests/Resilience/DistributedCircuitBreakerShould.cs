// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json;

using Excalibur.Dispatch.Resilience;
using Excalibur.Dispatch.Options.Resilience;
using Excalibur.Dispatch.Resilience.Polly;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;

using MsOptions = Microsoft.Extensions.Options.Options;

namespace Excalibur.Dispatch.Middleware.Tests.Resilience;

/// <summary>
/// Unit tests for <see cref="DistributedCircuitBreaker"/>.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Resilience)]
public sealed class DistributedCircuitBreakerShould : UnitTestBase, IAsyncDisposable
{
	private DistributedCircuitBreaker? _circuitBreaker;
	private IDistributedCache? _cache;
	private ILogger<DistributedCircuitBreaker>? _logger;

	public async ValueTask DisposeAsync()
	{
		if (_circuitBreaker != null)
		{
			await _circuitBreaker.DisposeAsync();
			_circuitBreaker = null;
		}
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing && _circuitBreaker != null)
		{
			_ = _circuitBreaker.DisposeAsync().AsTask();
			_circuitBreaker = null;
		}
		base.Dispose(disposing);
	}

	private DistributedCircuitBreaker CreateCircuitBreaker(
		string name = "test-circuit",
		DistributedCircuitBreakerOptions? options = null)
	{
		_cache = A.Fake<IDistributedCache>();
		_logger = A.Fake<ILogger<DistributedCircuitBreaker>>();
		var optionsWrapper = MsOptions.Create(options ?? new DistributedCircuitBreakerOptions());

		_circuitBreaker = new DistributedCircuitBreaker(name, _cache, optionsWrapper, _logger);
		return _circuitBreaker;
	}

	/// <summary>
	/// Makes every state-key read return <paramref name="state"/>, standing in for another instance
	/// having driven the shared circuit there.
	/// </summary>
	private void SeedSharedState(CircuitState state)
	{
		var json = JsonSerializer.Serialize(
			new DistributedCircuitState { State = state, InstanceId = "other-instance" },
			DistributedCircuitJsonContext.Default.DistributedCircuitState);
		A.CallTo(() => _cache.GetAsync(A<string>.That.Contains("state"), A<CancellationToken>._))
			.Returns(System.Text.Encoding.UTF8.GetBytes(json));
	}

	#region Constructor Tests

	[Fact]
	public void Constructor_WithNullName_ThrowsArgumentNullException()
	{
		// Arrange
		var cache = A.Fake<IDistributedCache>();
		var options = MsOptions.Create(new DistributedCircuitBreakerOptions());
		var logger = A.Fake<ILogger<DistributedCircuitBreaker>>();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new DistributedCircuitBreaker(null!, cache, options, logger));
	}

	[Fact]
	public void Constructor_WithNullCache_ThrowsArgumentNullException()
	{
		// Arrange
		var options = MsOptions.Create(new DistributedCircuitBreakerOptions());
		var logger = A.Fake<ILogger<DistributedCircuitBreaker>>();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new DistributedCircuitBreaker("test", null!, options, logger));
	}

	[Fact]
	public void Constructor_WithNullOptions_ThrowsArgumentNullException()
	{
		// Arrange
		var cache = A.Fake<IDistributedCache>();
		var logger = A.Fake<ILogger<DistributedCircuitBreaker>>();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new DistributedCircuitBreaker("test", cache, null!, logger));
	}

	[Fact]
	public void Constructor_WithNullLogger_ThrowsArgumentNullException()
	{
		// Arrange
		var cache = A.Fake<IDistributedCache>();
		var options = MsOptions.Create(new DistributedCircuitBreakerOptions());

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new DistributedCircuitBreaker("test", cache, options, null!));
	}

	[Fact]
	public void Constructor_WithValidArguments_CreatesInstance()
	{
		// Act
		var cb = CreateCircuitBreaker();

		// Assert
		cb.ShouldNotBeNull();
		cb.Name.ShouldBe("test-circuit");
	}

	#endregion

	#region Name Property Tests

	[Fact]
	public void Name_ReturnsConfiguredName()
	{
		// Arrange
		var cb = CreateCircuitBreaker("my-custom-circuit");

		// Assert
		cb.Name.ShouldBe("my-custom-circuit");
	}

	#endregion

	#region GetStateAsync Tests

	[Fact]
	public async Task GetStateAsync_WhenCacheIsEmpty_ReturnsClosedState()
	{
		// Arrange
		var cb = CreateCircuitBreaker();
		A.CallTo(() => _cache.GetAsync(A<string>._, A<CancellationToken>._))
			.Returns((byte[]?)null);

		// Act
		var state = await cb.GetStateAsync(CancellationToken.None);

		// Assert
		state.ShouldBe(CircuitState.Closed);
	}

	[Fact]
	public async Task GetStateAsync_WhenCacheHasState_ReturnsStoredState()
	{
		// Arrange
		var cb = CreateCircuitBreaker();
		var stateJson = JsonSerializer.Serialize(new { State = (int)CircuitState.Open, OpenedAt = DateTime.UtcNow, OpenUntil = DateTime.UtcNow.AddMinutes(1), InstanceId = "test" });
		var stateBytes = System.Text.Encoding.UTF8.GetBytes(stateJson);
		A.CallTo(() => _cache.GetAsync(A<string>.That.Contains("state"), A<CancellationToken>._))
			.Returns(stateBytes);

		// Act
		var state = await cb.GetStateAsync(CancellationToken.None);

		// Assert
		state.ShouldBe(CircuitState.Open);
	}

	[Fact]
	public async Task GetStateAsync_WhenCacheThrows_ReturnsLastKnownState()
	{
		// Arrange
		var cb = CreateCircuitBreaker();
		A.CallTo(() => _cache.GetAsync(A<string>._, A<CancellationToken>._))
			.Throws(new InvalidOperationException("Cache error"));

		// Act
		var state = await cb.GetStateAsync(CancellationToken.None);

		// Assert - Should return last known state (Closed initially) and not throw
		state.ShouldBe(CircuitState.Closed);
	}

	#endregion

	#region ExecuteAsync Tests

	[Fact]
	public async Task ExecuteAsync_WithNullOperation_ThrowsArgumentNullException()
	{
		// Arrange
		var cb = CreateCircuitBreaker();

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(() =>
			cb.ExecuteAsync<int>(null!, CancellationToken.None));
	}

	[Fact]
	public async Task ExecuteAsync_WhenCircuitClosed_ExecutesOperation()
	{
		// Arrange
		var cb = CreateCircuitBreaker();
		A.CallTo(() => _cache.GetAsync(A<string>._, A<CancellationToken>._))
			.Returns((byte[]?)null);

		// Act
		var result = await cb.ExecuteAsync(() => Task.FromResult(42), CancellationToken.None);

		// Assert
		result.ShouldBe(42);
	}

	[Fact]
	public async Task ExecuteAsync_WhenOperationSucceeds_RecordsSuccess()
	{
		// Asserted through the decision the count drives — a half-open circuit closing — rather than
		// through a cache write. Counters are per-instance and never leave the process, so a write
		// assertion would be testing an implementation detail that no longer exists.
		var cb = CreateCircuitBreaker(options: new DistributedCircuitBreakerOptions { SuccessThresholdToClose = 1 });
		SeedSharedState(CircuitState.HalfOpen);

		_ = await cb.ExecuteAsync(() => Task.FromResult(42), CancellationToken.None);

		A.CallTo(() => _cache.RemoveAsync(A<string>.That.Contains("state"), A<CancellationToken>._))
			.MustHaveHappened();
	}

	[Fact]
	public async Task ExecuteAsync_WhenOperationFails_RecordsFailure()
	{
		// A threshold of 1 makes the single recorded failure trip the circuit, so the shared state write
		// is the observable proof that the failure was counted.
		var cb = CreateCircuitBreaker(options: new DistributedCircuitBreakerOptions { ConsecutiveFailureThreshold = 1, SyncInterval = TimeSpan.FromHours(1) });
		A.CallTo(() => _cache.GetAsync(A<string>._, A<CancellationToken>._))
			.Returns((byte[]?)null);

		_ = await Should.ThrowAsync<InvalidOperationException>(async () =>
			await cb.ExecuteAsync<int>(() => throw new InvalidOperationException("Test error"), CancellationToken.None));

		A.CallTo(() => _cache.SetAsync(A<string>.That.Contains("state"), A<byte[]>._, A<DistributedCacheEntryOptions>._, A<CancellationToken>._))
			.MustHaveHappened();
	}

	[Fact]
	public async Task ExecuteAsync_WithCancellationToken_PassesToken()
	{
		// Arrange
		var cb = CreateCircuitBreaker();
		using var cts = new CancellationTokenSource();
		A.CallTo(() => _cache.GetAsync(A<string>._, A<CancellationToken>._))
			.Returns((byte[]?)null);

		// Act
		var result = await cb.ExecuteAsync(() => Task.FromResult(1), cts.Token);

		// Assert
		result.ShouldBe(1);
	}

	#endregion

	#region RecordSuccessAsync Tests

	[Fact]
	public async Task RecordSuccessAsync_CountsTowardsClosingAHalfOpenCircuit()
	{
		// Arrange — the shared store says half-open (another instance drove it there) and one success is
		// enough to close, so the close is the observable proof this success was counted.
		var cb = CreateCircuitBreaker(options: new DistributedCircuitBreakerOptions
		{
			SuccessThresholdToClose = 1,
			SyncInterval = TimeSpan.FromHours(1),
		});
		SeedSharedState(CircuitState.HalfOpen);

		// Act
		await cb.RecordSuccessAsync(CancellationToken.None);

		// Assert — the success counts towards closing the circuit, which is what closing a half-open
		// circuit proves. Nothing about the count itself crosses to the shared store.
		A.CallTo(() => _cache.RemoveAsync(A<string>.That.Contains("state"), A<CancellationToken>._))
			.MustHaveHappened();
	}

	[Fact]
	public async Task RecordSuccessAsync_WhenCacheThrows_DoesNotPropagate()
	{
		// Arrange
		var cb = CreateCircuitBreaker();
		A.CallTo(() => _cache.GetAsync(A<string>._, A<CancellationToken>._))
			.Throws(new InvalidOperationException("Cache error"));

		// Act & Assert - Should not throw
		await cb.RecordSuccessAsync(CancellationToken.None);
	}

	#endregion

	#region RecordFailureAsync Tests

	[Fact]
	public async Task RecordFailureAsync_CountsTowardsOpeningTheCircuit()
	{
		// Arrange — a threshold of one, so this single failure must trip.
		var cb = CreateCircuitBreaker(options: new DistributedCircuitBreakerOptions
		{
			ConsecutiveFailureThreshold = 1,
			SyncInterval = TimeSpan.FromHours(1),
		});
		A.CallTo(() => _cache.GetAsync(A<string>._, A<CancellationToken>._))
			.Returns((byte[]?)null);

		// Act
		await cb.RecordFailureAsync(CancellationToken.None, new InvalidOperationException("Test"));

		// Assert — the failure counts towards opening the circuit; the trip is the observable proof.
		A.CallTo(() => _cache.SetAsync(A<string>.That.Contains("state"), A<byte[]>._, A<DistributedCacheEntryOptions>._, A<CancellationToken>._))
			.MustHaveHappened();
	}

	[Fact]
	public async Task RecordFailureAsync_WithNullException_DoesNotThrow()
	{
		// Arrange
		var cb = CreateCircuitBreaker();
		A.CallTo(() => _cache.GetAsync(A<string>._, A<CancellationToken>._))
			.Returns((byte[]?)null);

		// Act & Assert - Should not throw
		await cb.RecordFailureAsync(CancellationToken.None);
	}

	[Fact]
	public async Task RecordFailureAsync_WhenCacheThrows_DoesNotPropagate()
	{
		// Arrange
		var cb = CreateCircuitBreaker();
		A.CallTo(() => _cache.GetAsync(A<string>._, A<CancellationToken>._))
			.Throws(new InvalidOperationException("Cache error"));

		// Act & Assert - Should not throw
		await cb.RecordFailureAsync(CancellationToken.None, new InvalidOperationException("Test"));
	}

	// gt2x7g (j604qc-a / thkygd): nothing derived from an exception may reach the shared store. The store
	// is written by every instance and read at rest cross-instance, so an exception message carrying PII
	// (user input, credentials, connection strings, record ids) would leak there. RED on a mutant that
	// puts exception.Message — or any per-failure detail — into a value written to the cache.
	[Fact]
	public async Task RecordFailureAsync_WritesNoExceptionDetailToTheSharedStore()
	{
		// Arrange — a threshold of 1 so this single failure trips and a state write definitely happens;
		// a test that captured no writes at all would pass vacuously.
		var cb = CreateCircuitBreaker(options: new DistributedCircuitBreakerOptions { ConsecutiveFailureThreshold = 1 });
		A.CallTo(() => _cache!.GetAsync(A<string>._, A<CancellationToken>._))
			.Returns((byte[]?)null);

		var written = new List<byte[]>();
		A.CallTo(() => _cache!.SetAsync(
				A<string>._, A<byte[]>._, A<DistributedCacheEntryOptions>._, A<CancellationToken>._))
			.Invokes(call => written.Add(call.GetArgument<byte[]>(1)!));

		// A message deliberately laden with PII/secret-shaped content that must NOT reach the store.
		const string piiMessage = "login failed for user alice@example.com token=hunter2 record-id=42";

		// Act
		await cb.RecordFailureAsync(CancellationToken.None, new InvalidOperationException(piiMessage));

		// Assert — the write happened…
		written.ShouldNotBeEmpty("the failure must trip the circuit, which writes the shared state");

		// …and none of the message's fragments — nor the exception type — are anywhere in what was written.
		var persisted = string.Join("\n", written.Select(System.Text.Encoding.UTF8.GetString));
		persisted.ShouldNotContain("alice@example.com", Case.Insensitive,
			"the raw exception message (PII) must never be written to the shared store.");
		persisted.ShouldNotContain("hunter2", Case.Insensitive,
			"secret-shaped content from the exception message must never be persisted.");
		persisted.ShouldNotContain("record-id=42", Case.Insensitive,
			"record identifiers from the exception message must never be persisted.");
		persisted.ShouldNotContain(nameof(InvalidOperationException), Case.Insensitive,
			"per-failure detail stays in the process; only circuit state crosses to the shared store.");
	}

	[Fact]
	public async Task ResetAsync_RemovesTheSharedStateAndClearsTheLocalRun()
	{
		// Arrange — one failure short of the threshold, so a reset that failed to clear the local run
		// would let the very next failure trip the circuit.
		var cb = CreateCircuitBreaker(options: new DistributedCircuitBreakerOptions
		{
			ConsecutiveFailureThreshold = 2,
			MinimumThroughput = int.MaxValue,
		});
		A.CallTo(() => _cache.GetAsync(A<string>._, A<CancellationToken>._)).Returns((byte[]?)null);
		await cb.RecordFailureAsync(CancellationToken.None, new InvalidOperationException("before reset"));

		// Act
		await cb.ResetAsync(CancellationToken.None);
		await cb.RecordFailureAsync(CancellationToken.None, new InvalidOperationException("after reset"));

		// Assert
		A.CallTo(() => _cache.RemoveAsync(A<string>.That.Contains("state"), A<CancellationToken>._))
			.MustHaveHappened();
		A.CallTo(() => _cache.SetAsync(A<string>.That.Contains("state"), A<byte[]>._, A<DistributedCacheEntryOptions>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task ResetAsync_WhenCacheThrows_DoesNotPropagate()
	{
		// Arrange
		var cb = CreateCircuitBreaker();
		A.CallTo(() => _cache.RemoveAsync(A<string>._, A<CancellationToken>._))
			.Throws(new InvalidOperationException("Cache error"));

		// Act & Assert - Should not throw
		await cb.ResetAsync(CancellationToken.None);
	}

	#endregion

	#region Circuit State Transition Tests

	[Fact]
	public async Task ExecuteAsync_WhenCircuitOpenAndNotExpired_ThrowsCircuitBreakerOpenException()
	{
		// Arrange
		var cb = CreateCircuitBreaker();
		var openState = new
		{
			State = (int)CircuitState.Open,
			OpenedAt = DateTime.UtcNow,
			OpenUntil = DateTime.UtcNow.AddMinutes(5), // Not expired
			InstanceId = "test"
		};
		var stateJson = JsonSerializer.Serialize(openState);
		var stateBytes = System.Text.Encoding.UTF8.GetBytes(stateJson);

		A.CallTo(() => _cache.GetAsync(A<string>.That.Contains("state"), A<CancellationToken>._))
			.Returns(stateBytes);

		// Act & Assert
		// FR-116-1: open state must throw the canonical CircuitBreakerOpenException,
		// not Polly's BrokenCircuitException.
		var ex = await Should.ThrowAsync<CircuitBreakerOpenException>(
			() => cb.ExecuteAsync(() => Task.FromResult(42), CancellationToken.None));
		ex.CircuitName.ShouldBe("test-circuit");
	}

	[Fact]
	public async Task ExecuteAsync_WhenCircuitOpenButExpired_TransitionsToHalfOpen()
	{
		// Arrange
		var cb = CreateCircuitBreaker();
		var openState = new
		{
			State = (int)CircuitState.Open,
			OpenedAt = DateTime.UtcNow.AddMinutes(-10),
			OpenUntil = DateTime.UtcNow.AddMinutes(-5), // Already expired
			InstanceId = "test"
		};
		var stateJson = JsonSerializer.Serialize(openState);
		var stateBytes = System.Text.Encoding.UTF8.GetBytes(stateJson);

		A.CallTo(() => _cache.GetAsync(A<string>.That.Contains("state"), A<CancellationToken>._))
			.Returns(stateBytes);

		// Act
		var result = await cb.ExecuteAsync(() => Task.FromResult(99), CancellationToken.None);

		// Assert - Operation should succeed and circuit should transition
		result.ShouldBe(99);
		A.CallTo(() => _cache.SetAsync(A<string>.That.Contains("state"), A<byte[]>._, A<DistributedCacheEntryOptions>._, A<CancellationToken>._))
			.MustHaveHappened();
	}

	[Fact]
	public async Task RecordFailureAsync_WhenConsecutiveFailuresExceedThreshold_OpensCircuit()
	{
		// Arrange
		var options = new DistributedCircuitBreakerOptions
		{
			ConsecutiveFailureThreshold = 2,
			FailureRatio = 0.9 // High ratio so consecutive failures trigger first
		};
		var cb = CreateCircuitBreaker(options: options);
		A.CallTo(() => _cache.GetAsync(A<string>._, A<CancellationToken>._)).Returns((byte[]?)null);

		// Act — the run is this instance's own, so both failures are recorded through it rather than one
		// being seeded into the store. A count read back from the store is exactly what a distributed
		// breaker over this abstraction cannot do correctly.
		await cb.RecordFailureAsync(CancellationToken.None, new InvalidOperationException("First failure"));
		await cb.RecordFailureAsync(CancellationToken.None, new InvalidOperationException("Second failure"));

		// Assert - State should be set to open
		A.CallTo(() => _cache.SetAsync(A<string>.That.Contains("state"), A<byte[]>._, A<DistributedCacheEntryOptions>._, A<CancellationToken>._))
			.MustHaveHappened();
	}

	[Fact]
	public async Task RecordFailureAsync_WhenWindowedFailureRatioExceedsThreshold_OpensCircuit()
	{
		// Arrange — flipped to the zxb7fp windowed contract (bd-c6bjc3, F-5 stale-sibling).
		// The open-decision now compares the ROLLING-WINDOW failure ratio (computed from bucketed
		// RecordWindow attempts persisted in metrics) against FailureRatio, and only trips once at least
		// MinimumThroughput attempts have accumulated in the SamplingDuration window — NOT a lifetime
		// cumulative ratio. ConsecutiveFailureThreshold is set high so ONLY the windowed-ratio gate (not
		// the consecutive-burst fallback) can open the circuit here.
		var options = new DistributedCircuitBreakerOptions
		{
			FailureRatio = 0.5,            // trip above 50% windowed failure ratio
			MinimumThroughput = 10,        // require >= 10 in-window attempts before evaluating the ratio
			ConsecutiveFailureThreshold = 100 // high: isolate the windowed-ratio gate from the consecutive fallback
		};
		var cb = CreateCircuitBreaker(options: options);

		// Stateful distributed cache so windowed buckets accumulate across calls (the windowed ratio reads
		// the rolling window round-tripped through metrics; a fixed/injected lifetime counter is ignored now).
		var store = new Dictionary<string, byte[]>(StringComparer.Ordinal);
		A.CallTo(() => _cache.SetAsync(A<string>._, A<byte[]>._, A<DistributedCacheEntryOptions>._, A<CancellationToken>._))
			.Invokes(call => store[(string)call.Arguments[0]!] = (byte[])call.Arguments[1]!)
			.Returns(Task.CompletedTask);
		A.CallTo(() => _cache.GetAsync(A<string>._, A<CancellationToken>._))
			.ReturnsLazily(call => Task.FromResult(store.TryGetValue((string)call.Arguments[0]!, out var v) ? v : (byte[]?)null));

		// Act — 4 successes + 6 failures = 10 in-window attempts at a 60% failure ratio (> 50%), with
		// consecutive failures peaking at 6 (< 100). Only the windowed-ratio gate can trip on the 10th attempt.
		for (var i = 0; i < 4; i++)
		{
			await cb.RecordSuccessAsync(CancellationToken.None);
		}

		for (var i = 0; i < 6; i++)
		{
			await cb.RecordFailureAsync(CancellationToken.None, new InvalidOperationException($"failure {i}"));
		}

		// Assert — windowed ratio (6/10 = 60% > 50%) with >= MinimumThroughput attempts opens the circuit
		// (writes the "state" key). RED on a regression that drops the MinimumThroughput/windowed-ratio gate.
		A.CallTo(() => _cache.SetAsync(A<string>.That.Contains("state"), A<byte[]>._, A<DistributedCacheEntryOptions>._, A<CancellationToken>._))
			.MustHaveHappened();
	}

	#endregion

	#region DisposeAsync Tests

	[Fact]
	public async Task DisposeAsync_CanBeCalledMultipleTimes()
	{
		// Arrange
		var cb = CreateCircuitBreaker();

		// Act & Assert - should not throw
		await cb.DisposeAsync();
		await cb.DisposeAsync();

		_circuitBreaker = null; // Prevent double dispose in test cleanup
	}

	#endregion

	#region Interface Implementation Tests

	[Fact]
	public void ImplementsIDistributedCircuitBreaker()
	{
		// Arrange
		var cb = CreateCircuitBreaker();

		// Assert
		cb.ShouldBeAssignableTo<IDistributedCircuitBreaker>();
	}

	[Fact]
	public void ImplementsIAsyncDisposable()
	{
		// Arrange
		var cb = CreateCircuitBreaker();

		// Assert
		cb.ShouldBeAssignableTo<IAsyncDisposable>();
	}

	#endregion
}
