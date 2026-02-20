// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Compliance;
using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.Erasure;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.EventSourcing.Tests.Core.Erasure;

/// <summary>
/// Depth coverage tests for <see cref="EventStoreErasureContributor"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class EventStoreErasureContributorDepthShould
{
	private readonly IEventStoreErasure _erasure = A.Fake<IEventStoreErasure>();
	private readonly IAggregateDataSubjectMapping _mapping = A.Fake<IAggregateDataSubjectMapping>();
	private readonly ISnapshotStore _snapshotStore = A.Fake<ISnapshotStore>();
	private readonly ILogger<EventStoreErasureContributor> _logger = NullLogger<EventStoreErasureContributor>.Instance;

	[Fact]
	public void Constructor_ThrowsArgumentNullException_WhenErasureIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new EventStoreErasureContributor(null!, _mapping, _logger));
	}

	[Fact]
	public void Constructor_ThrowsArgumentNullException_WhenMappingIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new EventStoreErasureContributor(_erasure, null!, _logger));
	}

	[Fact]
	public void Constructor_ThrowsArgumentNullException_WhenLoggerIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new EventStoreErasureContributor(_erasure, _mapping, null!));
	}

	[Fact]
	public void Name_ReturnsEventStore()
	{
		var contributor = CreateContributor();
		contributor.Name.ShouldBe("EventStore");
	}

	[Fact]
	public async Task EraseAsync_ThrowsArgumentNullException_WhenContextIsNull()
	{
		var contributor = CreateContributor();
		await Should.ThrowAsync<ArgumentNullException>(() =>
			contributor.EraseAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task EraseAsync_ReturnsSucceeded_WithZero_WhenNoAggregatesFound()
	{
		// Arrange
		A.CallTo(() => _mapping.GetAggregatesForDataSubjectAsync(A<string>._, A<string?>._, A<CancellationToken>._))
			.Returns(new List<AggregateReference>());

		var contributor = CreateContributor();
		var context = CreateContext();

		// Act
		var result = await contributor.EraseAsync(context, CancellationToken.None);

		// Assert
		result.Success.ShouldBeTrue();
		result.RecordsAffected.ShouldBe(0);
	}

	[Fact]
	public async Task EraseAsync_ErasesEventsAndSnapshots()
	{
		// Arrange
		var references = new List<AggregateReference>
		{
			new("agg-1", "OrderAggregate"),
		};

		A.CallTo(() => _mapping.GetAggregatesForDataSubjectAsync(A<string>._, A<string?>._, A<CancellationToken>._))
			.Returns(references);
		A.CallTo(() => _erasure.IsErasedAsync("agg-1", "OrderAggregate", A<CancellationToken>._))
			.Returns(false);
		A.CallTo(() => _erasure.EraseEventsAsync("agg-1", "OrderAggregate", A<Guid>._, A<CancellationToken>._))
			.Returns(5);

		var contributor = CreateContributor(withSnapshots: true);
		var context = CreateContext();

		// Act
		var result = await contributor.EraseAsync(context, CancellationToken.None);

		// Assert
		result.Success.ShouldBeTrue();
		result.RecordsAffected.ShouldBe(5);
		A.CallTo(() => _snapshotStore.DeleteSnapshotsAsync("agg-1", "OrderAggregate", A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task EraseAsync_SkipsAlreadyErasedAggregates()
	{
		// Arrange
		var references = new List<AggregateReference>
		{
			new("agg-1", "OrderAggregate"),
		};

		A.CallTo(() => _mapping.GetAggregatesForDataSubjectAsync(A<string>._, A<string?>._, A<CancellationToken>._))
			.Returns(references);
		A.CallTo(() => _erasure.IsErasedAsync("agg-1", "OrderAggregate", A<CancellationToken>._))
			.Returns(true);

		var contributor = CreateContributor();
		var context = CreateContext();

		// Act
		var result = await contributor.EraseAsync(context, CancellationToken.None);

		// Assert
		result.Success.ShouldBeTrue();
		A.CallTo(() => _erasure.EraseEventsAsync(A<string>._, A<string>._, A<Guid>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task EraseAsync_ReturnsFailure_WhenExceptionOccurs()
	{
		// Arrange
		var references = new List<AggregateReference>
		{
			new("agg-1", "OrderAggregate"),
		};

		A.CallTo(() => _mapping.GetAggregatesForDataSubjectAsync(A<string>._, A<string?>._, A<CancellationToken>._))
			.Returns(references);
		A.CallTo(() => _erasure.IsErasedAsync(A<string>._, A<string>._, A<CancellationToken>._))
			.Returns(false);
		A.CallTo(() => _erasure.EraseEventsAsync(A<string>._, A<string>._, A<Guid>._, A<CancellationToken>._))
			.Throws(new InvalidOperationException("db error"));

		var contributor = CreateContributor();
		var context = CreateContext();

		// Act
		var result = await contributor.EraseAsync(context, CancellationToken.None);

		// Assert
		result.Success.ShouldBeFalse();
		result.ErrorMessage.ShouldContain("Partial erasure");
	}

	[Fact]
	public async Task EraseAsync_WithoutSnapshotStore_SkipsSnapshotDeletion()
	{
		// Arrange
		var references = new List<AggregateReference>
		{
			new("agg-1", "OrderAggregate"),
		};

		A.CallTo(() => _mapping.GetAggregatesForDataSubjectAsync(A<string>._, A<string?>._, A<CancellationToken>._))
			.Returns(references);
		A.CallTo(() => _erasure.IsErasedAsync(A<string>._, A<string>._, A<CancellationToken>._))
			.Returns(false);
		A.CallTo(() => _erasure.EraseEventsAsync(A<string>._, A<string>._, A<Guid>._, A<CancellationToken>._))
			.Returns(3);

		var contributor = CreateContributor(withSnapshots: false);
		var context = CreateContext();

		// Act
		var result = await contributor.EraseAsync(context, CancellationToken.None);

		// Assert
		result.Success.ShouldBeTrue();
		result.RecordsAffected.ShouldBe(3);
	}

	private EventStoreErasureContributor CreateContributor(bool withSnapshots = false)
	{
		return new EventStoreErasureContributor(
			_erasure, _mapping, _logger,
			withSnapshots ? _snapshotStore : null);
	}

	private static ErasureContributorContext CreateContext()
	{
		return new ErasureContributorContext
		{
			RequestId = Guid.NewGuid(),
			DataSubjectIdHash = "hashed-subject-1",
			IdType = DataSubjectIdType.UserId,
			Scope = ErasureScope.User,
			TenantId = null,
		};
	}
}
