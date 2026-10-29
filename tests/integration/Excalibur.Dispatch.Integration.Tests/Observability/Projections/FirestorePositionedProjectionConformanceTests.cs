// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Data.Firestore.Projections;
using Excalibur.EventSourcing;
using Excalibur.Testing.Conformance;

using Google.Cloud.Firestore;

using Microsoft.Extensions.DependencyInjection;
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
	private ServiceProvider? _services;

	/// <summary>Gets the emulator-connected Firestore client.</summary>
	/// <value>The client the store under test writes through.</value>
	public FirestoreDb Db { get; private set; } = null!;

    /// <summary>Gets the endpoint of this fixture's emulator.</summary>
    public string Endpoint => _container!.GetEmulatorEndpoint();

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

		// Use the supported registration so transport identity is bound to this exact client.
		// The explicit emulator endpoint needs no process-global environment mutation.
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddExcaliburFirestore(builder => builder.ProjectId(ProjectId).EmulatorHost(Endpoint));
		_services = services.BuildServiceProvider();
		Db = _services.GetRequiredService<FirestoreDb>();
	}

	/// <inheritdoc/>
	protected override async Task DisposeContainerAsync(CancellationToken cancellationToken)
	{
		try
		{
			if (_services is not null)
			{
				await _services.DisposeAsync().ConfigureAwait(false);
			}
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
	: PositionedProjectionDestinationConformanceTestKit,
		IClassFixture<FirestorePositionedProjectionContainerFixture>
{
	private readonly FirestorePositionedProjectionContainerFixture _fixture;
	private readonly string _collection = $"positioned_{Guid.NewGuid():N}";

	public FirestorePositionedProjectionConformanceTests(
		FirestorePositionedProjectionContainerFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task ReadGenerationUsingTheExactFrameworkCreatedClientEndpoint()
    {
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        services.AddLogging();
        services.AddExcaliburFirestore(f => f.ProjectId(_fixture.ProjectId).EmulatorHost(_fixture.Endpoint));
        services.AddFirestoreProjectionStore<ConformanceProjection>(o => o.CollectionName = _collection);
        await using var provider = services.BuildServiceProvider();
        var store = provider.GetRequiredService<IProjectionStore<ConformanceProjection>>();
        await store.UpsertAsync("scope-row", new ConformanceProjection { Id = "scope-row", Total = 21 }, CancellationToken.None);
        var scope = ((IProjectionDestinationScope)store.GetService(typeof(IProjectionDestinationScope))!).Scope;
        var destination = ((IProjectionDestinationIdentity)store.GetService(typeof(IProjectionDestinationIdentity))!).Identity;
        var factory = (IProjectionGenerationReaderFactory<ConformanceProjection>)store.GetService(typeof(IProjectionGenerationReaderFactory<ConformanceProjection>))!;
        await using var reader = factory.OpenReader(destination);
        (await reader.GetDestinationScopeAsync(CancellationToken.None)).ShouldBe(scope);
        (await reader.GetByIdAsync("scope-row", CancellationToken.None))!.Total.ShouldBe(21);
        (await reader.QueryAsync(null, null, CancellationToken.None)).ShouldHaveSingleItem().Total.ShouldBe(21);
        (await reader.CountAsync(null, CancellationToken.None)).ShouldBe(1);
    }

    [Fact]
    public async Task ReadAnExactNextGenerationWithoutMovingTheWriter()
    {
        await using var provider = CreateGenerationProvider();
        var store = provider.GetRequiredService<IProjectionStore<ConformanceProjection>>();
        var next = provider.GetRequiredService<IProjectionStore<ConformanceProjectionNextGeneration>>();
        await next.UpsertAsync("reader-row", new ConformanceProjectionNextGeneration { Id = "reader-row", Total = 37 }, CancellationToken.None);
        var destination = ((IProjectionDestinationIdentity)next.GetService(typeof(IProjectionDestinationIdentity))!).Identity;
        var factory = (IProjectionGenerationReaderFactory<ConformanceProjection>)store.GetService(typeof(IProjectionGenerationReaderFactory<ConformanceProjection>))!;
        await using var reader = factory.OpenReader(destination);
        (await reader.GetDestinationScopeAsync(CancellationToken.None)).ShouldNotBeNull();
        (await reader.GetByIdAsync("reader-row", CancellationToken.None))!.Total.ShouldBe(37);
        (await reader.QueryAsync(null, null, CancellationToken.None)).ShouldHaveSingleItem().Total.ShouldBe(37);
        (await reader.CountAsync(null, CancellationToken.None)).ShouldBe(1);
        (await store.GetByIdAsync("reader-row", CancellationToken.None)).ShouldBeNull();
    }
    [Fact]
    public async Task GateCutoverAndRouteAllReadsToTheSelectedGeneration()
    {
        await using var provider = CreateGenerationProvider();
        await GenerationRoutingAcceptance.VerifyAsync(provider.GetRequiredService<IProjectionStore<ConformanceProjection>>(),
            provider.GetRequiredService<IProjectionStore<ConformanceProjectionNextGeneration>>());
    }

    private ServiceProvider CreateGenerationProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddExcaliburFirestore(f => f.ProjectId(_fixture.ProjectId).EmulatorHost(_fixture.Endpoint));
        services.AddFirestoreProjectionStore<ConformanceProjection>(o => o.CollectionName = _collection);
        services.AddFirestoreProjectionStore<ConformanceProjectionNextGeneration>(o => o.CollectionName = _collection);
        return services.BuildServiceProvider();
    }
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


	/// <summary>The store states which destination it is configured against.</summary>
	/// <returns>A task representing the arm.</returns>
	[Fact]
	public Task Advertise_the_destination_it_is_configured_against_Test() =>
		Advertise_the_destination_it_is_configured_against();

	/// <summary>A generation reader reports one storage scope through both of its members.</summary>
	/// <returns>A task representing the arm.</returns>
	[Fact]
	public Task Report_one_storage_scope_through_both_generation_reader_members_Test() =>
		Report_one_storage_scope_through_both_generation_reader_members();

	/// <summary>Moving the configured destination changes the identity the store advertises.</summary>
	/// <returns>A task representing the arm.</returns>
	[Fact]
	public Task Change_the_identity_when_the_configured_destination_moves_Test() =>
		Change_the_identity_when_the_configured_destination_moves();

	/// <summary>A declared generation resolves a destination of its own, carrying its generation segment.</summary>
	/// <returns>A task representing the arm.</returns>
	[Fact]
	public Task Resolve_a_distinct_destination_for_the_next_generation_Test() =>
		Resolve_a_distinct_destination_for_the_next_generation();

	/// <summary>The previous generation keeps serving its own documents while the next one builds.</summary>
	/// <returns>A task representing the arm.</returns>
	[Fact]
	public Task Keep_the_previous_generation_readable_while_the_next_one_builds_Test() =>
		Keep_the_previous_generation_readable_while_the_next_one_builds();

	/// <inheritdoc />
	protected override Task<IProjectionStore<ConformanceProjection>> CreateRelocatedStoreAsync() =>
		Task.FromResult<IProjectionStore<ConformanceProjection>>(
			new FirestoreProjectionStore<ConformanceProjection>(
				_fixture.Db,
				new SingleNamedFirestoreProjectionOptions(new FirestoreProjectionStoreOptions
				{
					CollectionName = _collection + "-moved",
					ProjectId = _fixture.ProjectId,
				}),
				NullLogger<FirestoreProjectionStore<ConformanceProjection>>.Instance));

	/// <inheritdoc />
	/// <remarks>
	/// Configured against the SAME collection as the baseline, because this provider puts the generation
	/// in the per-projection subcollection segment: the new generation folds into its own subcollection
	/// under the collection already configured. Firestore creates a subcollection on first write.
	/// </remarks>
	protected override Task<IProjectionStore<ConformanceProjectionNextGeneration>>
		CreateNextGenerationStoreAsync() =>
		Task.FromResult<IProjectionStore<ConformanceProjectionNextGeneration>>(
			new FirestoreProjectionStore<ConformanceProjectionNextGeneration>(
				_fixture.Db,
				new SingleNamedFirestoreProjectionOptions(new FirestoreProjectionStoreOptions
				{
					CollectionName = _collection,
					ProjectId = _fixture.ProjectId,
				}),
				NullLogger<FirestoreProjectionStore<ConformanceProjectionNextGeneration>>.Instance));

	/// <inheritdoc />
	protected override Task<IProjectionStore<ConformanceProjection>> CreateStoreAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"a Firestore emulator must be available -- positioned-projection conformance is never "
			+ "skipped, because an arm that passes by being skipped is indistinguishable from one that "
			+ "passed by working, and the transaction semantics under test belong to the service.");

		var options = new SingleNamedFirestoreProjectionOptions(new FirestoreProjectionStoreOptions
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
