// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Domain.Model;
using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.Snapshots.InMemory;

namespace Excalibur.EventSourcing.Tests.Core.Snapshots.InMemory;

[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
public sealed class InMemorySnapshotStoreShould
{
	private readonly InMemorySnapshotStore _sut = new();

	[Fact]
	public async Task SaveAndRetrieveSnapshot()
	{
		// Arrange
		var snapshot = CreateSnapshot("agg-1", "OrderAggregate", 5);

		// Act
		await _sut.SaveSnapshotAsync(snapshot, CancellationToken.None);
		var result = await _sut.GetLatestSnapshotAsync("agg-1", "OrderAggregate", CancellationToken.None);

		// Assert
		result.ShouldNotBeNull();
		result.AggregateId.ShouldBe("agg-1");
		result.Version.ShouldBe(5);
	}

	[Fact]
	public async Task ReturnNull_WhenSnapshotNotFound()
	{
		// Act
		var result = await _sut.GetLatestSnapshotAsync("nonexistent", "Type", CancellationToken.None);

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public async Task KeepHigherVersion_OnConcurrentSaves()
	{
		// Arrange
		var older = CreateSnapshot("agg-1", "Type", 3);
		var newer = CreateSnapshot("agg-1", "Type", 5);

		// Act
		await _sut.SaveSnapshotAsync(newer, CancellationToken.None);
		await _sut.SaveSnapshotAsync(older, CancellationToken.None);

		var result = await _sut.GetLatestSnapshotAsync("agg-1", "Type", CancellationToken.None);

		// Assert
		result.ShouldNotBeNull();
		result.Version.ShouldBe(5);
	}

	[Fact]
	public async Task ReplaceWithNewerVersion()
	{
		// Arrange
		var v1 = CreateSnapshot("agg-1", "Type", 1);
		var v2 = CreateSnapshot("agg-1", "Type", 2);

		// Act
		await _sut.SaveSnapshotAsync(v1, CancellationToken.None);
		await _sut.SaveSnapshotAsync(v2, CancellationToken.None);

		var result = await _sut.GetLatestSnapshotAsync("agg-1", "Type", CancellationToken.None);

		// Assert
		result.ShouldNotBeNull();
		result.Version.ShouldBe(2);
	}

	[Fact]
	public async Task DeleteSnapshots_ByAggregateId()
	{
		// Arrange
		await _sut.SaveSnapshotAsync(CreateSnapshot("agg-1", "Type", 1), CancellationToken.None);
		_sut.Count.ShouldBe(1);

		// Act
		await _sut.DeleteSnapshotsAsync("agg-1", "Type", CancellationToken.None);

		// Assert
		var result = await _sut.GetLatestSnapshotAsync("agg-1", "Type", CancellationToken.None);
		result.ShouldBeNull();
		_sut.Count.ShouldBe(0);
	}

	[Fact]
	public async Task DeleteSnapshotsOlderThan_RemoveOldVersions()
	{
		// Arrange
		await _sut.SaveSnapshotAsync(CreateSnapshot("agg-1", "Type", 3), CancellationToken.None);

		// Act
		await _sut.DeleteSnapshotsOlderThanAsync("agg-1", "Type", 5, CancellationToken.None);

		// Assert
		var result = await _sut.GetLatestSnapshotAsync("agg-1", "Type", CancellationToken.None);
		result.ShouldBeNull();
	}

	[Fact]
	public async Task DeleteSnapshotsOlderThan_KeepNewerVersions()
	{
		// Arrange
		await _sut.SaveSnapshotAsync(CreateSnapshot("agg-1", "Type", 10), CancellationToken.None);

		// Act
		await _sut.DeleteSnapshotsOlderThanAsync("agg-1", "Type", 5, CancellationToken.None);

		// Assert
		var result = await _sut.GetLatestSnapshotAsync("agg-1", "Type", CancellationToken.None);
		result.ShouldNotBeNull();
		result.Version.ShouldBe(10);
	}

	[Fact]
	public async Task ClearAllSnapshots()
	{
		// Arrange
		await _sut.SaveSnapshotAsync(CreateSnapshot("a1", "T1", 1), CancellationToken.None);
		await _sut.SaveSnapshotAsync(CreateSnapshot("a2", "T2", 1), CancellationToken.None);
		_sut.Count.ShouldBe(2);

		// Act
		_sut.Clear();

		// Assert
		_sut.Count.ShouldBe(0);
	}

	[Fact]
	public async Task ThrowOnNullOrEmptyArgs()
	{
		await Should.ThrowAsync<ArgumentException>(
			() => _sut.GetLatestSnapshotAsync(null!, "Type", CancellationToken.None).AsTask());
		await Should.ThrowAsync<ArgumentException>(
			() => _sut.GetLatestSnapshotAsync("", "Type", CancellationToken.None).AsTask());
		await Should.ThrowAsync<ArgumentException>(
			() => _sut.GetLatestSnapshotAsync("id", null!, CancellationToken.None).AsTask());
		await Should.ThrowAsync<ArgumentNullException>(
			() => _sut.SaveSnapshotAsync(null!, CancellationToken.None).AsTask());
		await Should.ThrowAsync<ArgumentException>(
			() => _sut.DeleteSnapshotsAsync(null!, "Type", CancellationToken.None).AsTask());
		await Should.ThrowAsync<ArgumentException>(
			() => _sut.DeleteSnapshotsOlderThanAsync(null!, "Type", 1, CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task IsolateDifferentAggregateTypes()
	{
		// Arrange
		await _sut.SaveSnapshotAsync(CreateSnapshot("agg-1", "TypeA", 1), CancellationToken.None);
		await _sut.SaveSnapshotAsync(CreateSnapshot("agg-1", "TypeB", 2), CancellationToken.None);

		// Act
		var resultA = await _sut.GetLatestSnapshotAsync("agg-1", "TypeA", CancellationToken.None);
		var resultB = await _sut.GetLatestSnapshotAsync("agg-1", "TypeB", CancellationToken.None);

		// Assert
		resultA.ShouldNotBeNull();
		resultA.Version.ShouldBe(1);
		resultB.ShouldNotBeNull();
		resultB.Version.ShouldBe(2);
		_sut.Count.ShouldBe(2);
	}

	private static ISnapshot CreateSnapshot(string aggregateId, string aggregateType, long version)
	{
		var snapshot = A.Fake<ISnapshot>();
		A.CallTo(() => snapshot.SnapshotId).Returns($"snap-{aggregateId}-{version}");
		A.CallTo(() => snapshot.AggregateId).Returns(aggregateId);
		A.CallTo(() => snapshot.AggregateType).Returns(aggregateType);
		A.CallTo(() => snapshot.Version).Returns(version);
		A.CallTo(() => snapshot.CreatedAt).Returns(DateTimeOffset.UtcNow);
		A.CallTo(() => snapshot.Data).Returns([1, 2, 3]);
		A.CallTo(() => snapshot.Metadata).Returns(null);
		return snapshot;
	}
}
