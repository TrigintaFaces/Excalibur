// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.InMemory.Snapshots;
using Excalibur.Domain.Model;

using FakeItEasy;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

using Xunit;

namespace Excalibur.EventSourcing.Tests.Core.Snapshots;

/// <summary>
/// Functional tests for <see cref="InMemorySnapshotStore"/> (from Excalibur.Data.InMemory)
/// covering CRUD operations, concurrency, and version semantics.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
public sealed class InMemorySnapshotStoreFunctionalShould : IDisposable
{
	private readonly InMemorySnapshotStore _sut = new(
		Options.Create(new InMemorySnapshotOptions()),
		NullLogger<InMemorySnapshotStore>.Instance);

	public void Dispose() => _sut.Dispose();

	private static ISnapshot CreateSnapshot(string aggregateId, string aggregateType, long version, byte[]? data = null)
	{
		var snapshot = A.Fake<ISnapshot>();
		A.CallTo(() => snapshot.AggregateId).Returns(aggregateId);
		A.CallTo(() => snapshot.AggregateType).Returns(aggregateType);
		A.CallTo(() => snapshot.Version).Returns(version);
		A.CallTo(() => snapshot.SnapshotId).Returns(Guid.NewGuid().ToString());
		A.CallTo(() => snapshot.Data).Returns(data ?? [1, 2, 3]);
		A.CallTo(() => snapshot.CreatedAt).Returns(DateTimeOffset.UtcNow);
		return snapshot;
	}

	[Fact]
	public async Task SaveAndRetrieve_ShouldRoundTrip()
	{
		// Arrange
		var snapshot = CreateSnapshot("agg-1", "Order", 5);

		// Act
		await _sut.SaveSnapshotAsync(snapshot, CancellationToken.None);
		var result = await _sut.GetLatestSnapshotAsync("agg-1", "Order", CancellationToken.None);

		// Assert
		result.ShouldNotBeNull();
		result.AggregateId.ShouldBe("agg-1");
		result.Version.ShouldBe(5);
	}

	[Fact]
	public async Task GetLatestSnapshot_NonExistent_ShouldReturnNull()
	{
		// Act
		var result = await _sut.GetLatestSnapshotAsync("non-existent", "Order", CancellationToken.None);

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public async Task SaveSnapshot_NewerVersion_ShouldOverwrite()
	{
		// Arrange
		var v1 = CreateSnapshot("agg-1", "Order", 1);
		var v5 = CreateSnapshot("agg-1", "Order", 5);

		// Act
		await _sut.SaveSnapshotAsync(v1, CancellationToken.None);
		await _sut.SaveSnapshotAsync(v5, CancellationToken.None);

		var result = await _sut.GetLatestSnapshotAsync("agg-1", "Order", CancellationToken.None);

		// Assert
		result.ShouldNotBeNull();
		result.Version.ShouldBe(5);
	}

	[Fact]
	public async Task SaveSnapshot_OlderVersion_ShouldNotOverwrite()
	{
		// Arrange
		var v5 = CreateSnapshot("agg-1", "Order", 5);
		var v1 = CreateSnapshot("agg-1", "Order", 1);

		// Act
		await _sut.SaveSnapshotAsync(v5, CancellationToken.None);
		await _sut.SaveSnapshotAsync(v1, CancellationToken.None);

		var result = await _sut.GetLatestSnapshotAsync("agg-1", "Order", CancellationToken.None);

		// Assert
		result.ShouldNotBeNull();
		result.Version.ShouldBe(5); // v5 still there
	}

	[Fact]
	public async Task DeleteSnapshots_ShouldRemoveSnapshot()
	{
		// Arrange
		var snapshot = CreateSnapshot("agg-1", "Order", 5);
		await _sut.SaveSnapshotAsync(snapshot, CancellationToken.None);

		// Act
		await _sut.DeleteSnapshotsAsync("agg-1", "Order", CancellationToken.None);
		var result = await _sut.GetLatestSnapshotAsync("agg-1", "Order", CancellationToken.None);

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public async Task DeleteSnapshotsOlderThan_ShouldRemoveOldSnapshots()
	{
		// Arrange
		var snapshot = CreateSnapshot("agg-1", "Order", 3);
		await _sut.SaveSnapshotAsync(snapshot, CancellationToken.None);

		// Act - delete older than version 5
		await _sut.DeleteSnapshotsOlderThanAsync("agg-1", "Order", 5, CancellationToken.None);
		var result = await _sut.GetLatestSnapshotAsync("agg-1", "Order", CancellationToken.None);

		// Assert
		result.ShouldBeNull(); // Version 3 < 5, so deleted
	}

	[Fact]
	public async Task DeleteSnapshotsOlderThan_ShouldKeepNewerSnapshots()
	{
		// Arrange
		var snapshot = CreateSnapshot("agg-1", "Order", 10);
		await _sut.SaveSnapshotAsync(snapshot, CancellationToken.None);

		// Act - delete older than version 5
		await _sut.DeleteSnapshotsOlderThanAsync("agg-1", "Order", 5, CancellationToken.None);
		var result = await _sut.GetLatestSnapshotAsync("agg-1", "Order", CancellationToken.None);

		// Assert
		result.ShouldNotBeNull(); // Version 10 >= 5, so kept
		result.Version.ShouldBe(10);
	}

	[Fact]
	public async Task DifferentAggregateTypes_ShouldBeIndependent()
	{
		// Arrange
		var orderSnapshot = CreateSnapshot("agg-1", "Order", 5);
		var customerSnapshot = CreateSnapshot("agg-1", "Customer", 3);

		// Act
		await _sut.SaveSnapshotAsync(orderSnapshot, CancellationToken.None);
		await _sut.SaveSnapshotAsync(customerSnapshot, CancellationToken.None);

		// Assert
		var orderResult = await _sut.GetLatestSnapshotAsync("agg-1", "Order", CancellationToken.None);
		var customerResult = await _sut.GetLatestSnapshotAsync("agg-1", "Customer", CancellationToken.None);

		orderResult.ShouldNotBeNull();
		orderResult.Version.ShouldBe(5);
		customerResult.ShouldNotBeNull();
		customerResult.Version.ShouldBe(3);
	}

	[Fact]
	public async Task ConcurrentSaves_ShouldBeThreadSafe()
	{
		// Arrange & Act - save many snapshots concurrently
		var tasks = Enumerable.Range(0, 50).Select(i =>
		{
			var snapshot = CreateSnapshot($"agg-{i}", "Order", i);
			return _sut.SaveSnapshotAsync(snapshot, CancellationToken.None).AsTask();
		});

		await Task.WhenAll(tasks);

		// Assert - verify a sample of snapshots are retrievable
		for (var i = 0; i < 50; i += 10)
		{
			var result = await _sut.GetLatestSnapshotAsync($"agg-{i}", "Order", CancellationToken.None);
			result.ShouldNotBeNull();
			result.Version.ShouldBe(i);
		}
	}
}
