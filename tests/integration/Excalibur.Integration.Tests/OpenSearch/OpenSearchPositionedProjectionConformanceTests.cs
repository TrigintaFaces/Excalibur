// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Data.OpenSearch.Projections;
using Excalibur.EventSourcing;
using Excalibur.Testing.Conformance;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using OpenSearch.Client;

using Tests.Shared.Fixtures;

namespace Excalibur.Integration.Tests.OpenSearch;

/// <summary>
/// Runs the positioned-projection conformance kit against a REAL OpenSearch server.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this cannot be a unit test, and why it matters more here than anywhere else.</b> This provider
/// does not have a conditional write — it has a READ that yields a sequence number and primary term,
/// followed by a write conditioned on that pair. Whether the pair still identifies the same document
/// version at the instant the write lands is decided entirely by the server. It is also the provider
/// where the tempting wrong answer lives: external versioning looks purpose-built for this and gives
/// only monotonicity, which admits a writer holding a stale read.
/// </para>
/// <para>
/// A second thing only a real server can settle: the position lives in a reserved document field the
/// projection type does not declare, so it has to survive whatever mapping the index was created with.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Database", "OpenSearch")]
[Trait("Component", "Projections")]
[Collection(OpenSearchTestCollection.CollectionName)]
public sealed class OpenSearchPositionedProjectionConformanceTests
	: PositionedProjectionStoreConformanceTestKit, IClassFixture<OpenSearchContainerFixture>
{
	private readonly OpenSearchContainerFixture _fixture;
	private readonly string _indexSuffix = Guid.NewGuid().ToString("N");

	public OpenSearchPositionedProjectionConformanceTests(OpenSearchContainerFixture fixture) =>
		_fixture = fixture;

	/// <summary>Wires the kit's own completeness check, which names any arm this suite forgot.</summary>
	/// <returns>A task representing the check.</returns>
	[Fact]
	public Task ConformanceSuite_ShouldWireEveryArm_Test() => ConformanceSuite_ShouldWireEveryArm();

	[Fact]
	public Task Create_when_nothing_is_stored_Test() => Create_when_nothing_is_stored();

	[Fact]
	public Task Refuse_a_late_starter_that_claims_absence_Test() => Refuse_a_late_starter_that_claims_absence();

	[Fact]
	public Task Adopt_a_row_that_carries_no_position_Test() => Adopt_a_row_that_carries_no_position();

	// The safety half of the pair above: that arm requires a COMPLETE FOLD with no number to be
	// adopted, this one requires a row the blind surface left UNPLACEABLE to be refused. Either
	// alone is satisfiable by a store that treats both the same, which is what every provider did.
	[Fact]
	public Task Refuse_to_adopt_a_row_an_unconditional_write_left_unplaceable_Test() =>
		Refuse_to_adopt_a_row_an_unconditional_write_left_unplaceable();

	[Fact]
	public Task Advance_from_the_position_it_read_Test() => Advance_from_the_position_it_read();

	[Fact]
	public Task Refuse_a_stale_expected_position_Test() => Refuse_a_stale_expected_position();

	[Fact]
	public Task Refuse_a_position_that_does_not_advance_Test() => Refuse_a_position_that_does_not_advance();

	[Fact]
	public Task Report_a_deleted_projection_as_vanished_without_recreating_it_Test() =>
		Report_a_deleted_projection_as_vanished_without_recreating_it();

	[Fact]
	public Task Admit_exactly_one_of_two_writers_racing_from_one_read_Test() =>
		Admit_exactly_one_of_two_writers_racing_from_one_read();

	[Fact]
	public Task Invalidate_the_position_when_an_unconditional_write_replaces_the_state_Test() =>
		Invalidate_the_position_when_an_unconditional_write_replaces_the_state();

	[Fact]
	public Task Read_the_state_and_its_position_as_one_observation_Test() =>
		Read_the_state_and_its_position_as_one_observation();

	/// <summary>
	/// The RE-FOLD arms. Erasure mutates events in place, so the fold beneath a position changes while
	/// the position stays fixed — a state transition the advancing write cannot express.
	/// </summary>
	/// <returns>A task representing the arm.</returns>
	[Fact]
	public Task Refold_the_state_at_the_position_the_row_already_holds_Test() =>
		Refold_the_state_at_the_position_the_row_already_holds();

	/// <summary>Refuses a re-fold the row moved out from under.</summary>
	/// <returns>A task representing the arm.</returns>
	[Fact]
	public Task Refuse_a_refold_when_the_row_advanced_after_the_read_Test() =>
		Refuse_a_refold_when_the_row_advanced_after_the_read();

	/// <summary>Refuses a re-fold naming a position the row does not hold.</summary>
	/// <returns>A task representing the arm.</returns>
	[Fact]
	public Task Refuse_a_refold_at_a_position_the_row_does_not_hold_Test() =>
		Refuse_a_refold_at_a_position_the_row_does_not_hold();

	/// <summary>An unpositioned row is terminal, not retryable.</summary>
	/// <returns>A task representing the arm.</returns>
	[Fact]
	public Task Report_requires_rebuild_for_a_row_with_no_established_position_Test() =>
		Report_requires_rebuild_for_a_row_with_no_established_position();

	/// <summary>A repeated re-fold leaves the row identical.</summary>
	/// <returns>A task representing the arm.</returns>
	[Fact]
	public Task Apply_the_same_refold_twice_without_changing_the_result_Test() =>
		Apply_the_same_refold_twice_without_changing_the_result();

	/// <summary>A re-fold must never resurrect a row erasure deleted.</summary>
	/// <returns>A task representing the arm.</returns>
	[Fact]
	public Task Report_a_refold_against_an_absent_row_as_vanished_without_creating_it_Test() =>
		Report_a_refold_against_an_absent_row_as_vanished_without_creating_it();

	/// <summary>A negative position is refused at the entry point, not stored.</summary>
	/// <returns>A task representing the arm.</returns>
	[Fact]
	public Task Refuse_a_negative_position_argument_Test() => Refuse_a_negative_position_argument();

	/// <inheritdoc />
	protected override Task<IProjectionStore<ConformanceProjection>> CreateStoreAsync()
	{
		_fixture.Available.ShouldBeTrue(
			"an OpenSearch container must be available -- positioned-projection conformance is never "
			+ "skipped, because an arm that passes by being skipped is indistinguishable from one that "
			+ "passed by working, and every property under test is decided by the server.");

		var options = new OpenSearchProjectionStoreOptions
		{
			NodeUri = _fixture.Endpoint.ToString(),
			IndexName = "positioned-conformance-" + _indexSuffix,
		};

		var client = new OpenSearchClient(new ConnectionSettings(_fixture.Endpoint));

		return Task.FromResult<IProjectionStore<ConformanceProjection>>(
			new OpenSearchProjectionStore<ConformanceProjection>(
				client,
				new SingleNamedOptionsMonitor(options),
				NullLogger<OpenSearchProjectionStore<ConformanceProjection>>.Instance));
	}

	/// <summary>
	/// Serves one options instance under every name.
	/// </summary>
	/// <remarks>
	/// The store resolves its options by the projection type's name. A directly-constructed store has no
	/// DI container to register that name against, so this returns the same instance whatever is asked
	/// for — which is correct here because the suite has exactly one projection type.
	/// </remarks>
	private sealed class SingleNamedOptionsMonitor(OpenSearchProjectionStoreOptions options)
		: IOptionsMonitor<OpenSearchProjectionStoreOptions>
	{
		public OpenSearchProjectionStoreOptions CurrentValue => options;

		public OpenSearchProjectionStoreOptions Get(string? name) => options;

		public IDisposable? OnChange(Action<OpenSearchProjectionStoreOptions, string?> listener) => null;
	}
}
