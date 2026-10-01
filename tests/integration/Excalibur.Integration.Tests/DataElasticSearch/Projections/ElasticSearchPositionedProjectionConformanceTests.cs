// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Elastic.Clients.Elasticsearch;

using Excalibur.Data.ElasticSearch.Projections;
using Excalibur.EventSourcing;
using Excalibur.Testing.Conformance;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Excalibur.Integration.Tests.DataElasticSearch.Infrastructure.TestBaseClasses;
using Tests.Shared.Fixtures;

namespace Excalibur.Integration.Tests.DataElasticSearch.Projections;

/// <summary>
/// Runs the positioned-projection conformance kit against a REAL Elasticsearch server.
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
[Trait("Database", "Elasticsearch")]
[Trait("Component", "Projections")]
[Collection(nameof(ElasticsearchHostTests))]
public sealed class ElasticSearchPositionedProjectionConformanceTests
	: PositionedProjectionStoreConformanceTestKit, IClassFixture<ElasticsearchContainerFixture>
{
	private readonly ElasticsearchContainerFixture _fixture;
	private readonly string _indexSuffix = Guid.NewGuid().ToString("N");

	public ElasticSearchPositionedProjectionConformanceTests(ElasticsearchContainerFixture fixture) =>
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
	public Task Refuse_a_row_that_carries_no_position_Test() => Refuse_a_row_that_carries_no_position();

	// The liveness half of the pair above, and what makes that refusal legitimate rather than a stall:
	// without it the refusal is satisfied by a store that refuses everything.
	[Fact]
	public Task Rebuild_repairs_a_row_that_carries_no_position_Test() =>
		Rebuild_repairs_a_row_that_carries_no_position();

	// The READ side of the two no-number states. Both writes are refused; what differs is which state
	// the row reads back as -- UNPLACEABLE here, UNNUMBERED above -- and a store that dropped the
	// position instead of recording the sentinel passes the refusal and fails this.
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

	// The UNNUMBERED half of the pair above: that arm drives the row through the blind surface, this one
	// writes a complete fold with no number. A store discriminating on == Unplaceable rather than
	// != Positioned passes one and fails the other.
	[Fact]
	public Task Report_requires_rebuild_for_a_refold_against_an_unnumbered_row_Test() =>
		Report_requires_rebuild_for_a_refold_against_an_unnumbered_row();

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

	/// <summary>A rebuild must never resurrect a row an erasure deleted.</summary>
	/// <returns>A task representing the arm.</returns>
	[Fact]
	public Task Rebuild_must_not_resurrect_a_deleted_row_Test() => Rebuild_must_not_resurrect_a_deleted_row();

	/// <inheritdoc />
	protected override Task<IProjectionStore<ConformanceProjection>> CreateStoreAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"an Elasticsearch container must be available -- positioned-projection conformance is never "
			+ "skipped, because an arm that passes by being skipped is indistinguishable from one that "
			+ "passed by working, and every property under test is decided by the server.");

		var options = new ElasticSearchProjectionStoreOptions
		{
			NodeUri = _fixture.ConnectionString,
		};
		options.Index.IndexPrefix = "positioned-conformance-" + _indexSuffix;

		var client = new ElasticsearchClient(new Uri(_fixture.ConnectionString));

		return Task.FromResult<IProjectionStore<ConformanceProjection>>(
			new ElasticSearchProjectionStore<ConformanceProjection>(
				client,
				new SingleNamedOptionsMonitor(options),
				NullLogger<ElasticSearchProjectionStore<ConformanceProjection>>.Instance));
	}

	/// <summary>
	/// Serves one options instance under every name.
	/// </summary>
	/// <remarks>
	/// The store resolves its options by the projection type's name. A directly-constructed store has no
	/// DI container to register that name against, so this returns the same instance whatever is asked
	/// for — which is correct here because the suite has exactly one projection type.
	/// </remarks>
	private sealed class SingleNamedOptionsMonitor(ElasticSearchProjectionStoreOptions options)
		: IOptionsMonitor<ElasticSearchProjectionStoreOptions>
	{
		public ElasticSearchProjectionStoreOptions CurrentValue => options;

		public ElasticSearchProjectionStoreOptions Get(string? name) => options;

		public IDisposable? OnChange(Action<ElasticSearchProjectionStoreOptions, string?> listener) => null;
	}
}
