// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Compliance;
using Excalibur.EventSourcing;
using Excalibur.EventSourcing.Erasure;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.EventSourcing.Tests.Core.Erasure;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class EventStoreErasureContributorShould
{
	private readonly IEventStoreErasure _erasure = A.Fake<IEventStoreErasure>();
	private readonly IAggregateDataSubjectMapping _mapping = A.Fake<IAggregateDataSubjectMapping>();
	private readonly ISnapshotStore _snapshotStore = A.Fake<ISnapshotStore>();
	private readonly ILogger<EventStoreErasureContributor> _logger = NullLogger<EventStoreErasureContributor>.Instance;

	[Fact]
	public void ThrowWhenErasureIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new EventStoreErasureContributor(null!, _mapping, _logger, snapshotStore: null, serviceProvider: null, retentions: null));
	}

	[Fact]
	public void ThrowWhenMappingIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new EventStoreErasureContributor(_erasure, null!, _logger, snapshotStore: null, serviceProvider: null, retentions: null));
	}

	[Fact]
	public void ThrowWhenLoggerIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new EventStoreErasureContributor(_erasure, _mapping, null!, snapshotStore: null, serviceProvider: null, retentions: null));
	}

	[Fact]
	public void ExposeNameAsEventStore()
	{
		// Arrange
		var sut = new EventStoreErasureContributor(_erasure, _mapping, _logger, snapshotStore: null, serviceProvider: null, retentions: null);

		// Act & Assert
		sut.Name.ShouldBe("EventStore");
	}

	[Fact]
	public void CreateSuccessfullyWithoutSnapshotStore()
	{
		// Arrange & Act
		var sut = new EventStoreErasureContributor(_erasure, _mapping, _logger, snapshotStore: null, serviceProvider: null, retentions: null);

		// Assert
		sut.ShouldNotBeNull();
	}

	[Fact]
	public void CreateSuccessfullyWithSnapshotStore()
	{
		// Arrange & Act
		var sut = new EventStoreErasureContributor(_erasure, _mapping, _logger, _snapshotStore, serviceProvider: null, retentions: null);

		// Assert
		sut.ShouldNotBeNull();
	}

	[Fact]
	public async Task ReturnSuccessWithZeroCountWhenNoAggregatesFound()
	{
		// Arrange
		var context = CreateContext();
		A.CallTo(() => _mapping.GetAggregatesForDataSubjectAsync(
				A<string>._, A<string?>._, CancellationToken.None))
			.Returns(Task.FromResult<IReadOnlyList<AggregateReference>>([]));
		var sut = new EventStoreErasureContributor(_erasure, _mapping, _logger, snapshotStore: null, serviceProvider: null, retentions: null);

		// Act
		var result = await sut.EraseAsync(context, CancellationToken.None);

		// Assert
		result.Success.ShouldBeTrue();
		result.RecordsAffected.ShouldBe(0);
	}

	[Fact]
	public async Task EraseEventsForMappedAggregates()
	{
		// Arrange
		var context = CreateContext();
		var references = new List<AggregateReference>
		{
			new("agg-1", "Order"),
		};
		A.CallTo(() => _mapping.GetAggregatesForDataSubjectAsync(
				A<string>._, A<string?>._, CancellationToken.None))
			.Returns(Task.FromResult<IReadOnlyList<AggregateReference>>(references));
		A.CallTo(() => _erasure.IsErasedAsync("agg-1", "Order", CancellationToken.None))
			.Returns(Task.FromResult(false));
		A.CallTo(() => _erasure.EraseEventsAsync("agg-1", "Order", A<Guid>._, CancellationToken.None))
			.Returns(Task.FromResult(5));
		var sut = new EventStoreErasureContributor(_erasure, _mapping, _logger, snapshotStore: null, serviceProvider: null, retentions: null);

		// Act
		var result = await sut.EraseAsync(context, CancellationToken.None);

		// Assert
		result.Success.ShouldBeTrue();
		result.RecordsAffected.ShouldBe(5);
	}

	[Fact]
	// FLIPPED to the corrected contract. This arm used to assert the opposite -- that an aggregate whose
	// events are already tombstoned is SKIPPED entirely -- and that skip is what made an interrupted
	// erasure permanent: the snapshot and the read models are destroyed AFTER the tombstone, so an
	// aggregate skipped on "already erased" never had those steps re-attempted, while the retry logged
	// success. The tombstone itself is idempotent (its own statement excludes rows already carrying the
	// marker), so re-entering the loop costs a round trip and is the only thing that can finish an
	// erasure that was interrupted part-way.
	public async Task StillRunTheErasureStepsWhenTheEventsAreAlreadyTombstoned()
	{
		// Arrange
		var context = CreateContext();
		var references = new List<AggregateReference>
		{
			new("agg-1", "Order"),
		};
		A.CallTo(() => _mapping.GetAggregatesForDataSubjectAsync(
				A<string>._, A<string?>._, CancellationToken.None))
			.Returns(Task.FromResult<IReadOnlyList<AggregateReference>>(references));
		A.CallTo(() => _erasure.IsErasedAsync("agg-1", "Order", CancellationToken.None))
			.Returns(Task.FromResult(true));
		var sut = new EventStoreErasureContributor(_erasure, _mapping, _logger, snapshotStore: null, serviceProvider: null, retentions: null);

		// Act
		var result = await sut.EraseAsync(context, CancellationToken.None);

		// Assert
		result.Success.ShouldBeTrue();
		// An already-tombstoned aggregate must still be carried through the loop, because the steps that
		// follow the tombstone are the ones an interrupted erasure left undone.
		A.CallTo(() => _erasure.EraseEventsAsync(A<string>._, A<string>._, A<Guid>._, CancellationToken.None))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task DeleteSnapshotsWhenSnapshotStoreProvided()
	{
		// Arrange
		var context = CreateContext();
		var references = new List<AggregateReference>
		{
			new("agg-1", "Order"),
		};
		A.CallTo(() => _mapping.GetAggregatesForDataSubjectAsync(
				A<string>._, A<string?>._, CancellationToken.None))
			.Returns(Task.FromResult<IReadOnlyList<AggregateReference>>(references));
		A.CallTo(() => _erasure.IsErasedAsync("agg-1", "Order", CancellationToken.None))
			.Returns(Task.FromResult(false));
		A.CallTo(() => _erasure.EraseEventsAsync("agg-1", "Order", A<Guid>._, CancellationToken.None))
			.Returns(Task.FromResult(3));
		var sut = new EventStoreErasureContributor(_erasure, _mapping, _logger, _snapshotStore, serviceProvider: null, retentions: null);

		// Act
		await sut.EraseAsync(context, CancellationToken.None);

		// Assert
		A.CallTo(() => _snapshotStore.DeleteSnapshotsAsync("agg-1", "Order", CancellationToken.None))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ReturnFailureOnPartialErasureError()
	{
		// Arrange
		var context = CreateContext();
		var references = new List<AggregateReference>
		{
			new("agg-1", "Order"),
		};
		A.CallTo(() => _mapping.GetAggregatesForDataSubjectAsync(
				A<string>._, A<string?>._, CancellationToken.None))
			.Returns(Task.FromResult<IReadOnlyList<AggregateReference>>(references));
		A.CallTo(() => _erasure.IsErasedAsync("agg-1", "Order", CancellationToken.None))
			.Returns(Task.FromResult(false));
		A.CallTo(() => _erasure.EraseEventsAsync("agg-1", "Order", A<Guid>._, CancellationToken.None))
			.Throws(new InvalidOperationException("DB error"));
		var sut = new EventStoreErasureContributor(_erasure, _mapping, _logger, snapshotStore: null, serviceProvider: null, retentions: null);

		// Act
		var result = await sut.EraseAsync(context, CancellationToken.None);

		// Assert
		result.Success.ShouldBeFalse();
	}

	[Fact]
	public async Task ThrowWhenContextIsNull()
	{
		// Arrange
		var sut = new EventStoreErasureContributor(_erasure, _mapping, _logger, snapshotStore: null, serviceProvider: null, retentions: null);

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(
			() => sut.EraseAsync(null!, CancellationToken.None));
	}

	[Fact]
	public void ImplementIErasureContributor()
	{
		// Arrange & Act
		var sut = new EventStoreErasureContributor(_erasure, _mapping, _logger, snapshotStore: null, serviceProvider: null, retentions: null);

		// Assert
		sut.ShouldBeAssignableTo<IErasureContributor>();
	}

	private static ErasureContributorContext CreateContext()
	{
		return new ErasureContributorContext
		{
			RequestId = Guid.NewGuid(),
			DataSubjectIdHash = "hash-abc-123",
			IdType = DataSubjectIdType.UserId,
			Scope = ErasureScope.User,
			TenantId = null,
		};
	}
}
