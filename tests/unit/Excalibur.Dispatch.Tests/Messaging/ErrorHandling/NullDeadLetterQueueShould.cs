// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.ErrorHandling;

namespace Excalibur.Dispatch.Tests.Messaging.ErrorHandling;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class NullDeadLetterQueueShould
{
	#region IDeadLetterQueue Tests

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
	public async Task GetCountAsync_ReturnZero()
	{
		// Arrange
		var dlq = NullDeadLetterQueue.Instance;

		// Act
		var count = await dlq.GetCountAsync(CancellationToken.None);

		// Assert
		count.ShouldBe(0L);
	}

	[Fact]
	public async Task GetCountAsync_WithFilter_ReturnZero()
	{
		// Arrange
		var dlq = NullDeadLetterQueue.Instance;
		var filter = new DeadLetterQueryFilter { Reason = DeadLetterReason.MaxRetriesExceeded };

		// Act
		var count = await dlq.GetCountAsync(CancellationToken.None, filter);

		// Assert
		count.ShouldBe(0L);
	}

	[Fact]
	public async Task GetEntriesAsync_WithFilter_ReturnEmptyList()
	{
		// Arrange
		var dlq = NullDeadLetterQueue.Instance;
		var filter = new DeadLetterQueryFilter { Reason = DeadLetterReason.ValidationFailed };

		// Act
		var entries = await dlq.GetEntriesAsync(CancellationToken.None, filter, limit: 50);

		// Assert
		entries.ShouldNotBeNull();
		entries.ShouldBeEmpty();
	}

	#endregion IDeadLetterQueue Tests

	#region IDeadLetterQueueAdmin Tests

	[Fact]
	public void ImplementIDeadLetterQueueAdmin()
	{
		// Assert
		typeof(IDeadLetterQueueAdmin).IsAssignableFrom(typeof(NullDeadLetterQueue)).ShouldBeTrue(
			"NullDeadLetterQueue must implement IDeadLetterQueueAdmin");
	}

	[Fact]
	public async Task Admin_ReplayBatchAsync_ReturnZero()
	{
		// Arrange
		IDeadLetterQueueAdmin admin = NullDeadLetterQueue.Instance;
		var filter = new DeadLetterQueryFilter { Reason = DeadLetterReason.MaxRetriesExceeded };

		// Act
		var result = await admin.ReplayBatchAsync(filter, CancellationToken.None);

		// Assert
		result.ShouldBe(0);
	}

	[Fact]
	public async Task Admin_PurgeAsync_ReturnFalse()
	{
		// Arrange
		IDeadLetterQueueAdmin admin = NullDeadLetterQueue.Instance;

		// Act
		var result = await admin.PurgeAsync(Guid.NewGuid(), CancellationToken.None);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public async Task Admin_PurgeAsync_WithEmptyGuid_ReturnFalse()
	{
		// Arrange
		IDeadLetterQueueAdmin admin = NullDeadLetterQueue.Instance;

		// Act
		var result = await admin.PurgeAsync(Guid.Empty, CancellationToken.None);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public async Task Admin_PurgeOlderThanAsync_ReturnZero()
	{
		// Arrange
		IDeadLetterQueueAdmin admin = NullDeadLetterQueue.Instance;

		// Act
		var result = await admin.PurgeOlderThanAsync(TimeSpan.FromDays(30), CancellationToken.None);

		// Assert
		result.ShouldBe(0);
	}

	[Fact]
	public async Task Admin_PurgeOlderThanAsync_WithZeroTimeSpan_ReturnZero()
	{
		// Arrange
		IDeadLetterQueueAdmin admin = NullDeadLetterQueue.Instance;

		// Act
		var result = await admin.PurgeOlderThanAsync(TimeSpan.Zero, CancellationToken.None);

		// Assert
		result.ShouldBe(0);
	}

	[Fact]
	public async Task Admin_AllOperationsCompleteSynchronously()
	{
		// Verify all admin no-op methods return completed tasks (no async overhead)
		IDeadLetterQueueAdmin admin = NullDeadLetterQueue.Instance;
		var filter = new DeadLetterQueryFilter();

		// Act
		var replayTask = admin.ReplayBatchAsync(filter, CancellationToken.None);
		var purgeTask = admin.PurgeAsync(Guid.NewGuid(), CancellationToken.None);
		var purgeOlderTask = admin.PurgeOlderThanAsync(TimeSpan.FromHours(1), CancellationToken.None);

		// Assert - all should be completed synchronously (Task.FromResult)
		replayTask.IsCompletedSuccessfully.ShouldBeTrue();
		purgeTask.IsCompletedSuccessfully.ShouldBeTrue();
		purgeOlderTask.IsCompletedSuccessfully.ShouldBeTrue();

		// Await to verify values
		(await replayTask).ShouldBe(0);
		(await purgeTask).ShouldBeFalse();
		(await purgeOlderTask).ShouldBe(0);
	}

	[Fact]
	public async Task Admin_CancellationToken_IsIgnored()
	{
		// NullDeadLetterQueue should succeed even with a cancelled token
		// because operations are no-ops that complete synchronously
		IDeadLetterQueueAdmin admin = NullDeadLetterQueue.Instance;
		using var cts = new CancellationTokenSource();
		cts.Cancel();
		var filter = new DeadLetterQueryFilter();

		// Act & Assert - should not throw OperationCanceledException
		var replayResult = await admin.ReplayBatchAsync(filter, cts.Token);
		replayResult.ShouldBe(0);

		var purgeResult = await admin.PurgeAsync(Guid.NewGuid(), cts.Token);
		purgeResult.ShouldBeFalse();

		var purgeOlderResult = await admin.PurgeOlderThanAsync(TimeSpan.FromDays(1), cts.Token);
		purgeOlderResult.ShouldBe(0);
	}

	#endregion IDeadLetterQueueAdmin Tests
}
