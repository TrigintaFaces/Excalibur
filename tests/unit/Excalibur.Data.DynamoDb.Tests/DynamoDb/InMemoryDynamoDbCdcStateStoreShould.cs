// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.DynamoDb.Cdc;

namespace Excalibur.Data.Tests.DynamoDb;

/// <summary>
/// Unit tests for <see cref="InMemoryDynamoDbCdcStateStore"/>.
/// </summary>
/// <remarks>
/// Sprint 514 (S514.4): DynamoDB unit tests.
/// Tests verify in-memory CDC state store operations.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "DynamoDb")]
[Trait("Feature", "CDC")]
public sealed class InMemoryDynamoDbCdcStateStoreShould : IAsyncDisposable
{
	private readonly InMemoryDynamoDbCdcStateStore _store = new();

	public async ValueTask DisposeAsync()
	{
		await _store.DisposeAsync();
	}

	#region GetPositionAsync Tests

	[Fact]
	public async Task GetPositionAsync_ReturnsNull_WhenProcessorNotFound()
	{
		// Act
		var result = await _store.GetPositionAsync("unknown-processor", CancellationToken.None);

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public async Task GetPositionAsync_ReturnsPosition_WhenSaved()
	{
		// Arrange
		var streamArn = "arn:aws:dynamodb:us-east-1:123456789012:table/TestTable/stream/2024-01-01";
		var position = DynamoDbCdcPosition.Beginning(streamArn);
		await _store.SavePositionAsync("processor-1", position, CancellationToken.None);

		// Act
		var result = await _store.GetPositionAsync("processor-1", CancellationToken.None);

		// Assert
		result.ShouldNotBeNull();
		result.StreamArn.ShouldBe(streamArn);
	}

	[Fact]
	public async Task GetPositionAsync_ThrowsArgumentException_WhenProcessorNameIsNull()
	{
		// Act & Assert
		await Should.ThrowAsync<ArgumentException>(
			() => _store.GetPositionAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task GetPositionAsync_ThrowsArgumentException_WhenProcessorNameIsEmpty()
	{
		// Act & Assert
		await Should.ThrowAsync<ArgumentException>(
			() => _store.GetPositionAsync(string.Empty, CancellationToken.None));
	}

	[Fact]
	public async Task GetPositionAsync_ThrowsObjectDisposedException_WhenDisposed()
	{
		// Arrange
		await _store.DisposeAsync();

		// Act & Assert
		await Should.ThrowAsync<ObjectDisposedException>(
			() => _store.GetPositionAsync("processor-1", CancellationToken.None));
	}

	#endregion

	#region SavePositionAsync Tests

	[Fact]
	public async Task SavePositionAsync_SavesPosition()
	{
		// Arrange
		var streamArn = "arn:aws:dynamodb:us-east-1:123456789012:table/TestTable/stream/2024-01-01";
		var position = DynamoDbCdcPosition.Beginning(streamArn);

		// Act
		await _store.SavePositionAsync("processor-1", position, CancellationToken.None);

		// Assert
		var result = await _store.GetPositionAsync("processor-1", CancellationToken.None);
		result.ShouldNotBeNull();
	}

	[Fact]
	public async Task SavePositionAsync_OverwritesExistingPosition()
	{
		// Arrange
		var streamArn1 = "arn:aws:dynamodb:us-east-1:123456789012:table/Table1/stream/2024-01-01";
		var streamArn2 = "arn:aws:dynamodb:us-east-1:123456789012:table/Table2/stream/2024-01-01";
		var position1 = DynamoDbCdcPosition.Beginning(streamArn1);
		var position2 = DynamoDbCdcPosition.Beginning(streamArn2);

		// Act
		await _store.SavePositionAsync("processor-1", position1, CancellationToken.None);
		await _store.SavePositionAsync("processor-1", position2, CancellationToken.None);

		// Assert
		var result = await _store.GetPositionAsync("processor-1", CancellationToken.None);
		result.StreamArn.ShouldBe(streamArn2);
	}

	[Fact]
	public async Task SavePositionAsync_ThrowsArgumentException_WhenProcessorNameIsNull()
	{
		// Arrange
		var position = DynamoDbCdcPosition.Beginning("arn:aws:dynamodb:...");

		// Act & Assert
		await Should.ThrowAsync<ArgumentException>(
			() => _store.SavePositionAsync(null!, position, CancellationToken.None));
	}

	[Fact]
	public async Task SavePositionAsync_ThrowsArgumentNullException_WhenPositionIsNull()
	{
		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(
			() => _store.SavePositionAsync("processor-1", null!, CancellationToken.None));
	}

	[Fact]
	public async Task SavePositionAsync_ThrowsObjectDisposedException_WhenDisposed()
	{
		// Arrange
		var position = DynamoDbCdcPosition.Beginning("arn:aws:dynamodb:...");
		await _store.DisposeAsync();

		// Act & Assert
		await Should.ThrowAsync<ObjectDisposedException>(
			() => _store.SavePositionAsync("processor-1", position, CancellationToken.None));
	}

	#endregion

	#region DeletePositionAsync Tests

	[Fact]
	public async Task DeletePositionAsync_RemovesPosition()
	{
		// Arrange
		var position = DynamoDbCdcPosition.Beginning("arn:aws:dynamodb:...");
		await _store.SavePositionAsync("processor-1", position, CancellationToken.None);

		// Act
		await _store.DeletePositionAsync("processor-1", CancellationToken.None);

		// Assert
		var result = await _store.GetPositionAsync("processor-1", CancellationToken.None);
		result.ShouldBeNull();
	}

	[Fact]
	public async Task DeletePositionAsync_DoesNotThrow_WhenProcessorNotFound()
	{
		// Act & Assert - should not throw
		await _store.DeletePositionAsync("unknown-processor", CancellationToken.None);
	}

	[Fact]
	public async Task DeletePositionAsync_ThrowsArgumentException_WhenProcessorNameIsNull()
	{
		// Act & Assert
		await Should.ThrowAsync<ArgumentException>(
			() => _store.DeletePositionAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task DeletePositionAsync_ThrowsObjectDisposedException_WhenDisposed()
	{
		// Arrange
		await _store.DisposeAsync();

		// Act & Assert
		await Should.ThrowAsync<ObjectDisposedException>(
			() => _store.DeletePositionAsync("processor-1", CancellationToken.None));
	}

	#endregion

	#region GetAllPositions Tests

	[Fact]
	public void GetAllPositions_ReturnsEmptyDictionary_WhenNoPositions()
	{
		// Act
		var result = _store.GetAllPositions();

		// Assert
		result.ShouldBeEmpty();
	}

	[Fact]
	public async Task GetAllPositions_ReturnsAllSavedPositions()
	{
		// Arrange
		var position1 = DynamoDbCdcPosition.Beginning("arn:aws:dynamodb:us-east-1:...");
		var position2 = DynamoDbCdcPosition.Beginning("arn:aws:dynamodb:us-west-2:...");
		await _store.SavePositionAsync("processor-1", position1, CancellationToken.None);
		await _store.SavePositionAsync("processor-2", position2, CancellationToken.None);

		// Act
		var result = _store.GetAllPositions();

		// Assert
		result.Count.ShouldBe(2);
		result.ShouldContainKey("processor-1");
		result.ShouldContainKey("processor-2");
	}

	[Fact]
	public async Task GetAllPositions_ReturnsStateEntries_WithCorrectProcessorName()
	{
		// Arrange
		var position = DynamoDbCdcPosition.Beginning("arn:aws:dynamodb:...");
		await _store.SavePositionAsync("my-processor", position, CancellationToken.None);

		// Act
		var result = _store.GetAllPositions();

		// Assert
		result["my-processor"].ProcessorName.ShouldBe("my-processor");
	}

	[Fact]
	public async Task GetAllPositions_ReturnsStateEntries_WithUpdatedAt()
	{
		// Arrange
		var before = DateTimeOffset.UtcNow;
		var position = DynamoDbCdcPosition.Beginning("arn:aws:dynamodb:...");
		await _store.SavePositionAsync("processor-1", position, CancellationToken.None);

		// Act
		var result = _store.GetAllPositions();

		// Assert
		result["processor-1"].UpdatedAt.ShouldBeGreaterThanOrEqualTo(before);
	}

	#endregion

	#region Clear Tests

	[Fact]
	public async Task Clear_RemovesAllPositions()
	{
		// Arrange
		var position1 = DynamoDbCdcPosition.Beginning("arn:aws:dynamodb:...");
		var position2 = DynamoDbCdcPosition.Beginning("arn:aws:dynamodb:...");
		await _store.SavePositionAsync("processor-1", position1, CancellationToken.None);
		await _store.SavePositionAsync("processor-2", position2, CancellationToken.None);

		// Act
		_store.Clear();

		// Assert
		_store.GetAllPositions().ShouldBeEmpty();
	}

	#endregion

	#region Dispose Tests

	[Fact]
	public async Task DisposeAsync_CanBeCalledMultipleTimes()
	{
		// Act & Assert - should not throw
		await _store.DisposeAsync();
		await _store.DisposeAsync();
	}

	[Fact]
	public void Dispose_CanBeCalledMultipleTimes()
	{
		// Act & Assert - should not throw
		_store.Dispose();
		_store.Dispose();
	}

	#endregion

	#region Interface Implementation Tests

	[Fact]
	public void ImplementsIDynamoDbCdcStateStore()
	{
		// Assert
		typeof(IDynamoDbCdcStateStore).IsAssignableFrom(typeof(InMemoryDynamoDbCdcStateStore)).ShouldBeTrue();
	}

	[Fact]
	public void ImplementsIAsyncDisposable()
	{
		// Assert
		typeof(IAsyncDisposable).IsAssignableFrom(typeof(InMemoryDynamoDbCdcStateStore)).ShouldBeTrue();
	}

	[Fact]
	public void ImplementsIDisposable()
	{
		// Assert
		typeof(IDisposable).IsAssignableFrom(typeof(InMemoryDynamoDbCdcStateStore)).ShouldBeTrue();
	}

	#endregion

	#region Type Tests

	[Fact]
	public void IsSealed()
	{
		// Assert
		typeof(InMemoryDynamoDbCdcStateStore).IsSealed.ShouldBeTrue();
	}

	[Fact]
	public void IsPublic()
	{
		// Assert
		typeof(InMemoryDynamoDbCdcStateStore).IsPublic.ShouldBeTrue();
	}

	#endregion
}
