// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Firestore.Cdc;

namespace Excalibur.Data.Tests.Firestore;

/// <summary>
/// Unit tests for <see cref="InMemoryFirestoreCdcStateStore"/>.
/// </summary>
/// <remarks>
/// Sprint 515 (S515.2): Firestore unit tests.
/// Tests verify in-memory CDC state store operations.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Firestore")]
[Trait("Feature", "CDC")]
public sealed class InMemoryFirestoreCdcStateStoreShould : IAsyncDisposable
{
	private readonly InMemoryFirestoreCdcStateStore _store = new();
	private const string TestProcessorName = "test-processor";
	private const string TestCollectionPath = "test-collection";

	public async ValueTask DisposeAsync()
	{
		await _store.DisposeAsync();
	}

	#region GetPositionAsync Tests

	[Fact]
	public async Task GetPositionAsync_ReturnsNull_WhenNoPositionSaved()
	{
		// Act
		var position = await _store.GetPositionAsync(TestProcessorName, CancellationToken.None);

		// Assert
		position.ShouldBeNull();
	}

	[Fact]
	public async Task GetPositionAsync_ThrowsArgumentException_WhenProcessorNameIsNull()
	{
		// Act & Assert
		await Should.ThrowAsync<ArgumentException>(async () =>
			await _store.GetPositionAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task GetPositionAsync_ThrowsArgumentException_WhenProcessorNameIsEmpty()
	{
		// Act & Assert
		await Should.ThrowAsync<ArgumentException>(async () =>
			await _store.GetPositionAsync("", CancellationToken.None));
	}

	[Fact]
	public async Task GetPositionAsync_ThrowsArgumentException_WhenProcessorNameIsWhitespace()
	{
		// Act & Assert
		await Should.ThrowAsync<ArgumentException>(async () =>
			await _store.GetPositionAsync("   ", CancellationToken.None));
	}

	[Fact]
	public async Task GetPositionAsync_ThrowsObjectDisposedException_WhenDisposed()
	{
		// Arrange
		await _store.DisposeAsync();

		// Act & Assert
		await Should.ThrowAsync<ObjectDisposedException>(async () =>
			await _store.GetPositionAsync(TestProcessorName, CancellationToken.None));
	}

	#endregion

	#region SavePositionAsync Tests

	[Fact]
	public async Task SavePositionAsync_SavesPosition()
	{
		// Arrange
		var position = FirestoreCdcPosition.Now(TestCollectionPath);

		// Act
		await _store.SavePositionAsync(TestProcessorName, position, CancellationToken.None);
		var retrieved = await _store.GetPositionAsync(TestProcessorName, CancellationToken.None);

		// Assert
		retrieved.ShouldNotBeNull();
		retrieved.CollectionPath.ShouldBe(TestCollectionPath);
	}

	[Fact]
	public async Task SavePositionAsync_OverwritesPreviousPosition()
	{
		// Arrange
		var position1 = FirestoreCdcPosition.Now("collection1");
		var position2 = FirestoreCdcPosition.Now("collection2");

		// Act
		await _store.SavePositionAsync(TestProcessorName, position1, CancellationToken.None);
		await _store.SavePositionAsync(TestProcessorName, position2, CancellationToken.None);
		var retrieved = await _store.GetPositionAsync(TestProcessorName, CancellationToken.None);

		// Assert
		retrieved.ShouldNotBeNull();
		retrieved.CollectionPath.ShouldBe("collection2");
	}

	[Fact]
	public async Task SavePositionAsync_ThrowsArgumentException_WhenProcessorNameIsNull()
	{
		// Arrange
		var position = FirestoreCdcPosition.Now(TestCollectionPath);

		// Act & Assert
		await Should.ThrowAsync<ArgumentException>(async () =>
			await _store.SavePositionAsync(null!, position, CancellationToken.None));
	}

	[Fact]
	public async Task SavePositionAsync_ThrowsArgumentNullException_WhenPositionIsNull()
	{
		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(async () =>
			await _store.SavePositionAsync(TestProcessorName, null!, CancellationToken.None));
	}

	[Fact]
	public async Task SavePositionAsync_ThrowsObjectDisposedException_WhenDisposed()
	{
		// Arrange
		var position = FirestoreCdcPosition.Now(TestCollectionPath);
		await _store.DisposeAsync();

		// Act & Assert
		await Should.ThrowAsync<ObjectDisposedException>(async () =>
			await _store.SavePositionAsync(TestProcessorName, position, CancellationToken.None));
	}

	#endregion

	#region DeletePositionAsync Tests

	[Fact]
	public async Task DeletePositionAsync_DeletesSavedPosition()
	{
		// Arrange
		var position = FirestoreCdcPosition.Now(TestCollectionPath);
		await _store.SavePositionAsync(TestProcessorName, position, CancellationToken.None);

		// Act
		await _store.DeletePositionAsync(TestProcessorName, CancellationToken.None);
		var retrieved = await _store.GetPositionAsync(TestProcessorName, CancellationToken.None);

		// Assert
		retrieved.ShouldBeNull();
	}

	[Fact]
	public async Task DeletePositionAsync_DoesNotThrow_WhenPositionDoesNotExist()
	{
		// Act & Assert
		await Should.NotThrowAsync(async () =>
			await _store.DeletePositionAsync("nonexistent", CancellationToken.None));
	}

	[Fact]
	public async Task DeletePositionAsync_ThrowsArgumentException_WhenProcessorNameIsNull()
	{
		// Act & Assert
		await Should.ThrowAsync<ArgumentException>(async () =>
			await _store.DeletePositionAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task DeletePositionAsync_ThrowsObjectDisposedException_WhenDisposed()
	{
		// Arrange
		await _store.DisposeAsync();

		// Act & Assert
		await Should.ThrowAsync<ObjectDisposedException>(async () =>
			await _store.DeletePositionAsync(TestProcessorName, CancellationToken.None));
	}

	#endregion

	#region GetAllPositions Tests

	[Fact]
	public void GetAllPositions_ReturnsEmptyDictionary_WhenNoPositionsSaved()
	{
		// Act
		var positions = _store.GetAllPositions();

		// Assert
		positions.ShouldBeEmpty();
	}

	[Fact]
	public async Task GetAllPositions_ReturnsAllSavedPositions()
	{
		// Arrange
		var position1 = FirestoreCdcPosition.Now("collection1");
		var position2 = FirestoreCdcPosition.Now("collection2");
		await _store.SavePositionAsync("processor1", position1, CancellationToken.None);
		await _store.SavePositionAsync("processor2", position2, CancellationToken.None);

		// Act
		var positions = _store.GetAllPositions();

		// Assert
		positions.Count.ShouldBe(2);
		positions.ContainsKey("processor1").ShouldBeTrue();
		positions.ContainsKey("processor2").ShouldBeTrue();
	}

	#endregion

	#region Clear Tests

	[Fact]
	public async Task Clear_RemovesAllPositions()
	{
		// Arrange
		var position = FirestoreCdcPosition.Now(TestCollectionPath);
		await _store.SavePositionAsync("processor1", position, CancellationToken.None);
		await _store.SavePositionAsync("processor2", position, CancellationToken.None);

		// Act
		_store.Clear();

		// Assert
		_store.GetAllPositions().ShouldBeEmpty();
	}

	#endregion

	#region Multiple Processors Tests

	[Fact]
	public async Task SupportsMultipleProcessors()
	{
		// Arrange
		var position1 = FirestoreCdcPosition.Now("collection1");
		var position2 = FirestoreCdcPosition.Now("collection2");

		// Act
		await _store.SavePositionAsync("processor1", position1, CancellationToken.None);
		await _store.SavePositionAsync("processor2", position2, CancellationToken.None);

		var retrieved1 = await _store.GetPositionAsync("processor1", CancellationToken.None);
		var retrieved2 = await _store.GetPositionAsync("processor2", CancellationToken.None);

		// Assert
		retrieved1.ShouldNotBeNull();
		retrieved1.CollectionPath.ShouldBe("collection1");
		retrieved2.ShouldNotBeNull();
		retrieved2.CollectionPath.ShouldBe("collection2");
	}

	#endregion

	#region Dispose Tests

	[Fact]
	public async Task DisposeAsync_CanBeCalledMultipleTimes()
	{
		// Act & Assert
		await Should.NotThrowAsync(async () =>
		{
			await _store.DisposeAsync();
			await _store.DisposeAsync();
		});
	}

	[Fact]
	public void Dispose_CanBeCalledMultipleTimes()
	{
		// Act & Assert
		Should.NotThrow(() =>
		{
			_store.Dispose();
			_store.Dispose();
		});
	}

	#endregion

	#region Type Tests

	[Fact]
	public void BeSealed()
	{
		// Assert
		typeof(InMemoryFirestoreCdcStateStore).IsSealed.ShouldBeTrue();
	}

	[Fact]
	public void BePublic()
	{
		// Assert
		typeof(InMemoryFirestoreCdcStateStore).IsPublic.ShouldBeTrue();
	}

	[Fact]
	public void ImplementIFirestoreCdcStateStore()
	{
		// Assert
		typeof(IFirestoreCdcStateStore).IsAssignableFrom(typeof(InMemoryFirestoreCdcStateStore)).ShouldBeTrue();
	}

	[Fact]
	public void ImplementIAsyncDisposable()
	{
		// Assert
		typeof(IAsyncDisposable).IsAssignableFrom(typeof(InMemoryFirestoreCdcStateStore)).ShouldBeTrue();
	}

	[Fact]
	public void ImplementIDisposable()
	{
		// Assert
		typeof(IDisposable).IsAssignableFrom(typeof(InMemoryFirestoreCdcStateStore)).ShouldBeTrue();
	}

	#endregion
}
