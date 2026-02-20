// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga.Idempotency;

namespace Excalibur.Saga.Tests.Idempotency;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class InMemorySagaIdempotencyProviderShould
{
	[Fact]
	public async Task ReturnFalseForUnprocessedKey()
	{
		// Arrange
		var sut = new InMemorySagaIdempotencyProvider();

		// Act
		var result = await sut.IsProcessedAsync("saga-1", "key-1", CancellationToken.None);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public async Task ReturnTrueAfterMarkingAsProcessed()
	{
		// Arrange
		var sut = new InMemorySagaIdempotencyProvider();
		await sut.MarkProcessedAsync("saga-1", "key-1", CancellationToken.None);

		// Act
		var result = await sut.IsProcessedAsync("saga-1", "key-1", CancellationToken.None);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public async Task TrackDifferentSagaIdsIndependently()
	{
		// Arrange
		var sut = new InMemorySagaIdempotencyProvider();
		await sut.MarkProcessedAsync("saga-1", "key-1", CancellationToken.None);

		// Act
		var result = await sut.IsProcessedAsync("saga-2", "key-1", CancellationToken.None);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public async Task TrackDifferentKeysIndependently()
	{
		// Arrange
		var sut = new InMemorySagaIdempotencyProvider();
		await sut.MarkProcessedAsync("saga-1", "key-1", CancellationToken.None);

		// Act
		var result = await sut.IsProcessedAsync("saga-1", "key-2", CancellationToken.None);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public async Task IncrementCountOnMarkProcessed()
	{
		// Arrange
		var sut = new InMemorySagaIdempotencyProvider();

		// Act
		await sut.MarkProcessedAsync("saga-1", "key-1", CancellationToken.None);
		await sut.MarkProcessedAsync("saga-1", "key-2", CancellationToken.None);

		// Assert
		sut.Count.ShouldBe(2);
	}

	[Fact]
	public void StartWithZeroCount()
	{
		// Arrange & Act
		var sut = new InMemorySagaIdempotencyProvider();

		// Assert
		sut.Count.ShouldBe(0);
	}

	[Fact]
	public async Task ClearAllTrackedKeys()
	{
		// Arrange
		var sut = new InMemorySagaIdempotencyProvider();
		await sut.MarkProcessedAsync("saga-1", "key-1", CancellationToken.None);
		await sut.MarkProcessedAsync("saga-2", "key-2", CancellationToken.None);

		// Act
		sut.Clear();

		// Assert
		sut.Count.ShouldBe(0);
		(await sut.IsProcessedAsync("saga-1", "key-1", CancellationToken.None)).ShouldBeFalse();
	}

	[Fact]
	public async Task ThrowOnNullOrWhitespaceSagaIdForIsProcessed()
	{
		// Arrange
		var sut = new InMemorySagaIdempotencyProvider();

		// Act & Assert
		await Should.ThrowAsync<ArgumentException>(
			() => sut.IsProcessedAsync(null!, "key", CancellationToken.None));
		await Should.ThrowAsync<ArgumentException>(
			() => sut.IsProcessedAsync("", "key", CancellationToken.None));
		await Should.ThrowAsync<ArgumentException>(
			() => sut.IsProcessedAsync("  ", "key", CancellationToken.None));
	}

	[Fact]
	public async Task ThrowOnNullOrWhitespaceIdempotencyKeyForIsProcessed()
	{
		// Arrange
		var sut = new InMemorySagaIdempotencyProvider();

		// Act & Assert
		await Should.ThrowAsync<ArgumentException>(
			() => sut.IsProcessedAsync("saga-1", null!, CancellationToken.None));
		await Should.ThrowAsync<ArgumentException>(
			() => sut.IsProcessedAsync("saga-1", "", CancellationToken.None));
		await Should.ThrowAsync<ArgumentException>(
			() => sut.IsProcessedAsync("saga-1", "  ", CancellationToken.None));
	}

	[Fact]
	public async Task ThrowOnNullOrWhitespaceSagaIdForMarkProcessed()
	{
		// Arrange
		var sut = new InMemorySagaIdempotencyProvider();

		// Act & Assert
		await Should.ThrowAsync<ArgumentException>(
			() => sut.MarkProcessedAsync(null!, "key", CancellationToken.None));
	}

	[Fact]
	public async Task ThrowOnCancelledToken()
	{
		// Arrange
		var sut = new InMemorySagaIdempotencyProvider();
		using var cts = new CancellationTokenSource();
		await cts.CancelAsync();

		// Act & Assert
		await Should.ThrowAsync<OperationCanceledException>(
			() => sut.IsProcessedAsync("saga-1", "key-1", cts.Token));
	}

	[Fact]
	public void ImplementISagaIdempotencyProvider()
	{
		// Arrange & Act
		var sut = new InMemorySagaIdempotencyProvider();

		// Assert
		sut.ShouldBeAssignableTo<ISagaIdempotencyProvider>();
	}

	[Fact]
	public async Task NotFailOnDuplicateMarkProcessed()
	{
		// Arrange
		var sut = new InMemorySagaIdempotencyProvider();

		// Act — marking same key twice should not throw
		await sut.MarkProcessedAsync("saga-1", "key-1", CancellationToken.None);
		await sut.MarkProcessedAsync("saga-1", "key-1", CancellationToken.None);

		// Assert
		sut.Count.ShouldBe(1); // Should not double-count
	}
}
