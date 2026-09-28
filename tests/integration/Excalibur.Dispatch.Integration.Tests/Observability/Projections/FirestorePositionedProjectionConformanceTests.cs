// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Data.Firestore.Projections;
using Excalibur.EventSourcing;
using Excalibur.Testing.Conformance;

using Google.Cloud.Firestore;

using Microsoft.Extensions.Logging.Abstractions;

using Testcontainers.Firestore;

using Tests.Shared.Infrastructure;

namespace Excalibur.Dispatch.Integration.Tests.Observability.Projections;

/// <summary>
/// A Firestore emulator for the positioned-projection conformance kit.
/// </summary>
#pragma warning disable CA1812 // Instantiated by the xUnit test runner via IClassFixture<T>.
public sealed class FirestorePositionedProjectionContainerFixture : ContainerFixtureBase
{
	private FirestoreContainer? _container;

	/// <summary>Gets the emulator-connected Firestore client.</summary>
	/// <value>The client the store under test writes through.</value>
	public FirestoreDb Db { get; private set; } = null!;

	/// <summary>Gets the emulator project id.</summary>
	/// <value>A fixed id; the emulator does not validate it.</value>
	public string ProjectId { get; } = "positioned-conformance";

	/// <inheritdoc/>
	protected override TimeSpan ContainerStartTimeout => TimeSpan.FromMinutes(6);

	/// <inheritdoc/>
	protected override async Task InitializeContainerAsync(CancellationToken cancellationToken)
	{
		_container = new FirestoreBuilder()
			.WithImage(TestContainerImages.GoogleCloudEmulators)
			.WithCleanUp(true)
			.Build();

		await _container.StartAsync(cancellationToken).ConfigureAwait(false);

		// EmulatorOnly and an explicit Endpoint are MUTUALLY EXCLUSIVE, and the sibling Firestore
		// fixtures in this tree already say so: EmulatorOnly makes the SDK build its own channel from
		// FIRESTORE_EMULATOR_HOST, so supplying Endpoint as well throws from GaxPreconditions before a
		// single request is sent. Setting only Endpoint is worse than failing -- the SDK then behaves as
		// though this were a real deployment and the emulator rejects admin-ish calls with
		// PermissionDenied. The variable is set from THIS fixture's own container, so it cannot point at
		// a foreign emulator.
		Environment.SetEnvironmentVariable("FIRESTORE_EMULATOR_HOST", _container.GetEmulatorEndpoint());

		Db = await new FirestoreDbBuilder
		{
			ProjectId = ProjectId,
			EmulatorDetection = Google.Api.Gax.EmulatorDetection.EmulatorOnly,
		}.BuildAsync(cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc/>
	protected override async Task DisposeContainerAsync(CancellationToken cancellationToken)
	{
		try
		{
			if (_container is not null)
			{
				await _container.DisposeAsync().ConfigureAwait(false);
			}
		}
		catch (Exception)
		{
			// Best effort - allow the test host to exit cleanly.
		}
	}
}
#pragma warning restore CA1812

/// <summary>
/// Runs the positioned-projection conformance kit against a REAL Firestore emulator.
/// </summary>
/// <remarks>
/// <para>
/// <b>This provider is the only one whose conditional write is a genuine transaction</b>, so it is the
/// one where the contract's two conjuncts are checked and applied without any second mechanism standing
/// in for atomicity — no ETag, no sequence pair, no condition expression. That makes it the most likely
/// of the eight to be correct by construction and the least likely to be correct by accident, which is
/// exactly why it is worth executing rather than reasoning about.
/// </para>
/// <para>
/// The arm that matters most here is the re-delivery one. The SDK retries an aborted transaction
/// automatically, re-running the callback; because the callback re-reads and re-derives its outcome
/// from what it finds, a <c>Superseded</c> is reproduced rather than swallowed. That is a claim about
/// the SDK's retry behaviour and nothing but the emulator can settle it.
/// </para>
/// </remarks>
[IntegrationTest]
[Trait("Infrastructure", TestInfrastructure.Firestore)]
[Trait(TraitNames.Category, TestCategories.Integration)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class FirestorePositionedProjectionConformanceTests
	: PositionedProjectionStoreConformanceTestKit,
		IClassFixture<FirestorePositionedProjectionContainerFixture>
{
	private readonly FirestorePositionedProjectionContainerFixture _fixture;
	private readonly string _collection = $"positioned_{Guid.NewGuid():N}";

	public FirestorePositionedProjectionConformanceTests(
		FirestorePositionedProjectionContainerFixture fixture) => _fixture = fixture;

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
		_fixture.DockerAvailable.ShouldBeTrue(
			"a Firestore emulator must be available -- positioned-projection conformance is never "
			+ "skipped, because an arm that passes by being skipped is indistinguishable from one that "
			+ "passed by working, and the transaction semantics under test belong to the service.");

		var options = Microsoft.Extensions.Options.Options.Create(new FirestoreProjectionStoreOptions
		{
			CollectionName = _collection,
			ProjectId = _fixture.ProjectId,
		});

		return Task.FromResult<IProjectionStore<ConformanceProjection>>(
			new FirestoreProjectionStore<ConformanceProjection>(
				_fixture.Db,
				options,
				NullLogger<FirestoreProjectionStore<ConformanceProjection>>.Instance));
	}
}
