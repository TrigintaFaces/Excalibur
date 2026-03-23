using Excalibur.Dispatch.Delivery;

using Microsoft.Extensions.Logging.Abstractions;

using Tests.Shared.Infrastructure;

namespace Excalibur.Dispatch.Tests.Messaging.Delivery;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class InMemoryDeduplicatorShould : IDisposable
{
	private static readonly TimeSpan ShortExpiry = TimeSpan.FromMilliseconds(100);

	private readonly InMemoryDeduplicator _deduplicator;

	public InMemoryDeduplicatorShould()
	{
		_deduplicator = new InMemoryDeduplicator(
			NullLogger<InMemoryDeduplicator>.Instance,
			cleanupInterval: TimeSpan.FromHours(1)); // Long interval to avoid timer interference
	}

	[Fact]
	public async Task IsDuplicateAsync_ReturnsFalse_ForNewMessage()
	{
		var result = await _deduplicator.IsDuplicateAsync(
			"msg-1", TimeSpan.FromMinutes(5), CancellationToken.None);

		result.ShouldBeFalse();
	}

	[Fact]
	public async Task IsDuplicateAsync_ReturnsTrue_ForProcessedMessage()
	{
		await _deduplicator.MarkProcessedAsync(
			"msg-1", TimeSpan.FromMinutes(5), CancellationToken.None);

		var result = await _deduplicator.IsDuplicateAsync(
			"msg-1", TimeSpan.FromMinutes(5), CancellationToken.None);

		result.ShouldBeTrue();
	}

	[Fact]
	public async Task IsDuplicateAsync_ThrowsOnNullMessageId()
	{
		await Should.ThrowAsync<ArgumentException>(
			() => _deduplicator.IsDuplicateAsync(null!, TimeSpan.FromMinutes(1), CancellationToken.None));
	}

	[Fact]
	public async Task IsDuplicateAsync_ThrowsOnEmptyMessageId()
	{
		await Should.ThrowAsync<ArgumentException>(
			() => _deduplicator.IsDuplicateAsync("", TimeSpan.FromMinutes(1), CancellationToken.None));
	}

	[Fact]
	public async Task IsDuplicateAsync_ThrowsOnZeroExpiry()
	{
		await Should.ThrowAsync<ArgumentOutOfRangeException>(
			() => _deduplicator.IsDuplicateAsync("msg-1", TimeSpan.Zero, CancellationToken.None));
	}

	[Fact]
	public async Task IsDuplicateAsync_ThrowsOnNegativeExpiry()
	{
		await Should.ThrowAsync<ArgumentOutOfRangeException>(
			() => _deduplicator.IsDuplicateAsync("msg-1", TimeSpan.FromSeconds(-1), CancellationToken.None));
	}

	[Fact]
	public async Task MarkProcessedAsync_ThrowsOnNullMessageId()
	{
		await Should.ThrowAsync<ArgumentException>(
			() => _deduplicator.MarkProcessedAsync(null!, TimeSpan.FromMinutes(1), CancellationToken.None));
	}

	[Fact]
	public async Task MarkProcessedAsync_ThrowsOnZeroExpiry()
	{
		await Should.ThrowAsync<ArgumentOutOfRangeException>(
			() => _deduplicator.MarkProcessedAsync("msg-1", TimeSpan.Zero, CancellationToken.None));
	}

	[Fact]
	public async Task GetStatistics_ReturnsTrackedCounts()
	{
		await _deduplicator.MarkProcessedAsync(
			"msg-1", TimeSpan.FromMinutes(5), CancellationToken.None);
		await _deduplicator.MarkProcessedAsync(
			"msg-2", TimeSpan.FromMinutes(5), CancellationToken.None);

		// Check for duplicate
		await _deduplicator.IsDuplicateAsync(
			"msg-1", TimeSpan.FromMinutes(5), CancellationToken.None);

		var stats = _deduplicator.GetStatistics();

		stats.TrackedMessageCount.ShouldBe(2);
		stats.TotalChecks.ShouldBe(1);
		stats.DuplicatesDetected.ShouldBe(1);
		stats.EstimatedMemoryUsageBytes.ShouldBeGreaterThan(0);
		stats.CapturedAt.ShouldNotBe(default);
	}

	[Fact]
	public async Task ClearAsync_RemovesAllEntries()
	{
		await _deduplicator.MarkProcessedAsync(
			"msg-1", TimeSpan.FromMinutes(5), CancellationToken.None);

		await _deduplicator.ClearAsync(CancellationToken.None);

		var stats = _deduplicator.GetStatistics();
		stats.TrackedMessageCount.ShouldBe(0);
	}

	[Fact]
	public async Task CleanupExpiredEntries_RemovesExpired()
	{
		// Mark with very short expiry
		await _deduplicator.MarkProcessedAsync(
			"msg-1", ShortExpiry, CancellationToken.None);

		var removedCount = 0;
		await AwaitCleanupResultAsync(async () =>
		{
			removedCount = await _deduplicator.CleanupExpiredEntriesAsync(CancellationToken.None);
			return removedCount == 1;
		}, TimeSpan.FromSeconds(10));

		removedCount.ShouldBe(1);
	}

	[Fact]
	public async Task MultipleDifferentMessages_TrackIndependently()
	{
		await _deduplicator.MarkProcessedAsync(
			"msg-1", TimeSpan.FromMinutes(5), CancellationToken.None);

		var result1 = await _deduplicator.IsDuplicateAsync(
			"msg-1", TimeSpan.FromMinutes(5), CancellationToken.None);
		var result2 = await _deduplicator.IsDuplicateAsync(
			"msg-2", TimeSpan.FromMinutes(5), CancellationToken.None);

		result1.ShouldBeTrue();
		result2.ShouldBeFalse();
	}

	[Fact]
	public void Dispose_DoesNotThrow()
	{
		var dedup = new InMemoryDeduplicator(
			NullLogger<InMemoryDeduplicator>.Instance);

		Should.NotThrow(() => dedup.Dispose());
	}

	// =====================================================================
	// Sprint 686 T.5 (xsqrv) -- Concurrent deduplication regression tests
	// Validates atomic TryAdd prevents race conditions.
	// =====================================================================

	[Fact]
	public async Task ConcurrentMarkProcessed_DoesNotThrow()
	{
		// Arrange -- multiple threads mark the same message concurrently
		const string messageId = "concurrent-msg-1";
		var expiry = TimeSpan.FromMinutes(5);
		const int concurrency = 50;

		// Act -- all threads try to mark the same message simultaneously
		var tasks = Enumerable.Range(0, concurrency)
			.Select(_ => _deduplicator.MarkProcessedAsync(messageId, expiry, CancellationToken.None))
			.ToArray();

		await Task.WhenAll(tasks).ConfigureAwait(false);

		// Assert -- message should be tracked exactly once
		var isDuplicate = await _deduplicator.IsDuplicateAsync(
			messageId, expiry, CancellationToken.None).ConfigureAwait(false);
		isDuplicate.ShouldBeTrue();

		var stats = _deduplicator.GetStatistics();
		stats.TrackedMessageCount.ShouldBe(1);
	}

	[Fact]
	public async Task ConcurrentIsDuplicate_ReturnsFalseOnlyOnce_ForUnprocessedMessage()
	{
		// Arrange -- multiple threads check + mark the same message
		const string messageId = "race-msg-1";
		var expiry = TimeSpan.FromMinutes(5);
		const int concurrency = 20;

		// Act -- concurrent IsDuplicate checks before any MarkProcessed
		var checkTasks = Enumerable.Range(0, concurrency)
			.Select(_ => _deduplicator.IsDuplicateAsync(messageId, expiry, CancellationToken.None))
			.ToArray();

		var results = await Task.WhenAll(checkTasks).ConfigureAwait(false);

		// Assert -- all should return false since message was never marked
		results.ShouldAllBe(r => r == false);
	}

	[Fact]
	public async Task ConcurrentMarkAndCheck_IsThreadSafe()
	{
		// Arrange -- interleave marks and checks for different messages
		var expiry = TimeSpan.FromMinutes(5);
		const int messageCount = 100;

		// Act -- mark all messages concurrently
		var markTasks = Enumerable.Range(0, messageCount)
			.Select(i => _deduplicator.MarkProcessedAsync($"msg-{i}", expiry, CancellationToken.None))
			.ToArray();
		await Task.WhenAll(markTasks).ConfigureAwait(false);

		// Check all messages concurrently
		var checkTasks = Enumerable.Range(0, messageCount)
			.Select(i => _deduplicator.IsDuplicateAsync($"msg-{i}", expiry, CancellationToken.None))
			.ToArray();
		var results = await Task.WhenAll(checkTasks).ConfigureAwait(false);

		// Assert -- all should be duplicates
		results.ShouldAllBe(r => r == true);
		_deduplicator.GetStatistics().TrackedMessageCount.ShouldBe(messageCount);
	}

	// =====================================================================
	// Sprint 686 T.6 (1hguy) -- Bounded growth regression tests
	// Validates that the deduplicator caps at 10,000 entries.
	// =====================================================================

	[Fact]
	public async Task SkipsDeduplication_WhenAtCapacity()
	{
		// Arrange -- fill to capacity (10,000 entries)
		var expiry = TimeSpan.FromHours(1);
		for (var i = 0; i < 10_000; i++)
		{
			await _deduplicator.MarkProcessedAsync($"fill-{i}", expiry, CancellationToken.None)
				.ConfigureAwait(false);
		}

		var stats = _deduplicator.GetStatistics();
		stats.TrackedMessageCount.ShouldBe(10_000);

		// Act -- try to mark one more
		await _deduplicator.MarkProcessedAsync("overflow-msg", expiry, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert -- count should not exceed capacity
		stats = _deduplicator.GetStatistics();
		stats.TrackedMessageCount.ShouldBe(10_000);
	}

	[Fact]
	public async Task IsDuplicate_ReturnsFalse_WhenAtCapacity_ForNewMessage()
	{
		// Arrange -- fill to capacity
		var expiry = TimeSpan.FromHours(1);
		for (var i = 0; i < 10_000; i++)
		{
			await _deduplicator.MarkProcessedAsync($"fill-{i}", expiry, CancellationToken.None)
				.ConfigureAwait(false);
		}

		// Act -- check a new message when at capacity
		var result = await _deduplicator.IsDuplicateAsync(
			"new-unchecked-msg", expiry, CancellationToken.None).ConfigureAwait(false);

		// Assert -- should return false (not duplicate) since dedup is disabled at capacity
		result.ShouldBeFalse();
	}

	[Fact]
	public async Task IsDuplicate_StillDetectsDuplicates_ForExistingEntries_AtCapacity()
	{
		// Arrange -- fill to capacity with known messages
		var expiry = TimeSpan.FromHours(1);
		for (var i = 0; i < 10_000; i++)
		{
			await _deduplicator.MarkProcessedAsync($"fill-{i}", expiry, CancellationToken.None)
				.ConfigureAwait(false);
		}

		// Act -- check a message that was already tracked
		var result = await _deduplicator.IsDuplicateAsync(
			"fill-500", expiry, CancellationToken.None).ConfigureAwait(false);

		// Assert -- existing entries should still be found as duplicates
		result.ShouldBeTrue();
	}

	public void Dispose()
	{
		_deduplicator.Dispose();
	}

	private static async Task AwaitCleanupResultAsync(Func<Task<bool>> condition, TimeSpan timeout)
	{
		var scaledTimeout = TestTimeouts.Scale(timeout);
		var conditionMet = await WaitHelpers.WaitUntilAsync(
				condition,
				scaledTimeout,
				TimeSpan.FromMilliseconds(100))
			.ConfigureAwait(false);
		conditionMet.ShouldBeTrue($"Condition was not met within {scaledTimeout}.");
	}
}
