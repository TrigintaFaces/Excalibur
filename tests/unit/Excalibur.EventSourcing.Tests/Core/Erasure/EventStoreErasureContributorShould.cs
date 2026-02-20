// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Compliance;
using Excalibur.EventSourcing.Abstractions;
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
			new EventStoreErasureContributor(null!, _mapping, _logger));
	}

	[Fact]
	public void ThrowWhenMappingIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new EventStoreErasureContributor(_erasure, null!, _logger));
	}

	[Fact]
	public void ThrowWhenLoggerIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new EventStoreErasureContributor(_erasure, _mapping, null!));
	}

	[Fact]
	public void ExposeNameAsEventStore()
	{
		// Arrange
		var sut = new EventStoreErasureContributor(_erasure, _mapping, _logger);

		// Act & Assert
		sut.Name.ShouldBe("EventStore");
	}

	[Fact]
	public void CreateSuccessfullyWithoutSnapshotStore()
	{
		// Arrange & Act
		var sut = new EventStoreErasureContributor(_erasure, _mapping, _logger);

		// Assert
		sut.ShouldNotBeNull();
	}

	[Fact]
	public void CreateSuccessfullyWithSnapshotStore()
	{
		// Arrange & Act
		var sut = new EventStoreErasureContributor(_erasure, _mapping, _logger, _snapshotStore);

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
		var sut = new EventStoreErasureContributor(_erasure, _mapping, _logger);

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
		var sut = new EventStoreErasureContributor(_erasure, _mapping, _logger);

		// Act
		var result = await sut.EraseAsync(context, CancellationToken.None);

		// Assert
		result.Success.ShouldBeTrue();
		result.RecordsAffected.ShouldBe(5);
	}

	[Fact]
	public async Task SkipAlreadyErasedAggregates()
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
		var sut = new EventStoreErasureContributor(_erasure, _mapping, _logger);

		// Act
		var result = await sut.EraseAsync(context, CancellationToken.None);

		// Assert
		result.Success.ShouldBeTrue();
		A.CallTo(() => _erasure.EraseEventsAsync(A<string>._, A<string>._, A<Guid>._, CancellationToken.None))
			.MustNotHaveHappened();
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
		var sut = new EventStoreErasureContributor(_erasure, _mapping, _logger, _snapshotStore);

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
		var sut = new EventStoreErasureContributor(_erasure, _mapping, _logger);

		// Act
		var result = await sut.EraseAsync(context, CancellationToken.None);

		// Assert
		result.Success.ShouldBeFalse();
	}

	[Fact]
	public async Task ThrowWhenContextIsNull()
	{
		// Arrange
		var sut = new EventStoreErasureContributor(_erasure, _mapping, _logger);

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(
			() => sut.EraseAsync(null!, CancellationToken.None));
	}

	[Fact]
	public void ImplementIErasureContributor()
	{
		// Arrange & Act
		var sut = new EventStoreErasureContributor(_erasure, _mapping, _logger);

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
