// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Compliance;
using Excalibur.EventSourcing;
using Excalibur.EventSourcing.Erasure;

using FakeItEasy;

using Microsoft.Extensions.Logging.Abstractions;

using Shouldly;

using Xunit;

namespace Excalibur.EventSourcing.Tests.Core.Erasure;

/// <summary>
/// The order in which an erasure destroys an aggregate's event rows and its snapshot, and what a
/// retry does after the two halves have diverged.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why the order is a correctness property and not a preference.</b> Nothing spans the event store
/// and the snapshot store transactionally — they are separate stores and frequently separate databases
/// — so one of the two possible orders has to survive a fault between them, and only one does.
/// </para>
/// <para>
/// Tombstone first, and the aggregate load returns the FULL PRE-ERASURE STATE for as long as the
/// snapshot lives. A snapshot at count <c>N</c> over events <c>0..N-1</c> makes the load compute
/// <c>fromVersion = N-1</c> and fetch zero event rows, so the tombstone check never sees a row and the
/// erased sentinel never returns — the snapshot is applied and handed back. Delete the snapshot first
/// and the same fault leaves a not-yet-erased aggregate with no snapshot, which costs a slower
/// rehydrate from its own events and loses nothing, because a snapshot is derived state.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
public sealed class ErasureDeletesTheSnapshotBeforeTheTombstoneShould
{
	private readonly IEventStoreErasure _eventStoreErasure = A.Fake<IEventStoreErasure>();
	private readonly ISnapshotStore _snapshotStore = A.Fake<ISnapshotStore>();
	private readonly IAggregateDataSubjectMapping _mapping = A.Fake<IAggregateDataSubjectMapping>();

	private EventStoreErasureContributor CreateSut() =>
		new(_eventStoreErasure, _mapping,
			NullLogger<EventStoreErasureContributor>.Instance,
			_snapshotStore,
			serviceProvider: null,
			retentions: null);

	private static ErasureContributorContext CreateContext() =>
		new()
		{
			RequestId = Guid.NewGuid(),
			DataSubjectIdHash = "hash-123",
			IdType = DataSubjectIdType.UserId,
			Scope = ErasureScope.User,
			TenantId = null,
		};

	private void ResolveTo(ErasureContributorContext context, string id, string type) =>
		A.CallTo(() => _mapping.GetAggregatesForDataSubjectAsync(
				context.DataSubjectIdHash, context.TenantId, A<CancellationToken>._))
			.Returns(new List<AggregateReference> { new(id, type) });

	// SAFETY. The snapshot must be gone BEFORE the events are tombstoned, so that a fault between the
	// two stores can never leave a readable snapshot behind a tombstoned stream. Mutant: swap the two
	// calls back to tombstone-then-delete and this arm names it.
	[Fact]
	public async Task DeleteTheSnapshotBeforeTombstoningTheEvents()
	{
		var context = CreateContext();
		ResolveTo(context, "order-1", "Order");
		A.CallTo(() => _eventStoreErasure.IsErasedAsync(A<string>._, A<string>._, A<CancellationToken>._))
			.Returns(false);
		A.CallTo(() => _eventStoreErasure.EraseEventsAsync("order-1", "Order", A<Guid>._, A<CancellationToken>._))
			.Returns(4);

		_ = await CreateSut().EraseAsync(context, CancellationToken.None);

		// Ordered assertion: the snapshot delete must be observed strictly before the tombstone.
		A.CallTo(() => _snapshotStore.DeleteSnapshotsAsync("order-1", "Order", A<CancellationToken>._))
			.MustHaveHappenedOnceExactly()
			.Then(A.CallTo(() => _eventStoreErasure.EraseEventsAsync(
					"order-1", "Order", A<Guid>._, A<CancellationToken>._))
				.MustHaveHappenedOnceExactly());
	}

	// SAFETY, and this is the arm that catches the defect that made an interrupted erasure PERMANENT.
	// The first attempt tombstones the events and then fails to delete the snapshot. On the retry the
	// events ARE erased, so a guard that skips the rest of the loop on that fact never re-attempts the
	// snapshot and the pre-erasure state stays readable forever -- while the retry logs success.
	// Mutant: restore `continue` under the already-erased check and this arm names it.
	[Fact]
	public async Task ReattemptTheSnapshotOnARetryEvenWhenTheEventsAreAlreadyTombstoned()
	{
		var context = CreateContext();
		ResolveTo(context, "order-1", "Order");

		// First pass: not yet erased. Second pass: the events are tombstoned, because the first pass
		// tombstoned them. This is the real post-fault state, not a contrived one.
		A.CallTo(() => _eventStoreErasure.IsErasedAsync(A<string>._, A<string>._, A<CancellationToken>._))
			.ReturnsNextFromSequence(false, true);
		A.CallTo(() => _eventStoreErasure.EraseEventsAsync("order-1", "Order", A<Guid>._, A<CancellationToken>._))
			.Returns(4);

		// The snapshot delete fails on the first attempt and succeeds on the retry.
		A.CallTo(() => _snapshotStore.DeleteSnapshotsAsync("order-1", "Order", A<CancellationToken>._))
			.Throws(new TimeoutException("transient snapshot-store fault")).Once();

		var sut = CreateSut();

		var first = await sut.EraseAsync(context, CancellationToken.None);
		first.Success.ShouldBeFalse(
			"a fault against the snapshot store must be reported, never absorbed -- the erasure did not "
			+ "finish and the caller is the only party who can retry it");

		var second = await sut.EraseAsync(context, CancellationToken.None);

		A.CallTo(() => _snapshotStore.DeleteSnapshotsAsync("order-1", "Order", A<CancellationToken>._))
			.MustHaveHappenedTwiceExactly();
		second.Success.ShouldBeTrue(
			"the retry completed the half that failed, so it must report success -- otherwise no retry "
			+ "can ever close an erasure and the subject's data stays readable");
	}

	// LIVENESS, and it is what stops both arms above being satisfied by a contributor that refuses
	// everything. A clean erasure must still tombstone, still delete, and still report what it erased.
	[Fact]
	public async Task StillTombstoneAndReportTheCountOnACleanErasure()
	{
		var context = CreateContext();
		ResolveTo(context, "order-1", "Order");
		A.CallTo(() => _eventStoreErasure.IsErasedAsync(A<string>._, A<string>._, A<CancellationToken>._))
			.Returns(false);
		A.CallTo(() => _eventStoreErasure.EraseEventsAsync("order-1", "Order", A<Guid>._, A<CancellationToken>._))
			.Returns(7);

		var result = await CreateSut().EraseAsync(context, CancellationToken.None);

		result.Success.ShouldBeTrue();
		result.RecordsAffected.ShouldBe(7);
		A.CallTo(() => _eventStoreErasure.EraseEventsAsync("order-1", "Order", A<Guid>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() => _snapshotStore.DeleteSnapshotsAsync("order-1", "Order", A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}
}
