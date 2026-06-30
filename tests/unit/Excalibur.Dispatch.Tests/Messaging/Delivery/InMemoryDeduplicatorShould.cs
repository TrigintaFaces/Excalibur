using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Options.Delivery;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Tests.Shared.Infrastructure;

namespace Excalibur.Dispatch.Tests.Messaging.Delivery;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class InMemoryDeduplicatorShould : IDisposable
{
	private static readonly TimeSpan ShortExpiry = TimeSpan.FromMilliseconds(100);

	private readonly InMemoryDeduplicator _deduplicator;

	public InMemoryDeduplicatorShould()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new InMemoryDeduplicatorOptions
		{
			EnableAutomaticCleanup = false, // Disable timer to avoid interference in tests
			CleanupInterval = TimeSpan.FromHours(1),
		});
		_deduplicator = new InMemoryDeduplicator(
			options,
			NullLogger<InMemoryDeduplicator>.Instance);
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
		var options = Microsoft.Extensions.Options.Options.Create(new InMemoryDeduplicatorOptions());
		var dedup = new InMemoryDeduplicator(
			options,
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

	// =====================================================================
	// bd-t33cz2 (S859) — fail-closed capacity contract. The prior Sprint-686 hardcoded 10k silent-skip
	// cap was replaced by a configurable MaxEntries (default 100k) + FAIL-CLOSED semantics: record-producing
	// ops throw a transient DeduplicationCapacityExceededException (never silently drop a message), and the
	// claim path denies. These tests assert the new contract against an explicit small bound.
	// =====================================================================

	private const int BoundedCapacity = 3;

	private static InMemoryDeduplicator CreateBounded(int maxEntries) =>
		new(
			Microsoft.Extensions.Options.Options.Create(new InMemoryDeduplicatorOptions
			{
				EnableAutomaticCleanup = false,
				CleanupInterval = TimeSpan.FromHours(1),
				MaxEntries = maxEntries,
			}),
			NullLogger<InMemoryDeduplicator>.Instance);

	private static async Task FillToCapacityAsync(InMemoryDeduplicator dedup, int count, TimeSpan expiry)
	{
		for (var i = 0; i < count; i++)
		{
			await dedup.MarkProcessedAsync($"fill-{i}", expiry, CancellationToken.None).ConfigureAwait(false);
		}
	}

	[Fact]
	public async Task FailClosed_OnMarkProcessed_WhenAtCapacity()
	{
		// Arrange -- filled exactly to the configured bound.
		using var dedup = CreateBounded(BoundedCapacity);
		var expiry = TimeSpan.FromHours(1);
		await FillToCapacityAsync(dedup, BoundedCapacity, expiry).ConfigureAwait(false);
		dedup.GetStatistics().TrackedMessageCount.ShouldBe(BoundedCapacity);

		// Act / Assert -- at capacity a record-producing op fails CLOSED (transient throw), never a silent
		// skip that would drop the message; the tracked count must not grow past the bound.
		_ = await Should.ThrowAsync<DeduplicationCapacityExceededException>(async () =>
			await dedup.MarkProcessedAsync("overflow-msg", expiry, CancellationToken.None).ConfigureAwait(false));
		dedup.GetStatistics().TrackedMessageCount.ShouldBe(BoundedCapacity);
	}

	[Fact]
	public async Task FailClosed_OnIsDuplicate_ForNewMessage_WhenAtCapacity()
	{
		// Arrange
		using var dedup = CreateBounded(BoundedCapacity);
		var expiry = TimeSpan.FromHours(1);
		await FillToCapacityAsync(dedup, BoundedCapacity, expiry).ConfigureAwait(false);

		// Act / Assert -- checking an UNTRACKED id at capacity cannot prove non-duplicate without admitting
		// it, so it fails closed (throws) rather than returning false and silently dropping a real message.
		_ = await Should.ThrowAsync<DeduplicationCapacityExceededException>(async () =>
			await dedup.IsDuplicateAsync("new-unchecked-msg", expiry, CancellationToken.None).ConfigureAwait(false));
	}

	[Fact]
	public async Task TryClaim_FailsClosed_WhenAtCapacity()
	{
		// Arrange
		using var dedup = CreateBounded(BoundedCapacity);
		var expiry = TimeSpan.FromHours(1);
		await FillToCapacityAsync(dedup, BoundedCapacity, expiry).ConfigureAwait(false);

		// Act -- the claim path denies (fails closed) at capacity rather than admitting an untracked claim.
		var claimed = await dedup.TryClaimAsync("claim-overflow", expiry, CancellationToken.None).ConfigureAwait(false);

		// Assert
		claimed.ShouldBeFalse();
	}

	[Fact]
	public async Task IsDuplicate_StillDetectsDuplicates_ForExistingEntries_AtCapacity()
	{
		// Arrange -- filled exactly to capacity with known ids.
		using var dedup = CreateBounded(BoundedCapacity);
		var expiry = TimeSpan.FromHours(1);
		await FillToCapacityAsync(dedup, BoundedCapacity, expiry).ConfigureAwait(false);

		// Act -- an ALREADY-TRACKED id is matched before the capacity guard, so it returns true (no throw):
		// fail-closed must not break dedup of entries that ARE tracked.
		var result = await dedup.IsDuplicateAsync("fill-1", expiry, CancellationToken.None).ConfigureAwait(false);

		// Assert
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
