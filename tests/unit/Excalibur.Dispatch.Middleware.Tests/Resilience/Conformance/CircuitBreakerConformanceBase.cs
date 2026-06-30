// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

// Behavioral conformance test suite for circuit-breaker implementations — bd-ccyett.
//
// Three hand-rolled state machines survive in the codebase
// (CircuitBreakerPattern / PollyCircuitBreakerAdapter / DistributedCircuitBreaker).
// This fixture asserts that all three honour the same contract so that
// recovery-semantics drift (the root cause of bd-lpnsjb) is caught by CI
// before it reaches production.
//
// Contract under test (acceptance criteria from bd-116roh):
//   FR-116-1  Open state throws CircuitBreakerOpenException (canonical type).
//   FR-116-2  OperationCanceledException never trips a circuit breaker.
//   FR-116-3  State property / GetStateAsync return Excalibur.Dispatch.Resilience.CircuitState.
//   FR-116-4  CircuitBreakerOpenException.RetryAfter is non-negative when present.

using Excalibur.Dispatch.CloudNative;
using Excalibur.Dispatch.Resilience;
using Excalibur.Dispatch.Resilience.Polly;

namespace Excalibur.Dispatch.Middleware.Tests.Resilience.Conformance;

// ---------------------------------------------------------------------------
// Thin test-internal adapter — normalises the three CB interfaces behind one seam.
// ---------------------------------------------------------------------------

/// <summary>
/// Minimal test interface that unifies <see cref="IResiliencePattern"/> and
/// <see cref="IDistributedCircuitBreaker"/> behind a single seam for the conformance suite.
/// </summary>
internal interface ICircuitBreakerTestSut : IAsyncDisposable
{
	/// <summary>Gets the CB name (used for assertion messages).</summary>
	string Name { get; }

	/// <summary>Returns the current circuit state without executing any operation.</summary>
	ValueTask<CircuitState> GetStateAsync(CancellationToken ct);

	/// <summary>
	/// Executes the given <paramref name="operation"/> through the circuit breaker.
	/// Propagates whatever exception the operation (or the CB itself) throws.
	/// </summary>
	Task ExecuteAsync(Func<Task> operation, CancellationToken ct);
}

/// <summary>
/// Wraps <see cref="IResiliencePattern"/> implementations
/// (<see cref="CircuitBreakerPattern"/>, <see cref="PollyCircuitBreakerAdapter"/>).
/// </summary>
internal sealed class ResiliencePatternSut(IResiliencePattern pattern, string name) : ICircuitBreakerTestSut
{
	public string Name => name;

	public ValueTask<CircuitState> GetStateAsync(CancellationToken ct) =>
		ValueTask.FromResult(pattern.State);

	public async Task ExecuteAsync(Func<Task> operation, CancellationToken ct) =>
		await pattern.ExecuteAsync<bool>(
			async () => { await operation().ConfigureAwait(false); return true; },
			ct).ConfigureAwait(false);

	public async ValueTask DisposeAsync()
	{
		if (pattern is IAsyncDisposable d)
		{
			await d.DisposeAsync().ConfigureAwait(false);
		}
	}
}

/// <summary>
/// Wraps <see cref="DistributedCircuitBreaker"/> (async state, distributed-cache-backed).
/// </summary>
internal sealed class DistributedCircuitBreakerSut(DistributedCircuitBreaker cb) : ICircuitBreakerTestSut
{
	public string Name => cb.Name;

	public async ValueTask<CircuitState> GetStateAsync(CancellationToken ct) =>
		await cb.GetStateAsync(ct).ConfigureAwait(false);

	public async Task ExecuteAsync(Func<Task> operation, CancellationToken ct) =>
		await cb.ExecuteAsync<bool>(
			async () => { await operation().ConfigureAwait(false); return true; },
			ct).ConfigureAwait(false);

	public ValueTask DisposeAsync() => cb.DisposeAsync();
}

// ---------------------------------------------------------------------------
// Abstract conformance test base — one subclass per CB implementation.
// ---------------------------------------------------------------------------

/// <summary>
/// Abstract base class for circuit-breaker behavioral conformance tests (bd-ccyett).
/// Subclasses implement <see cref="CreateSut"/> to supply the CB under test;
/// the base class asserts the shared behavioral contract.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Resilience)]
public abstract class CircuitBreakerConformanceBase : IAsyncLifetime
{
	/// <summary>
	/// Failure threshold used for all CBs in this suite.
	/// Chosen large enough that a single transient error does not trigger a flip,
	/// but small enough that the "drive-to-open" sequence completes quickly.
	/// </summary>
	protected const int ConformanceFailureThreshold = 3;

	/// <summary>Break duration kept long so no HalfOpen transitions happen during state assertions.</summary>
	protected static readonly TimeSpan ConformanceOpenDuration = TimeSpan.FromSeconds(60);

	/// <summary>Success threshold for half-open recovery (used where configurable).</summary>
	protected const int ConformanceSuccessThreshold = 1;

	private ICircuitBreakerTestSut _sut = null!;

	/// <summary>
	/// Creates the CB implementation under test with the specified parameters.
	/// All parameters must map to equivalent concepts in the CB's native options type.
	/// </summary>
	/// <param name="failureThreshold">
	///     Number of consecutive failures (or min-throughput) required to open.
	/// </param>
	/// <param name="openDuration">Time the circuit stays Open before entering HalfOpen.</param>
	/// <param name="successThreshold">Successes required to transition HalfOpen → Closed.</param>
	private protected abstract ICircuitBreakerTestSut CreateSut(
		int failureThreshold,
		TimeSpan openDuration,
		int successThreshold);

	/// <summary>xUnit lifecycle — create the SUT.</summary>
	public ValueTask InitializeAsync()
	{
		_sut = CreateSut(ConformanceFailureThreshold, ConformanceOpenDuration, ConformanceSuccessThreshold);
		return ValueTask.CompletedTask;
	}

	/// <summary>xUnit lifecycle — dispose the SUT.</summary>
	public async ValueTask DisposeAsync()
	{
		if (_sut != null)
		{
			await _sut.DisposeAsync().ConfigureAwait(false);
		}
	}

	// -----------------------------------------------------------------------
	// FR-116-3: canonical CircuitState
	// -----------------------------------------------------------------------

	/// <summary>FR-116-3: all CBs start in the canonical Closed state.</summary>
	[Fact]
	public async Task StartInClosedState_FR116_3()
	{
		// Act
		var state = await _sut.GetStateAsync(CancellationToken.None).ConfigureAwait(false);

		// Assert — FR-116-3: must return Excalibur.Dispatch.Resilience.CircuitState.Closed (= 0).
		state.ShouldBe(CircuitState.Closed, $"{_sut.Name}: expected initial state Closed (FR-116-3)");
	}

	// -----------------------------------------------------------------------
	// Closed-state happy path
	// -----------------------------------------------------------------------

	/// <summary>Operations execute successfully in Closed state.</summary>
	[Fact]
	public async Task ClosedState_AllowsExecution()
	{
		// Arrange
		var executed = false;

		// Act
		await _sut.ExecuteAsync(async () =>
		{
			executed = true;
			await Task.CompletedTask.ConfigureAwait(false);
		}, CancellationToken.None).ConfigureAwait(false);

		// Assert
		executed.ShouldBeTrue($"{_sut.Name}: operation should execute in Closed state");
	}

	// -----------------------------------------------------------------------
	// FR-116-2: OperationCanceledException never trips the breaker
	// -----------------------------------------------------------------------

	/// <summary>
	/// FR-116-2: an OperationCanceledException thrown by the operation must NOT count
	/// as a failure — the circuit must remain Closed.
	/// </summary>
	[Fact]
	public async Task OCE_DoesNotTrip_CircuitBreaker_FR116_2()
	{
		// Arrange — the operation throws an OCE (simulating caller cancellation).
		static Task OceOperation() => throw new OperationCanceledException("simulated cancellation");

		// Act — the OCE should propagate; the CB must NOT count it as a failure.
		await Should.ThrowAsync<OperationCanceledException>(
			() => _sut.ExecuteAsync(OceOperation, CancellationToken.None))
			.ConfigureAwait(false);

		// Assert — state must remain Closed; OCE is never a CB failure (FR-116-2).
		var state = await _sut.GetStateAsync(CancellationToken.None).ConfigureAwait(false);
		state.ShouldBe(CircuitState.Closed,
			$"{_sut.Name}: OperationCanceledException must NOT trip the circuit breaker (FR-116-2)");
	}

	// -----------------------------------------------------------------------
	// FR-116-1 + FR-116-3: opening the circuit; canonical exception + state
	// -----------------------------------------------------------------------

	/// <summary>
	/// FR-116-3: after enough failures the CB transitions to Open state
	/// (canonical CircuitState.Open = 1).
	/// </summary>
	[Fact]
	public async Task AfterThresholdFailures_StateIsOpen_FR116_3()
	{
		// Arrange — drive the CB into Open by triggering enough failures.
		await DriveToOpenAsync(_sut).ConfigureAwait(false);

		// Assert — FR-116-3: state must be CircuitState.Open (= 1).
		var state = await _sut.GetStateAsync(CancellationToken.None).ConfigureAwait(false);
		state.ShouldBe(CircuitState.Open,
			$"{_sut.Name}: expected Open state after {ConformanceFailureThreshold} failures (FR-116-3)");
	}

	/// <summary>
	/// FR-116-1: when Open the CB must throw <see cref="CircuitBreakerOpenException"/>
	/// (the canonical exception type — never InvalidOperationException or Polly's BrokenCircuitException).
	/// </summary>
	[Fact]
	public async Task OpenState_ThrowsCircuitBreakerOpenException_FR116_1()
	{
		// Arrange — drive the CB into Open state.
		await DriveToOpenAsync(_sut).ConfigureAwait(false);

		// Act — next call must be rejected with the canonical exception.
		var ex = await Should.ThrowAsync<CircuitBreakerOpenException>(
			() => _sut.ExecuteAsync(() => Task.CompletedTask, CancellationToken.None))
			.ConfigureAwait(false);

		// Assert
		ex.ShouldNotBeNull($"{_sut.Name}: Open state must throw CircuitBreakerOpenException (FR-116-1)");
	}

	/// <summary>
	/// FR-116-1 + FR-116-4: when Open the thrown <see cref="CircuitBreakerOpenException"/>
	/// must carry a non-null <see cref="CircuitBreakerOpenException.CircuitName"/>,
	/// and <see cref="CircuitBreakerOpenException.RetryAfter"/> (when set) must be non-negative.
	/// </summary>
	[Fact]
	public async Task OpenException_HasValidCircuitName_FR116_1_FR116_4()
	{
		// Arrange
		await DriveToOpenAsync(_sut).ConfigureAwait(false);

		// Act
		var ex = await Should.ThrowAsync<CircuitBreakerOpenException>(
			() => _sut.ExecuteAsync(() => Task.CompletedTask, CancellationToken.None))
			.ConfigureAwait(false);

		// Assert FR-116-1: CircuitName must be non-null
		ex.CircuitName.ShouldNotBeNullOrWhiteSpace(
			$"{_sut.Name}: CircuitBreakerOpenException.CircuitName must be set (FR-116-1)");

		// Assert FR-116-4: RetryAfter, when present, must be non-negative
		if (ex.RetryAfter.HasValue)
		{
			ex.RetryAfter.Value.ShouldBeGreaterThanOrEqualTo(TimeSpan.Zero,
				$"{_sut.Name}: CircuitBreakerOpenException.RetryAfter must be >= 0 (FR-116-4)");
		}
	}

	// -----------------------------------------------------------------------
	// Helper: drive the CB to Open state.
	// -----------------------------------------------------------------------

	/// <summary>
	/// Drives <paramref name="sut"/> into Open state by executing
	/// <see cref="ConformanceFailureThreshold"/> failing operations plus one
	/// additional call that elicits the <see cref="CircuitBreakerOpenException"/>
	/// and therefore sets the state to Open on CBs that track state reactively.
	/// </summary>
	private static async Task DriveToOpenAsync(ICircuitBreakerTestSut sut)
	{
		// Trigger enough failures for all three CB implementations.
		for (var i = 0; i < ConformanceFailureThreshold; i++)
		{
			// Ignore the failure exception — we only care that it's recorded.
			_ = await Record.ExceptionAsync(
				() => sut.ExecuteAsync(
					static () => throw new InvalidOperationException("conformance-failure"),
					CancellationToken.None))
				.ConfigureAwait(false);
		}

		// For PollyCircuitBreakerAdapter the State property only updates reactively
		// (when a BrokenCircuitException is caught).  Fire one more request so that
		// Polly's already-open CB emits the BrokenCircuitException, which the adapter
		// translates to CircuitBreakerOpenException and sets State = Open.
		// For CBs that are already in Open state this is a no-op (they throw CBOE immediately).
		_ = await Record.ExceptionAsync(
			() => sut.ExecuteAsync(
				static () => throw new InvalidOperationException("conformance-probe"),
				CancellationToken.None))
			.ConfigureAwait(false);
	}
}
