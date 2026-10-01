// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Compliance;
using Excalibur.EventSourcing;
using Excalibur.EventSourcing.Erasure;

using FakeItEasy;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using Shouldly;

using Xunit;

namespace Excalibur.EventSourcing.Tests.Core.Erasure;

/// <summary>
/// Functional tests for <see cref="EventStoreErasureContributor"/> covering
/// GDPR erasure workflows: aggregate resolution, event tombstoning, snapshot deletion,
/// partial failures, and already-erased aggregates.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
public sealed class EventStoreErasureContributorFunctionalShould
{
	private readonly IEventStoreErasure _eventStoreErasure = A.Fake<IEventStoreErasure>();
	private readonly ISnapshotStore _snapshotStore = A.Fake<ISnapshotStore>();
	private readonly IAggregateDataSubjectMapping _mapping = A.Fake<IAggregateDataSubjectMapping>();

	private EventStoreErasureContributor CreateSut(ISnapshotStore? snapshotStore = null) =>
		new(_eventStoreErasure, _mapping,
			NullLogger<EventStoreErasureContributor>.Instance,
			snapshotStore,
			serviceProvider: null,
			retentions: null);

	private static ErasureContributorContext CreateContext(
		string dataSubjectHash = "hash-123",
		string? tenantId = null,
		ErasureScope scope = ErasureScope.User) =>
		new()
		{
			RequestId = Guid.NewGuid(),
			DataSubjectIdHash = dataSubjectHash,
			IdType = DataSubjectIdType.UserId,
			Scope = scope,
			TenantId = tenantId,
		};

	[Fact]
	public async Task EraseEventsAndSnapshots_ForResolvedAggregates()
	{
		// Arrange
		var context = CreateContext();
		var references = new List<AggregateReference>
		{
			new("order-1", "Order"),
			new("order-2", "Order"),
		};

		A.CallTo(() => _mapping.GetAggregatesForDataSubjectAsync(context.DataSubjectIdHash, context.TenantId, A<CancellationToken>._))
			.Returns(references);
		A.CallTo(() => _eventStoreErasure.IsErasedAsync(A<string>._, A<string>._, A<CancellationToken>._))
			.Returns(false);
		A.CallTo(() => _eventStoreErasure.EraseEventsAsync("order-1", "Order", A<Guid>._, A<CancellationToken>._))
			.Returns(5);
		A.CallTo(() => _eventStoreErasure.EraseEventsAsync("order-2", "Order", A<Guid>._, A<CancellationToken>._))
			.Returns(3);

		var sut = CreateSut(_snapshotStore);

		// Act
		var result = await sut.EraseAsync(context, CancellationToken.None);

		// Assert
		result.Success.ShouldBeTrue();
		result.RecordsAffected.ShouldBe(8); // 5 + 3

		A.CallTo(() => _snapshotStore.DeleteSnapshotsAsync("order-1", "Order", A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() => _snapshotStore.DeleteSnapshotsAsync("order-2", "Order", A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ReturnSuccess_WhenNoAggregatesFound()
	{
		// Arrange
		var context = CreateContext();
		A.CallTo(() => _mapping.GetAggregatesForDataSubjectAsync(context.DataSubjectIdHash, context.TenantId, A<CancellationToken>._))
			.Returns(new List<AggregateReference>());

		var sut = CreateSut();

		// Act
		var result = await sut.EraseAsync(context, CancellationToken.None);

		// Assert
		result.Success.ShouldBeTrue();
		result.RecordsAffected.ShouldBe(0);
	}

	// SAFETY. A Selective request names specific data CATEGORIES. This contributor has exactly one action
	// -- tombstone every event of an aggregate -- so honouring that restriction is impossible, and both ways
	// of ignoring it are wrong: tombstoning destroys categories nobody asked about, and reporting success
	// erases nothing while claiming it did. RED if the scope check is removed: the fakes would tombstone
	// both aggregates and the result would report Success.
	[Fact]
	public async Task RefuseAndTombstoneNothing_WhenTheRequestNamesSpecificCategories()
	{
		var context = CreateContext(scope: ErasureScope.Selective);
		A.CallTo(() => _mapping.GetAggregatesForDataSubjectAsync(A<string>._, A<string>._, A<CancellationToken>._))
			.Returns(new List<AggregateReference> { new("order-1", "Order"), new("order-2", "Order") });
		A.CallTo(() => _eventStoreErasure.IsErasedAsync(A<string>._, A<string>._, A<CancellationToken>._)).Returns(false);
		A.CallTo(() => _eventStoreErasure.EraseEventsAsync(A<string>._, A<string>._, A<Guid>._, A<CancellationToken>._))
			.Returns(5);

		var result = await CreateSut(_snapshotStore).EraseAsync(context, CancellationToken.None);

		result.Success.ShouldBeFalse(
			"a contributor that cannot express the declared restriction must refuse, so the coverage gate "
			+ "leaves the obligation outstanding instead of certifying a completion it did not perform");
		result.RecordsAffected.ShouldBe(0);

		A.CallTo(() => _eventStoreErasure.EraseEventsAsync(A<string>._, A<string>._, A<Guid>._, A<CancellationToken>._))
			.MustNotHaveHappened();
		A.CallTo(() => _snapshotStore.DeleteSnapshotsAsync(A<string>._, A<string>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	// LIVENESS, and it is what stops the arm above being satisfied by refusing EVERYTHING. A whole-subject
	// request is exactly what whole-aggregate tombstoning expresses correctly, so it must still act. RED if
	// the scope check is widened past Selective.
	[Fact]
	public async Task StillTombstone_WhenTheRequestCoversTheWholeDataSubject()
	{
		var context = CreateContext(scope: ErasureScope.User);
		A.CallTo(() => _mapping.GetAggregatesForDataSubjectAsync(A<string>._, A<string>._, A<CancellationToken>._))
			.Returns(new List<AggregateReference> { new("order-1", "Order") });
		A.CallTo(() => _eventStoreErasure.IsErasedAsync(A<string>._, A<string>._, A<CancellationToken>._)).Returns(false);
		A.CallTo(() => _eventStoreErasure.EraseEventsAsync("order-1", "Order", A<Guid>._, A<CancellationToken>._))
			.Returns(4);

		var result = await CreateSut(_snapshotStore).EraseAsync(context, CancellationToken.None);

		result.Success.ShouldBeTrue("a whole-subject erasure is the case this mechanism does express");
		result.RecordsAffected.ShouldBe(4);
		A.CallTo(() => _eventStoreErasure.EraseEventsAsync("order-1", "Order", A<Guid>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	// FLIPPED to the corrected contract, and STRENGTHENED: it now pins the property that matters, which
	// is that the already-tombstoned aggregate still has its SNAPSHOT deleted. A snapshot outliving a
	// tombstoned stream is the readable copy -- the aggregate load applies it and returns the full
	// pre-erasure state, because a current snapshot makes the load fetch zero event rows and the
	// tombstone check never sees one.
	public async Task StillDeleteTheSnapshotOfAnAggregateWhoseEventsAreAlreadyTombstoned()
	{
		// Arrange
		var context = CreateContext();
		var references = new List<AggregateReference>
		{
			new("order-1", "Order"),
			new("order-2", "Order"),
		};

		A.CallTo(() => _mapping.GetAggregatesForDataSubjectAsync(context.DataSubjectIdHash, context.TenantId, A<CancellationToken>._))
			.Returns(references);
		A.CallTo(() => _eventStoreErasure.IsErasedAsync("order-1", "Order", A<CancellationToken>._))
			.Returns(true); // Already erased
		A.CallTo(() => _eventStoreErasure.IsErasedAsync("order-2", "Order", A<CancellationToken>._))
			.Returns(false);
		A.CallTo(() => _eventStoreErasure.EraseEventsAsync("order-2", "Order", A<Guid>._, A<CancellationToken>._))
			.Returns(3);

		var sut = CreateSut(_snapshotStore);

		// Act
		var result = await sut.EraseAsync(context, CancellationToken.None);

		// Assert
		result.Success.ShouldBeTrue();

		// The re-tombstone of order-1 is a no-op that reports zero, so completing an already-erased
		// aggregate does not inflate the count the certificate carries.
		result.RecordsAffected.ShouldBe(3);

		// The snapshot is the copy that keeps the erased state readable, so it must be destroyed even
		// when the events were tombstoned by an earlier, interrupted attempt.
		A.CallTo(() => _snapshotStore.DeleteSnapshotsAsync("order-1", "Order", A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task HandlePartialFailure_ReturnFailedResult()
	{
		// Arrange
		var context = CreateContext();
		var references = new List<AggregateReference>
		{
			new("order-1", "Order"),
			new("order-2", "Order"),
		};

		A.CallTo(() => _mapping.GetAggregatesForDataSubjectAsync(context.DataSubjectIdHash, context.TenantId, A<CancellationToken>._))
			.Returns(references);
		A.CallTo(() => _eventStoreErasure.IsErasedAsync(A<string>._, A<string>._, A<CancellationToken>._))
			.Returns(false);
		A.CallTo(() => _eventStoreErasure.EraseEventsAsync("order-1", "Order", A<Guid>._, A<CancellationToken>._))
			.Returns(5);
		A.CallTo(() => _eventStoreErasure.EraseEventsAsync("order-2", "Order", A<Guid>._, A<CancellationToken>._))
			.Throws(new InvalidOperationException("Connection lost"));

		var sut = CreateSut();

		// Act
		var result = await sut.EraseAsync(context, CancellationToken.None);

		// Assert
		result.Success.ShouldBeFalse();
		result.ErrorMessage!.ShouldContain("Partial erasure");
		result.ErrorMessage!.ShouldContain("Connection lost");
	}

	[Fact]
	public async Task WorkWithoutSnapshotStore()
	{
		// Arrange
		var context = CreateContext();
		var references = new List<AggregateReference>
		{
			new("order-1", "Order"),
		};

		A.CallTo(() => _mapping.GetAggregatesForDataSubjectAsync(context.DataSubjectIdHash, context.TenantId, A<CancellationToken>._))
			.Returns(references);
		A.CallTo(() => _eventStoreErasure.IsErasedAsync(A<string>._, A<string>._, A<CancellationToken>._))
			.Returns(false);
		A.CallTo(() => _eventStoreErasure.EraseEventsAsync("order-1", "Order", A<Guid>._, A<CancellationToken>._))
			.Returns(5);

		var sut = CreateSut(null); // No snapshot store

		// Act
		var result = await sut.EraseAsync(context, CancellationToken.None);

		// Assert
		result.Success.ShouldBeTrue();
		result.RecordsAffected.ShouldBe(5);
	}

	[Fact]
	public void HaveCorrectName()
	{
		// Act
		var sut = CreateSut();

		// Assert
		sut.Name.ShouldBe("EventStore");
	}

	[Fact]
	public async Task ThrowOnNullContext()
	{
		// Arrange
		var sut = CreateSut();

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(
			() => sut.EraseAsync(null!, CancellationToken.None));
	}

	[Fact]
	public void ThrowOnNullConstructorArguments()
	{
		var logger = NullLogger<EventStoreErasureContributor>.Instance;

		Should.Throw<ArgumentNullException>(() =>
			new EventStoreErasureContributor(null!, _mapping, logger, snapshotStore: null, serviceProvider: null, retentions: null));
		Should.Throw<ArgumentNullException>(() =>
			new EventStoreErasureContributor(_eventStoreErasure, null!, logger, snapshotStore: null, serviceProvider: null, retentions: null));
		Should.Throw<ArgumentNullException>(() =>
			new EventStoreErasureContributor(_eventStoreErasure, _mapping, null!, snapshotStore: null, serviceProvider: null, retentions: null));
	}

	[Fact]
	public async Task PassTenantIdToMapping()
	{
		// Arrange
		var context = CreateContext(tenantId: "tenant-abc");
		A.CallTo(() => _mapping.GetAggregatesForDataSubjectAsync(context.DataSubjectIdHash, "tenant-abc", A<CancellationToken>._))
			.Returns(new List<AggregateReference>());

		var sut = CreateSut();

		// Act
		await sut.EraseAsync(context, CancellationToken.None);

		// Assert
		A.CallTo(() => _mapping.GetAggregatesForDataSubjectAsync(context.DataSubjectIdHash, "tenant-abc", A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}
}
