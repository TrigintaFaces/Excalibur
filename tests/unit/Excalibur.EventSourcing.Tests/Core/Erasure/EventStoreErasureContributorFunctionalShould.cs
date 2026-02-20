// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Compliance;
using Excalibur.EventSourcing.Abstractions;
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
public sealed class EventStoreErasureContributorFunctionalShould
{
	private readonly IEventStoreErasure _eventStoreErasure = A.Fake<IEventStoreErasure>();
	private readonly ISnapshotStore _snapshotStore = A.Fake<ISnapshotStore>();
	private readonly IAggregateDataSubjectMapping _mapping = A.Fake<IAggregateDataSubjectMapping>();

	private EventStoreErasureContributor CreateSut(ISnapshotStore? snapshotStore = null) =>
		new(_eventStoreErasure, _mapping,
			NullLogger<EventStoreErasureContributor>.Instance,
			snapshotStore);

	private static ErasureContributorContext CreateContext(string dataSubjectHash = "hash-123", string? tenantId = null) =>
		new()
		{
			RequestId = Guid.NewGuid(),
			DataSubjectIdHash = dataSubjectHash,
			IdType = DataSubjectIdType.UserId,
			Scope = ErasureScope.User,
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

	[Fact]
	public async Task SkipAlreadyErasedAggregates()
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

		var sut = CreateSut();

		// Act
		var result = await sut.EraseAsync(context, CancellationToken.None);

		// Assert
		result.Success.ShouldBeTrue();
		result.RecordsAffected.ShouldBe(3); // Only order-2

		// order-1 should not have EraseEventsAsync called
		A.CallTo(() => _eventStoreErasure.EraseEventsAsync("order-1", "Order", A<Guid>._, A<CancellationToken>._))
			.MustNotHaveHappened();
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
		result.ErrorMessage.ShouldContain("Partial erasure");
		result.ErrorMessage.ShouldContain("Connection lost");
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
			new EventStoreErasureContributor(null!, _mapping, logger));
		Should.Throw<ArgumentNullException>(() =>
			new EventStoreErasureContributor(_eventStoreErasure, null!, logger));
		Should.Throw<ArgumentNullException>(() =>
			new EventStoreErasureContributor(_eventStoreErasure, _mapping, null!));
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
