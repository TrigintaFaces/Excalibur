// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga.Idempotency;

namespace Excalibur.Saga.Tests.Core.Idempotency;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class InMemorySagaIdempotencyDepthShould
{
	private readonly InMemorySagaIdempotencyProvider _sut = new();

	[Fact]
	public async Task IsProcessedReturnsFalseForNewKey()
	{
		// Act
		var result = await _sut.IsProcessedAsync("saga-1", "key-1", CancellationToken.None);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public async Task IsProcessedReturnsTrueAfterMarkProcessed()
	{
		// Arrange
		await _sut.MarkProcessedAsync("saga-1", "key-1", CancellationToken.None);

		// Act
		var result = await _sut.IsProcessedAsync("saga-1", "key-1", CancellationToken.None);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public async Task DifferentSagaIdsAreIndependent()
	{
		// Arrange
		await _sut.MarkProcessedAsync("saga-1", "key-1", CancellationToken.None);

		// Act
		var result = await _sut.IsProcessedAsync("saga-2", "key-1", CancellationToken.None);

		// Assert - different saga, same key should not be processed
		result.ShouldBeFalse();
	}

	[Fact]
	public async Task DifferentKeysAreIndependent()
	{
		// Arrange
		await _sut.MarkProcessedAsync("saga-1", "key-1", CancellationToken.None);

		// Act
		var result = await _sut.IsProcessedAsync("saga-1", "key-2", CancellationToken.None);

		// Assert - same saga, different key should not be processed
		result.ShouldBeFalse();
	}

	[Fact]
	public async Task MarkProcessedIsIdempotent()
	{
		// Arrange & Act - mark same key twice
		await _sut.MarkProcessedAsync("saga-1", "key-1", CancellationToken.None);
		await _sut.MarkProcessedAsync("saga-1", "key-1", CancellationToken.None);

		// Assert - count should be 1, not 2
		_sut.Count.ShouldBe(1);
	}

	[Fact]
	public async Task CountReflectsTrackedKeys()
	{
		// Arrange
		_sut.Count.ShouldBe(0);

		// Act
		await _sut.MarkProcessedAsync("saga-1", "key-1", CancellationToken.None);
		await _sut.MarkProcessedAsync("saga-1", "key-2", CancellationToken.None);
		await _sut.MarkProcessedAsync("saga-2", "key-1", CancellationToken.None);

		// Assert
		_sut.Count.ShouldBe(3);
	}

	[Fact]
	public async Task ClearRemovesAllTrackedKeys()
	{
		// Arrange
		await _sut.MarkProcessedAsync("saga-1", "key-1", CancellationToken.None);
		await _sut.MarkProcessedAsync("saga-2", "key-2", CancellationToken.None);
		_sut.Count.ShouldBe(2);

		// Act
		_sut.Clear();

		// Assert
		_sut.Count.ShouldBe(0);
		(await _sut.IsProcessedAsync("saga-1", "key-1", CancellationToken.None)).ShouldBeFalse();
		(await _sut.IsProcessedAsync("saga-2", "key-2", CancellationToken.None)).ShouldBeFalse();
	}

	[Fact]
	public async Task IsProcessedThrowsWhenSagaIdIsNull()
	{
		await Should.ThrowAsync<ArgumentException>(async () =>
			await _sut.IsProcessedAsync(null!, "key-1", CancellationToken.None));
	}

	[Fact]
	public async Task IsProcessedThrowsWhenSagaIdIsWhiteSpace()
	{
		await Should.ThrowAsync<ArgumentException>(async () =>
			await _sut.IsProcessedAsync("  ", "key-1", CancellationToken.None));
	}

	[Fact]
	public async Task IsProcessedThrowsWhenKeyIsNull()
	{
		await Should.ThrowAsync<ArgumentException>(async () =>
			await _sut.IsProcessedAsync("saga-1", null!, CancellationToken.None));
	}

	[Fact]
	public async Task IsProcessedThrowsWhenKeyIsWhiteSpace()
	{
		await Should.ThrowAsync<ArgumentException>(async () =>
			await _sut.IsProcessedAsync("saga-1", "  ", CancellationToken.None));
	}

	[Fact]
	public async Task MarkProcessedThrowsWhenSagaIdIsNull()
	{
		await Should.ThrowAsync<ArgumentException>(async () =>
			await _sut.MarkProcessedAsync(null!, "key-1", CancellationToken.None));
	}

	[Fact]
	public async Task MarkProcessedThrowsWhenKeyIsNull()
	{
		await Should.ThrowAsync<ArgumentException>(async () =>
			await _sut.MarkProcessedAsync("saga-1", null!, CancellationToken.None));
	}

	[Fact]
	public async Task IsProcessedThrowsWhenCancelled()
	{
		// Arrange
		using var cts = new CancellationTokenSource();
		await cts.CancelAsync();

		// Act & Assert
		await Should.ThrowAsync<OperationCanceledException>(async () =>
			await _sut.IsProcessedAsync("saga-1", "key-1", cts.Token));
	}

	[Fact]
	public async Task MarkProcessedThrowsWhenCancelled()
	{
		// Arrange
		using var cts = new CancellationTokenSource();
		await cts.CancelAsync();

		// Act & Assert
		await Should.ThrowAsync<OperationCanceledException>(async () =>
			await _sut.MarkProcessedAsync("saga-1", "key-1", cts.Token));
	}
}
