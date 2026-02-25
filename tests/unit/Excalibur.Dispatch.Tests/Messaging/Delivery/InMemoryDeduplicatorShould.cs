using Excalibur.Dispatch.Delivery;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Tests.Messaging.Delivery;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class InMemoryDeduplicatorShould : IDisposable
{
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

		await _deduplicator.ClearAsync();

		var stats = _deduplicator.GetStatistics();
		stats.TrackedMessageCount.ShouldBe(0);
	}

	[Fact]
	public async Task CleanupExpiredEntries_RemovesExpired()
	{
		// Mark with very short expiry
		await _deduplicator.MarkProcessedAsync(
			"msg-1", TimeSpan.FromMilliseconds(1), CancellationToken.None);

		var removedCount = 0;
		await WaitUntilAsync(async () =>
		{
			removedCount = await _deduplicator.CleanupExpiredEntriesAsync(CancellationToken.None);
			return removedCount == 1;
		}, TimeSpan.FromSeconds(2));

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

	public void Dispose()
	{
		_deduplicator.Dispose();
	}

	private static async Task WaitUntilAsync(Func<Task<bool>> condition, TimeSpan timeout)
	{
		var deadline = DateTimeOffset.UtcNow + timeout;
		while (DateTimeOffset.UtcNow < deadline)
		{
			if (await condition())
			{
				return;
			}

			await global::Tests.Shared.Infrastructure.TestTiming.PauseAsync(10);
		}

		throw new TimeoutException($"Condition was not met within {timeout}.");
	}
}

