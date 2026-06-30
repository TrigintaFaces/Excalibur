// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch;
using Excalibur.Data.ElasticSearch.Resilience;
using Excalibur.Dispatch.Resilience;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Data.Tests.ElasticSearch.Resilience;

/// <summary>
/// Author≠impl regression locks for bead <c>bd-116roh</c> (S856): CB contract unification.
/// </summary>
/// <remarks>
/// <para>
/// <b>Scope:</b> AC-116-1 (ES CB open → <see cref="CircuitBreakerOpenException"/>),
/// AC-116-6 (State property returns <see cref="CircuitState"/>), and AC-116-7
/// (<see cref="CircuitBreakerOpenException.RetryAfter"/> non-negative and ≈ remaining break duration).
/// Locks for AC-116-2 (OpenSearch CB) and AC-116-3/4/5 (Distributed/Polly) are authored in their
/// respective test projects by the restarted TestsDeveloper window.
/// </para>
/// <para>
/// <b>Non-vacuity:</b>
/// <list type="bullet">
///   <item>AC-116-1: pre-116roh impl threw <c>InvalidOperationException</c> which is NOT
///     <see cref="CircuitBreakerOpenException"/> → <c>Should.ThrowAsync&lt;CircuitBreakerOpenException&gt;</c>
///     fails (RED). Post-fix: throws <see cref="CircuitBreakerOpenException"/> → GREEN.</item>
///   <item>AC-116-7: RetryAfter assertion fails (RED) if the impl returns a negative TimeSpan.</item>
///   <item>AC-116-6: the StateProperty type assertion fails (RED) if State is declared as any type
///     other than <see cref="CircuitState"/>; the compile-time layer (F-5 sibling flips) is
///     the primary enforcement.</item>
/// </list>
/// </para>
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Data)]
[Trait(TraitNames.Feature, TestFeatures.Resilience)]
public sealed class Cb116rohContractShould : IDisposable
{
	private readonly ElasticsearchCircuitBreaker _sut;

	public Cb116rohContractShould()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new ElasticsearchConfigurationOptions
		{
			Resilience = new ElasticsearchResilienceOptions
			{
				CircuitBreaker = new CircuitBreakerOptions
				{
					Enabled = true,
					FailureThreshold = 3,
					MinimumThroughput = 1,
					BreakDuration = TimeSpan.FromSeconds(30),
					SamplingDuration = TimeSpan.FromMinutes(1),
					FailureRateThreshold = 0.5,
				},
			},
		});

		_sut = new ElasticsearchCircuitBreaker(options, NullLogger<ElasticsearchCircuitBreaker>.Instance);
	}

	/// <summary>
	/// AC-116-1: an open <see cref="ElasticsearchCircuitBreaker"/> throws the canonical
	/// <see cref="CircuitBreakerOpenException"/>, not the old <c>InvalidOperationException</c>.
	/// </summary>
	/// <remarks>
	/// <b>Non-vacuous (RED pre-fix):</b> the pre-116roh impl threw <c>InvalidOperationException</c>
	/// which is NOT assignable to <see cref="CircuitBreakerOpenException"/> → assertion FAILS → RED.
	/// </remarks>
	[Fact]
	public async Task ThrowCanonicalCircuitBreakerOpenException_WhenElasticsearchCbIsOpen()
	{
		// Arrange — exceed failure threshold to open the circuit
		await _sut.RecordFailureAsync();
		await _sut.RecordFailureAsync();
		await _sut.RecordFailureAsync();
		_sut.IsOpen.ShouldBeTrue("bd-116roh AC-116-1 arrange: circuit must be open.");

		// Act & Assert — canonical exception, NOT InvalidOperationException
		await Should.ThrowAsync<CircuitBreakerOpenException>(
			() => _sut.ExecuteAsync(() => Task.FromResult(42), CancellationToken.None));
	}

	/// <summary>
	/// AC-116-7: the <see cref="CircuitBreakerOpenException"/> thrown when the
	/// <see cref="ElasticsearchCircuitBreaker"/> is open has a non-negative
	/// <see cref="CircuitBreakerOpenException.RetryAfter"/> that does not exceed the configured
	/// break duration.
	/// </summary>
	[Fact]
	public async Task CanonicalException_HasNonNegativeRetryAfter_ForElasticsearchCb()
	{
		// Arrange — open the circuit (configured 30s break duration)
		await _sut.RecordFailureAsync();
		await _sut.RecordFailureAsync();
		await _sut.RecordFailureAsync();
		_sut.IsOpen.ShouldBeTrue("bd-116roh AC-116-7 arrange: circuit must be open.");

		// Act
		var ex = await Should.ThrowAsync<CircuitBreakerOpenException>(
			() => _sut.ExecuteAsync(() => Task.FromResult(0), CancellationToken.None));

		// Assert — RetryAfter in [0, 31s]: break duration 30s + 1s tolerance for execution time
		ex.RetryAfter.ShouldNotBeNull(
			"AC-116-7: RetryAfter must be set when the ES CB throws with a configured break duration.");
		ex.RetryAfter.Value.ShouldBeGreaterThanOrEqualTo(TimeSpan.Zero,
			"AC-116-7: RetryAfter must be non-negative — a negative value indicates a clock or init bug.");
		ex.RetryAfter.Value.ShouldBeLessThanOrEqualTo(TimeSpan.FromSeconds(31),
			"AC-116-7: RetryAfter must not exceed the 30s configured break duration (+ 1s tolerance).");
	}

	/// <summary>
	/// AC-116-6 (runtime layer): the <see cref="ElasticsearchCircuitBreaker.State"/> property
	/// returns the canonical <see cref="CircuitState"/> type — not a removed per-package
	/// <c>CircuitBreakerState</c>. The compile-time enforcement is the F-5 sibling flip
	/// in <c>CircuitBreakerStateShould.cs</c> and <c>IElasticsearchCircuitBreakerShould.cs</c>.
	/// </summary>
	[Fact]
	public void StateProperty_ReturnsCanonicalCircuitStateType_ForElasticsearchCb()
	{
		// Assert — the concrete impl's State property must be declared as CircuitState
		var stateProperty = typeof(ElasticsearchCircuitBreaker).GetProperty("State");
		stateProperty.ShouldNotBeNull("ElasticsearchCircuitBreaker.State property must exist.");
		stateProperty.PropertyType.ShouldBe(typeof(CircuitState),
			"AC-116-6: State must be Excalibur.Dispatch.Resilience.CircuitState (canonical), "
			+ "not a per-package CircuitBreakerState. This is the runtime layer of the compile-time "
			+ "F-5 sibling enforcement.");
	}

	public void Dispose() => _sut.Dispose();
}
