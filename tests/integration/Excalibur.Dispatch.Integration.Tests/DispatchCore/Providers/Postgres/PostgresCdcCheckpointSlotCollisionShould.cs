// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc.Postgres;
using Excalibur.Dispatch;

using Shouldly;

using Tests.Shared.Fixtures;

using MsOptions = Microsoft.Extensions.Options.Options;

#pragma warning disable CA1812 // Instantiated by the xUnit test runner.

namespace Excalibur.Dispatch.Integration.Tests.DispatchCore.Providers.Postgres;

/// <summary>
/// Binds the separation between the provider-neutral <see cref="ICdcStateStore"/> checkpoint and the typed
/// per-slot positions that share the same Postgres state table.
/// </summary>
/// <remarks>
/// <para>
/// Both kinds of row carry an empty <c>table_name</c>, so <c>table_name</c> alone cannot tell them apart.
/// When the generic read, delete and enumerate predicates omitted <c>slot_name</c>, a generic consumer
/// resumed from whichever slot had advanced most recently and skipped every change in between. That is
/// silent loss: the consumer sees no error, no gap and no changes, which is indistinguishable from having
/// nothing to do.
/// </para>
/// <para>
/// Every safety arm here is paired with a liveness arm, because each safety property alone is satisfied by
/// a store that returns nothing to anyone - which is a strictly worse version of the same bug. Docker is a
/// hard requirement: an in-memory double cannot reproduce any of this, because it keys entries exactly and
/// so has never had the defect these arms bind.
/// </para>
/// </remarks>
[Collection(ContainerCollections.Postgres)]
[Trait("Category", "Integration")]
[Trait("Component", "Cdc")]
[Trait("Database", "Postgres")]
public sealed class PostgresCdcCheckpointSlotCollisionShould
{
	private const string TypedSlotName = "orders_slot";

	private readonly PostgresFixture _fixture;
	private readonly string _tableName = $"cdc_state_{Guid.NewGuid():N}";

	/// <summary>
	/// Initializes a new instance of the <see cref="PostgresCdcCheckpointSlotCollisionShould"/> class.
	/// </summary>
	/// <param name="fixture">The shared Postgres container fixture.</param>
	public PostgresCdcCheckpointSlotCollisionShould(PostgresFixture fixture)
	{
		_fixture = fixture;
	}

	// ---- SAFETY -------------------------------------------------------------------------------------

	[Fact]
	public async Task NotReturnATypedSlotPositionToTheGenericConsumer()
	{
		var store = CreateStore();
		const string ConsumerId = "orders-sync";

		// The generic consumer checkpoints early, then a typed slot for the SAME processor advances far
		// ahead. Ordering by updated_at puts the typed row first, so an unscoped read returns it.
		await ((ICdcStateStore)store).SavePositionAsync(ConsumerId, Position(1), TestContext.Current.CancellationToken);
		await store.SavePositionAsync(ConsumerId, TypedSlotName, new PostgresCdcPosition(9_000), TestContext.Current.CancellationToken);

		var generic = await ((ICdcStateStore)store).GetPositionAsync(ConsumerId, TestContext.Current.CancellationToken);

		generic.ShouldNotBeNull();
		generic!.ToString().ShouldBe(
			Position(1).ToString(),
			"the generic consumer checkpointed at position 1 and never reached the typed slot's position. " +
			"Returning the typed slot's position makes it resume past changes it never read, and it cannot " +
			"tell that happened - there is no error, no gap and no changes, which looks exactly like an " +
			"idle stream. A duplicate merely reprocesses; a skip loses the change permanently.");
	}

	[Fact]
	public async Task NotDeleteTypedSlotCheckpointsWhenDeletingTheGenericCheckpoint()
	{
		var store = CreateStore();
		const string ConsumerId = "inventory-sync";

		await ((ICdcStateStore)store).SavePositionAsync(ConsumerId, Position(1), TestContext.Current.CancellationToken);
		await store.SavePositionAsync(ConsumerId, TypedSlotName, new PostgresCdcPosition(9_000), TestContext.Current.CancellationToken);

		_ = await ((ICdcStateStore)store).DeletePositionAsync(ConsumerId, TestContext.Current.CancellationToken);

		var typed = await store.GetLastPositionAsync(ConsumerId, TypedSlotName, TestContext.Current.CancellationToken);
		typed.LsnString.ShouldBe(
			new PostgresCdcPosition(9_000).LsnString,
			"deleting the generic checkpoint must not reset a replication slot the caller never named. " +
			"Losing it silently rewinds that slot to the start of the stream on the next resume.");
	}

	[Fact]
	public async Task NotEmitSeveralConflictingPositionsForOneConsumer()
	{
		var store = CreateStore();
		const string ConsumerId = "billing-sync";

		await ((ICdcStateStore)store).SavePositionAsync(ConsumerId, Position(1), TestContext.Current.CancellationToken);
		await store.SavePositionAsync(ConsumerId, TypedSlotName, new PostgresCdcPosition(9_000), TestContext.Current.CancellationToken);
		await store.SavePositionAsync(ConsumerId, "shipping_slot", new PostgresCdcPosition(7_000), TestContext.Current.CancellationToken);

		var emitted = await CollectAsync(store);

		emitted.Count(pair => pair.ConsumerId == ConsumerId).ShouldBe(
			1,
			"the contract yields one (ConsumerId, Position) pair per consumer. Emitting one tuple per slot " +
			"gives a monitor several rows under one id, each claiming to be that consumer's position, with " +
			"nothing to say which is authoritative.");
	}

	// ---- LIVENESS -----------------------------------------------------------------------------------
	// Each arm above is satisfied by a store that returns nothing to anybody. These fail if it does.

	[Fact]
	public async Task StillReturnTheGenericConsumerItsOwnSavedPosition()
	{
		var store = CreateStore();
		const string ConsumerId = "liveness-read";

		await ((ICdcStateStore)store).SavePositionAsync(ConsumerId, Position(5), TestContext.Current.CancellationToken);

		var generic = await ((ICdcStateStore)store).GetPositionAsync(ConsumerId, TestContext.Current.CancellationToken);

		generic.ShouldNotBeNull("a checkpoint that reads back as null makes every consumer resume from the " +
			"beginning of the stream on every restart, which is the safety arms passing by doing nothing.");
		generic!.ToString().ShouldBe(Position(5).ToString());
	}

	[Fact]
	public async Task StillDeleteTheGenericCheckpointAndReportWhetherOneExisted()
	{
		var store = CreateStore();
		const string ConsumerId = "liveness-delete";

		await ((ICdcStateStore)store).SavePositionAsync(ConsumerId, Position(5), TestContext.Current.CancellationToken);

		var deletedExisting = await ((ICdcStateStore)store).DeletePositionAsync(ConsumerId, TestContext.Current.CancellationToken);
		deletedExisting.ShouldBeTrue("a checkpoint existed, so the delete must report that it removed one.");

		(await ((ICdcStateStore)store).GetPositionAsync(ConsumerId, TestContext.Current.CancellationToken))
			.ShouldBeNull("the checkpoint was deleted, so the consumer must now resume from the beginning.");

		var deletedMissing = await ((ICdcStateStore)store).DeletePositionAsync(ConsumerId, TestContext.Current.CancellationToken);
		deletedMissing.ShouldBeFalse("no checkpoint existed, so the contract requires false rather than true.");
	}

	[Fact]
	public async Task StillEnumerateEveryConsumerHoldingAGenericCheckpoint()
	{
		var store = CreateStore();
		var first = $"liveness-all-a-{Guid.NewGuid():N}";
		var second = $"liveness-all-b-{Guid.NewGuid():N}";

		await ((ICdcStateStore)store).SavePositionAsync(first, Position(3), TestContext.Current.CancellationToken);
		await ((ICdcStateStore)store).SavePositionAsync(second, Position(4), TestContext.Current.CancellationToken);

		var emitted = await CollectAsync(store);

		emitted.ShouldContain(pair => pair.ConsumerId == first,
			"scoping the enumerate predicate must not empty it - an enumeration that returns nothing " +
			"satisfies every safety arm above while reporting no consumer progress at all.");
		emitted.ShouldContain(pair => pair.ConsumerId == second);
	}

	[Fact]
	public async Task StillResumeAConsumerCheckpointedBeforeTheReservedSlotExisted()
	{
		var store = CreateStore();
		const string ConsumerId = "legacy-consumer";

		// A deployment that checkpointed under the previous scheme stored the generic position under the
		// literal slot name "default". Those rows must still resolve, or upgrading silently rewinds every
		// consumer to the start of the stream and reprocesses the whole backlog.
		await store.SavePositionAsync(ConsumerId, "default", new PostgresCdcPosition(4_242), TestContext.Current.CancellationToken);

		var generic = await ((ICdcStateStore)store).GetPositionAsync(ConsumerId, TestContext.Current.CancellationToken);

		generic.ShouldNotBeNull("an existing checkpoint written before the reserved slot name existed must " +
			"still be found, otherwise the upgrade itself causes mass reprocessing.");
		generic!.ToString().ShouldBe(new PostgresCdcPosition(4_242).ToChangePosition().ToString());
	}

	// ---- helpers ------------------------------------------------------------------------------------

	private PostgresCdcStateStore CreateStore()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"Docker/Postgres must be available - the checkpoint separation is a real-infra lock and must " +
			"never be skipped. An in-memory double keys its entries exactly and cannot reproduce the SQL " +
			"predicate this binds.");

		return new PostgresCdcStateStore(
			_fixture.ConnectionString,
			MsOptions.Create(new PostgresCdcStateStoreOptions { SchemaName = "public", TableName = _tableName }));
	}

	private static ChangePosition Position(int index) =>
		new PostgresCdcPosition((ulong)((index + 1) * 1000)).ToChangePosition();

	private static async Task<List<(string ConsumerId, ChangePosition Position)>> CollectAsync(ICdcStateStore store)
	{
		var collected = new List<(string ConsumerId, ChangePosition Position)>();
		await foreach (var pair in store.GetAllPositionsAsync(TestContext.Current.CancellationToken))
		{
			collected.Add(pair);
		}

		return collected;
	}
}
