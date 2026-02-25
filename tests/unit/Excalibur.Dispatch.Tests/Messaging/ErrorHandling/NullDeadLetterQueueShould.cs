// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.ErrorHandling;

namespace Excalibur.Dispatch.Tests.Messaging.ErrorHandling;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class NullDeadLetterQueueShould
{
	[Fact]
	public void Instance_ReturnSingleton()
	{
		// Act
		var instance1 = NullDeadLetterQueue.Instance;
		var instance2 = NullDeadLetterQueue.Instance;

		// Assert
		instance1.ShouldNotBeNull();
		instance1.ShouldBeSameAs(instance2);
	}

	[Fact]
	public async Task EnqueueAsync_ReturnEmptyGuid()
	{
		// Arrange
		var dlq = NullDeadLetterQueue.Instance;

		// Act
		var result = await dlq.EnqueueAsync(
			"test-message",
			DeadLetterReason.MaxRetriesExceeded,
			CancellationToken.None);

		// Assert
		result.ShouldBe(Guid.Empty);
	}

	[Fact]
	public async Task EnqueueAsync_WithMetadata_ReturnEmptyGuid()
	{
		// Arrange
		var dlq = NullDeadLetterQueue.Instance;
		var metadata = new Dictionary<string, string> { ["key"] = "value" };

		// Act
		var result = await dlq.EnqueueAsync(
			42,
			DeadLetterReason.ValidationFailed,
			CancellationToken.None,
			exception: new InvalidOperationException("test"),
			metadata: metadata);

		// Assert
		result.ShouldBe(Guid.Empty);
	}

	[Fact]
	public async Task GetEntriesAsync_ReturnEmptyList()
	{
		// Arrange
		var dlq = NullDeadLetterQueue.Instance;

		// Act
		var entries = await dlq.GetEntriesAsync(CancellationToken.None);

		// Assert
		entries.ShouldNotBeNull();
		entries.ShouldBeEmpty();
	}

	[Fact]
	public async Task GetEntryAsync_ReturnNull()
	{
		// Arrange
		var dlq = NullDeadLetterQueue.Instance;

		// Act
		var entry = await dlq.GetEntryAsync(Guid.NewGuid(), CancellationToken.None);

		// Assert
		entry.ShouldBeNull();
	}

	[Fact]
	public async Task ReplayAsync_ReturnFalse()
	{
		// Arrange
		var dlq = NullDeadLetterQueue.Instance;

		// Act
		var result = await dlq.ReplayAsync(Guid.NewGuid(), CancellationToken.None);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public async Task ReplayBatchAsync_ReturnZero()
	{
		// Arrange
		var dlq = NullDeadLetterQueue.Instance;
		var filter = new DeadLetterQueryFilter();

		// Act
		var result = await dlq.ReplayBatchAsync(filter, CancellationToken.None);

		// Assert
		result.ShouldBe(0);
	}

	[Fact]
	public async Task PurgeAsync_ReturnFalse()
	{
		// Arrange
		var dlq = NullDeadLetterQueue.Instance;

		// Act
		var result = await dlq.PurgeAsync(Guid.NewGuid(), CancellationToken.None);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public async Task PurgeOlderThanAsync_ReturnZero()
	{
		// Arrange
		var dlq = NullDeadLetterQueue.Instance;

		// Act
		var result = await dlq.PurgeOlderThanAsync(TimeSpan.FromHours(1), CancellationToken.None);

		// Assert
		result.ShouldBe(0);
	}

	[Fact]
	public async Task GetCountAsync_ReturnZero()
	{
		// Arrange
		var dlq = NullDeadLetterQueue.Instance;

		// Act
		var count = await dlq.GetCountAsync(CancellationToken.None);

		// Assert
		count.ShouldBe(0L);
	}
}
