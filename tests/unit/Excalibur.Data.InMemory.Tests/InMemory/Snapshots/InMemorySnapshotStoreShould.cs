// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.InMemory.Snapshots;
using Excalibur.Domain.Model;
using Excalibur.EventSourcing.Abstractions;

using FakeItEasy;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.Tests.InMemory.Snapshots;

/// <summary>
/// Unit tests for <see cref="InMemorySnapshotStore"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Data.InMemory")]
public sealed class InMemorySnapshotStoreShould : UnitTestBase
{
	private readonly ILogger<InMemorySnapshotStore> _logger;
	private readonly InMemorySnapshotStore _store;

	public InMemorySnapshotStoreShould()
	{
		_logger = A.Fake<ILogger<InMemorySnapshotStore>>();
		var options = Options.Create(new InMemorySnapshotOptions { MaxSnapshots = 100 });
		_store = new InMemorySnapshotStore(options, _logger);
	}

	#region Constructor Tests

	[Fact]
	public void ThrowArgumentNullException_WhenOptionsIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new InMemorySnapshotStore(null!, _logger));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenLoggerIsNull()
	{
		// Arrange
		var options = Options.Create(new InMemorySnapshotOptions());

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new InMemorySnapshotStore(options, null!));
	}

	#endregion Constructor Tests

	#region SaveSnapshotAsync Tests

	[Fact]
	public async Task SaveSnapshotAsync_SavesSnapshot()
	{
		// Arrange
		var snapshot = CreateSnapshot("agg1", "TestAggregate", 1);

		// Act
		await _store.SaveSnapshotAsync(snapshot, CancellationToken.None);

		// Assert
		var retrieved = await _store.GetLatestSnapshotAsync("agg1", "TestAggregate", CancellationToken.None);
		retrieved.ShouldNotBeNull();
		retrieved.AggregateId.ShouldBe("agg1");
		retrieved.Version.ShouldBe(1);
	}

	[Fact]
	public async Task SaveSnapshotAsync_ThrowsArgumentNullException_WhenSnapshotIsNull()
	{
		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(() =>
			_store.SaveSnapshotAsync(null!, CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task SaveSnapshotAsync_ReplacesExistingSnapshot_WhenNewerVersion()
	{
		// Arrange
		var snapshot1 = CreateSnapshot("agg1", "TestAggregate", 1);
		var snapshot2 = CreateSnapshot("agg1", "TestAggregate", 2);

		// Act
		await _store.SaveSnapshotAsync(snapshot1, CancellationToken.None);
		await _store.SaveSnapshotAsync(snapshot2, CancellationToken.None);

		// Assert
		var retrieved = await _store.GetLatestSnapshotAsync("agg1", "TestAggregate", CancellationToken.None);
		retrieved.ShouldNotBeNull();
		retrieved.Version.ShouldBe(2);
	}

	[Fact]
	public async Task SaveSnapshotAsync_KeepsExistingSnapshot_WhenOlderVersion()
	{
		// Arrange
		var snapshot1 = CreateSnapshot("agg1", "TestAggregate", 5);
		var snapshot2 = CreateSnapshot("agg1", "TestAggregate", 3);

		// Act
		await _store.SaveSnapshotAsync(snapshot1, CancellationToken.None);
		await _store.SaveSnapshotAsync(snapshot2, CancellationToken.None);

		// Assert
		var retrieved = await _store.GetLatestSnapshotAsync("agg1", "TestAggregate", CancellationToken.None);
		retrieved.ShouldNotBeNull();
		retrieved.Version.ShouldBe(5);
	}

	[Fact]
	public async Task SaveSnapshotAsync_ThrowsObjectDisposedException_WhenDisposed()
	{
		// Arrange
		var options = Options.Create(new InMemorySnapshotOptions());
		var store = new InMemorySnapshotStore(options, _logger);
		store.Dispose();
		var snapshot = CreateSnapshot("agg1", "TestAggregate", 1);

		// Act & Assert
		_ = await Should.ThrowAsync<ObjectDisposedException>(() =>
			store.SaveSnapshotAsync(snapshot, CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task SaveSnapshotAsync_EvictsOldestSnapshot_WhenMaxSnapshotsReached()
	{
		// Arrange
		var options = Options.Create(new InMemorySnapshotOptions { MaxSnapshots = 2 });
		using var store = new InMemorySnapshotStore(options, _logger);

		var oldest = CreateSnapshot("agg1", "TestAggregate", 1, DateTime.UtcNow.AddHours(-2));
		var middle = CreateSnapshot("agg2", "TestAggregate", 1, DateTime.UtcNow.AddHours(-1));
		var newest = CreateSnapshot("agg3", "TestAggregate", 1, DateTime.UtcNow);

		// Act
		await store.SaveSnapshotAsync(oldest, CancellationToken.None);
		await store.SaveSnapshotAsync(middle, CancellationToken.None);
		await store.SaveSnapshotAsync(newest, CancellationToken.None);

		// Assert - oldest should be evicted
		var oldestRetrieved = await store.GetLatestSnapshotAsync("agg1", "TestAggregate", CancellationToken.None);
		oldestRetrieved.ShouldBeNull();

		var middleRetrieved = await store.GetLatestSnapshotAsync("agg2", "TestAggregate", CancellationToken.None);
		middleRetrieved.ShouldNotBeNull();

		var newestRetrieved = await store.GetLatestSnapshotAsync("agg3", "TestAggregate", CancellationToken.None);
		newestRetrieved.ShouldNotBeNull();
	}

	#endregion SaveSnapshotAsync Tests

	#region GetLatestSnapshotAsync Tests

	[Fact]
	public async Task GetLatestSnapshotAsync_ReturnsNull_WhenSnapshotDoesNotExist()
	{
		// Act
		var result = await _store.GetLatestSnapshotAsync("nonexistent", "TestAggregate", CancellationToken.None);

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public async Task GetLatestSnapshotAsync_ReturnsSnapshot_WhenExists()
	{
		// Arrange
		var snapshot = CreateSnapshot("agg1", "TestAggregate", 5);
		await _store.SaveSnapshotAsync(snapshot, CancellationToken.None);

		// Act
		var result = await _store.GetLatestSnapshotAsync("agg1", "TestAggregate", CancellationToken.None);

		// Assert
		result.ShouldNotBeNull();
		result.AggregateId.ShouldBe("agg1");
		result.AggregateType.ShouldBe("TestAggregate");
		result.Version.ShouldBe(5);
	}

	[Fact]
	public async Task GetLatestSnapshotAsync_ThrowsArgumentException_WhenAggregateIdIsNull()
	{
		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(() =>
			_store.GetLatestSnapshotAsync(null!, "TestAggregate", CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task GetLatestSnapshotAsync_ThrowsArgumentException_WhenAggregateIdIsEmpty()
	{
		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(() =>
			_store.GetLatestSnapshotAsync("", "TestAggregate", CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task GetLatestSnapshotAsync_ThrowsArgumentException_WhenAggregateIdIsWhitespace()
	{
		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(() =>
			_store.GetLatestSnapshotAsync("   ", "TestAggregate", CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task GetLatestSnapshotAsync_ThrowsArgumentException_WhenAggregateTypeIsNull()
	{
		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(() =>
			_store.GetLatestSnapshotAsync("agg1", null!, CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task GetLatestSnapshotAsync_ThrowsArgumentException_WhenAggregateTypeIsEmpty()
	{
		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(() =>
			_store.GetLatestSnapshotAsync("agg1", "", CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task GetLatestSnapshotAsync_ThrowsObjectDisposedException_WhenDisposed()
	{
		// Arrange
		var options = Options.Create(new InMemorySnapshotOptions());
		var store = new InMemorySnapshotStore(options, _logger);
		store.Dispose();

		// Act & Assert
		_ = await Should.ThrowAsync<ObjectDisposedException>(() =>
			store.GetLatestSnapshotAsync("agg1", "TestAggregate", CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task GetLatestSnapshotAsync_ReturnsCorrectSnapshot_ForDifferentAggregateTypes()
	{
		// Arrange
		var snapshot1 = CreateSnapshot("agg1", "TypeA", 1);
		var snapshot2 = CreateSnapshot("agg1", "TypeB", 2);

		await _store.SaveSnapshotAsync(snapshot1, CancellationToken.None);
		await _store.SaveSnapshotAsync(snapshot2, CancellationToken.None);

		// Act
		var resultA = await _store.GetLatestSnapshotAsync("agg1", "TypeA", CancellationToken.None);
		var resultB = await _store.GetLatestSnapshotAsync("agg1", "TypeB", CancellationToken.None);

		// Assert
		resultA.ShouldNotBeNull();
		resultA.AggregateType.ShouldBe("TypeA");
		resultA.Version.ShouldBe(1);

		resultB.ShouldNotBeNull();
		resultB.AggregateType.ShouldBe("TypeB");
		resultB.Version.ShouldBe(2);
	}

	#endregion GetLatestSnapshotAsync Tests

	#region DeleteSnapshotsAsync Tests

	[Fact]
	public async Task DeleteSnapshotsAsync_RemovesSnapshot_WhenExists()
	{
		// Arrange
		var snapshot = CreateSnapshot("agg1", "TestAggregate", 1);
		await _store.SaveSnapshotAsync(snapshot, CancellationToken.None);

		// Act
		await _store.DeleteSnapshotsAsync("agg1", "TestAggregate", CancellationToken.None);

		// Assert
		var result = await _store.GetLatestSnapshotAsync("agg1", "TestAggregate", CancellationToken.None);
		result.ShouldBeNull();
	}

	[Fact]
	public async Task DeleteSnapshotsAsync_DoesNotThrow_WhenSnapshotDoesNotExist()
	{
		// Act & Assert - Should not throw
		await Should.NotThrowAsync(() =>
			_store.DeleteSnapshotsAsync("nonexistent", "TestAggregate", CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task DeleteSnapshotsAsync_ThrowsArgumentException_WhenAggregateIdIsNull()
	{
		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(() =>
			_store.DeleteSnapshotsAsync(null!, "TestAggregate", CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task DeleteSnapshotsAsync_ThrowsArgumentException_WhenAggregateTypeIsNull()
	{
		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(() =>
			_store.DeleteSnapshotsAsync("agg1", null!, CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task DeleteSnapshotsAsync_ThrowsObjectDisposedException_WhenDisposed()
	{
		// Arrange
		var options = Options.Create(new InMemorySnapshotOptions());
		var store = new InMemorySnapshotStore(options, _logger);
		store.Dispose();

		// Act & Assert
		_ = await Should.ThrowAsync<ObjectDisposedException>(() =>
			store.DeleteSnapshotsAsync("agg1", "TestAggregate", CancellationToken.None).AsTask());
	}

	#endregion DeleteSnapshotsAsync Tests

	#region DeleteSnapshotsOlderThanAsync Tests

	[Fact]
	public async Task DeleteSnapshotsOlderThanAsync_RemovesSnapshot_WhenVersionIsBelow()
	{
		// Arrange
		var snapshot = CreateSnapshot("agg1", "TestAggregate", 5);
		await _store.SaveSnapshotAsync(snapshot, CancellationToken.None);

		// Act
		await _store.DeleteSnapshotsOlderThanAsync("agg1", "TestAggregate", 10, CancellationToken.None);

		// Assert
		var result = await _store.GetLatestSnapshotAsync("agg1", "TestAggregate", CancellationToken.None);
		result.ShouldBeNull();
	}

	[Fact]
	public async Task DeleteSnapshotsOlderThanAsync_KeepsSnapshot_WhenVersionIsEqual()
	{
		// Arrange
		var snapshot = CreateSnapshot("agg1", "TestAggregate", 5);
		await _store.SaveSnapshotAsync(snapshot, CancellationToken.None);

		// Act
		await _store.DeleteSnapshotsOlderThanAsync("agg1", "TestAggregate", 5, CancellationToken.None);

		// Assert
		var result = await _store.GetLatestSnapshotAsync("agg1", "TestAggregate", CancellationToken.None);
		result.ShouldNotBeNull();
		result.Version.ShouldBe(5);
	}

	[Fact]
	public async Task DeleteSnapshotsOlderThanAsync_KeepsSnapshot_WhenVersionIsAbove()
	{
		// Arrange
		var snapshot = CreateSnapshot("agg1", "TestAggregate", 10);
		await _store.SaveSnapshotAsync(snapshot, CancellationToken.None);

		// Act
		await _store.DeleteSnapshotsOlderThanAsync("agg1", "TestAggregate", 5, CancellationToken.None);

		// Assert
		var result = await _store.GetLatestSnapshotAsync("agg1", "TestAggregate", CancellationToken.None);
		result.ShouldNotBeNull();
		result.Version.ShouldBe(10);
	}

	[Fact]
	public async Task DeleteSnapshotsOlderThanAsync_DoesNotThrow_WhenSnapshotDoesNotExist()
	{
		// Act & Assert - Should not throw
		await Should.NotThrowAsync(() =>
			_store.DeleteSnapshotsOlderThanAsync("nonexistent", "TestAggregate", 10, CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task DeleteSnapshotsOlderThanAsync_ThrowsArgumentException_WhenAggregateIdIsNull()
	{
		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(() =>
			_store.DeleteSnapshotsOlderThanAsync(null!, "TestAggregate", 10, CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task DeleteSnapshotsOlderThanAsync_ThrowsArgumentException_WhenAggregateTypeIsNull()
	{
		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(() =>
			_store.DeleteSnapshotsOlderThanAsync("agg1", null!, 10, CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task DeleteSnapshotsOlderThanAsync_ThrowsObjectDisposedException_WhenDisposed()
	{
		// Arrange
		var options = Options.Create(new InMemorySnapshotOptions());
		var store = new InMemorySnapshotStore(options, _logger);
		store.Dispose();

		// Act & Assert
		_ = await Should.ThrowAsync<ObjectDisposedException>(() =>
			store.DeleteSnapshotsOlderThanAsync("agg1", "TestAggregate", 10, CancellationToken.None).AsTask());
	}

	#endregion DeleteSnapshotsOlderThanAsync Tests

	#region Dispose Tests

	[Fact]
	public async Task Dispose_ClearsAllSnapshots()
	{
		// Arrange
		var options = Options.Create(new InMemorySnapshotOptions());
		var store = new InMemorySnapshotStore(options, _logger);
		var snapshot = CreateSnapshot("agg1", "TestAggregate", 1);
		await store.SaveSnapshotAsync(snapshot, CancellationToken.None);

		// Act
		store.Dispose();

		// Assert - Attempting any operation should throw ObjectDisposedException
		_ = await Should.ThrowAsync<ObjectDisposedException>(() =>
			store.GetLatestSnapshotAsync("agg1", "TestAggregate", CancellationToken.None).AsTask());
	}

	[Fact]
	public void Dispose_IsIdempotent()
	{
		// Arrange
		var options = Options.Create(new InMemorySnapshotOptions());
		var store = new InMemorySnapshotStore(options, _logger);

		// Act & Assert - Should not throw
		Should.NotThrow(() =>
		{
			store.Dispose();
			store.Dispose();
			store.Dispose();
		});
	}

	[Fact]
	public async Task DisposeAsync_ClearsAllSnapshots()
	{
		// Arrange
		var options = Options.Create(new InMemorySnapshotOptions());
		var store = new InMemorySnapshotStore(options, _logger);
		var snapshot = CreateSnapshot("agg1", "TestAggregate", 1);
		await store.SaveSnapshotAsync(snapshot, CancellationToken.None);

		// Act
		await store.DisposeAsync();

		// Assert - Attempting any operation should throw ObjectDisposedException
		_ = await Should.ThrowAsync<ObjectDisposedException>(() =>
			store.GetLatestSnapshotAsync("agg1", "TestAggregate", CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task DisposeAsync_IsIdempotent()
	{
		// Arrange
		var options = Options.Create(new InMemorySnapshotOptions());
		var store = new InMemorySnapshotStore(options, _logger);

		// Act & Assert - Should not throw
		await Should.NotThrowAsync(async () =>
		{
			await store.DisposeAsync();
			await store.DisposeAsync();
			await store.DisposeAsync();
		});
	}

	#endregion Dispose Tests

	#region Interface Implementation Tests

	[Fact]
	public void ImplementsISnapshotStore()
	{
		// Assert
		_ = _store.ShouldBeAssignableTo<ISnapshotStore>();
	}

	[Fact]
	public void ImplementsIDisposable()
	{
		// Assert
		_ = _store.ShouldBeAssignableTo<IDisposable>();
	}

	[Fact]
	public void ImplementsIAsyncDisposable()
	{
		// Assert
		_ = _store.ShouldBeAssignableTo<IAsyncDisposable>();
	}

	#endregion Interface Implementation Tests

	/// <inheritdoc/>
	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			_store?.Dispose();
		}
		base.Dispose(disposing);
	}

	private static ISnapshot CreateSnapshot(string aggregateId, string aggregateType, long version, DateTime? createdAt = null)
	{
		var snapshot = A.Fake<ISnapshot>();
		A.CallTo(() => snapshot.SnapshotId).Returns(Guid.NewGuid().ToString());
		A.CallTo(() => snapshot.AggregateId).Returns(aggregateId);
		A.CallTo(() => snapshot.AggregateType).Returns(aggregateType);
		A.CallTo(() => snapshot.Version).Returns(version);
		A.CallTo(() => snapshot.CreatedAt).Returns(createdAt ?? DateTime.UtcNow);
		A.CallTo(() => snapshot.Data).Returns(new byte[] { 1, 2, 3 });
		A.CallTo(() => snapshot.Metadata).Returns(null);
		return snapshot;
	}
}
