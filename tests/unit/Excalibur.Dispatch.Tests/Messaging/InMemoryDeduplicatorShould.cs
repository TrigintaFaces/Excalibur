// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Delivery;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Tests.Messaging;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class InMemoryDeduplicatorShould : IDisposable
{
	private readonly InMemoryDeduplicator _deduplicator;

	public InMemoryDeduplicatorShould()
	{
		_deduplicator = new InMemoryDeduplicator(
			NullLogger<InMemoryDeduplicator>.Instance,
			TimeSpan.FromMinutes(30));
	}

	public void Dispose()
	{
		_deduplicator.Dispose();
	}

	[Fact]
	public async Task ReturnFalseForNewMessage()
	{
		// Act
		var isDuplicate = await _deduplicator.IsDuplicateAsync("msg-1", TimeSpan.FromHours(1), CancellationToken.None);

		// Assert
		isDuplicate.ShouldBeFalse();
	}

	[Fact]
	public async Task ReturnTrueForDuplicateMessage()
	{
		// Arrange
		await _deduplicator.MarkProcessedAsync("msg-1", TimeSpan.FromHours(1), CancellationToken.None);

		// Act
		var isDuplicate = await _deduplicator.IsDuplicateAsync("msg-1", TimeSpan.FromHours(1), CancellationToken.None);

		// Assert
		isDuplicate.ShouldBeTrue();
	}

	[Fact]
	public async Task ReturnFalseForExpiredMessage()
	{
		// Arrange - use very short expiry
		await _deduplicator.MarkProcessedAsync("msg-1", TimeSpan.FromMilliseconds(1), CancellationToken.None);
		await WaitUntilNotDuplicateAsync("msg-1", TimeSpan.FromSeconds(5));

		// Act
		var isDuplicate = await _deduplicator.IsDuplicateAsync("msg-1", TimeSpan.FromHours(1), CancellationToken.None);

		// Assert
		isDuplicate.ShouldBeFalse();
	}

	[Fact]
	public async Task ThrowWhenMessageIdIsNullOrWhitespace()
	{
		// Act & Assert
		await Should.ThrowAsync<ArgumentException>(
			() => _deduplicator.IsDuplicateAsync(null!, TimeSpan.FromHours(1), CancellationToken.None));
		await Should.ThrowAsync<ArgumentException>(
			() => _deduplicator.IsDuplicateAsync("", TimeSpan.FromHours(1), CancellationToken.None));
		await Should.ThrowAsync<ArgumentException>(
			() => _deduplicator.IsDuplicateAsync("   ", TimeSpan.FromHours(1), CancellationToken.None));
	}

	[Fact]
	public async Task ThrowWhenExpiryIsZeroOrNegative()
	{
		// Act & Assert
		await Should.ThrowAsync<ArgumentOutOfRangeException>(
			() => _deduplicator.IsDuplicateAsync("msg-1", TimeSpan.Zero, CancellationToken.None));
		await Should.ThrowAsync<ArgumentOutOfRangeException>(
			() => _deduplicator.IsDuplicateAsync("msg-1", TimeSpan.FromSeconds(-1), CancellationToken.None));
	}

	[Fact]
	public async Task MarkProcessedThrowsForNullMessageId()
	{
		// Act & Assert
		await Should.ThrowAsync<ArgumentException>(
			() => _deduplicator.MarkProcessedAsync(null!, TimeSpan.FromHours(1), CancellationToken.None));
	}

	[Fact]
	public async Task MarkProcessedThrowsForZeroExpiry()
	{
		// Act & Assert
		await Should.ThrowAsync<ArgumentOutOfRangeException>(
			() => _deduplicator.MarkProcessedAsync("msg-1", TimeSpan.Zero, CancellationToken.None));
	}

	[Fact]
	public async Task MarkProcessedExtendsExpiryOnUpdate()
	{
		// Arrange
		await _deduplicator.MarkProcessedAsync("msg-1", TimeSpan.FromMilliseconds(50), CancellationToken.None);

		// Act - mark with longer expiry
		await _deduplicator.MarkProcessedAsync("msg-1", TimeSpan.FromHours(1), CancellationToken.None);

		// Assert - should still be a duplicate (extended expiry)
		var isDuplicate = await _deduplicator.IsDuplicateAsync("msg-1", TimeSpan.FromHours(1), CancellationToken.None);
		isDuplicate.ShouldBeTrue();
	}

	[Fact]
	public async Task CleanupExpiredEntriesRemovesExpiredOnly()
	{
		// Arrange
		await _deduplicator.MarkProcessedAsync("expired-1", TimeSpan.FromMilliseconds(1), CancellationToken.None);
		await _deduplicator.MarkProcessedAsync("active-1", TimeSpan.FromHours(1), CancellationToken.None);
		await Task.Delay(100).ConfigureAwait(false);

		// Act
		var removed = await _deduplicator.CleanupExpiredEntriesAsync(CancellationToken.None);

		// Assert
		removed.ShouldBeGreaterThanOrEqualTo(1);
		// Active message should still be detected as duplicate
		var isDuplicate = await _deduplicator.IsDuplicateAsync("active-1", TimeSpan.FromHours(1), CancellationToken.None);
		isDuplicate.ShouldBeTrue();
	}

	[Fact]
	public async Task GetStatisticsReturnsAccurateData()
	{
		// Arrange
		await _deduplicator.MarkProcessedAsync("msg-1", TimeSpan.FromHours(1), CancellationToken.None);
		await _deduplicator.MarkProcessedAsync("msg-2", TimeSpan.FromHours(1), CancellationToken.None);
		_ = await _deduplicator.IsDuplicateAsync("msg-1", TimeSpan.FromHours(1), CancellationToken.None);
		_ = await _deduplicator.IsDuplicateAsync("msg-3", TimeSpan.FromHours(1), CancellationToken.None);

		// Act
		var stats = _deduplicator.GetStatistics();

		// Assert
		stats.TrackedMessageCount.ShouldBe(2);
		stats.TotalChecks.ShouldBeGreaterThanOrEqualTo(2);
		stats.DuplicatesDetected.ShouldBeGreaterThanOrEqualTo(1);
		stats.EstimatedMemoryUsageBytes.ShouldBeGreaterThan(0);
		stats.CapturedAt.ShouldBeGreaterThan(DateTimeOffset.UtcNow.AddMinutes(-1));
	}

	[Fact]
	public async Task ClearAsyncRemovesAllEntries()
	{
		// Arrange
		await _deduplicator.MarkProcessedAsync("msg-1", TimeSpan.FromHours(1), CancellationToken.None);
		await _deduplicator.MarkProcessedAsync("msg-2", TimeSpan.FromHours(1), CancellationToken.None);

		// Act
		await _deduplicator.ClearAsync();

		// Assert
		var stats = _deduplicator.GetStatistics();
		stats.TrackedMessageCount.ShouldBe(0);
	}

	[Fact]
	public void ThrowWhenLoggerIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => new InMemoryDeduplicator(null!));
	}

	[Fact]
	public void UseDefaultCleanupIntervalWhenNoneProvided()
	{
		// Act & Assert - should not throw
		using var dedup = new InMemoryDeduplicator(NullLogger<InMemoryDeduplicator>.Instance);
		dedup.ShouldNotBeNull();
	}

	[Fact]
	public async Task HandleConcurrentDuplicateChecks()
	{
		// Arrange
		await _deduplicator.MarkProcessedAsync("concurrent-msg", TimeSpan.FromHours(1), CancellationToken.None);

		// Act - multiple concurrent checks
		var tasks = Enumerable.Range(0, 10)
			.Select(_ => _deduplicator.IsDuplicateAsync("concurrent-msg", TimeSpan.FromHours(1), CancellationToken.None))
			.ToArray();
		var results = await Task.WhenAll(tasks);

		// Assert - all should detect as duplicate
		results.ShouldAllBe(r => r);
	}

	private async Task WaitUntilNotDuplicateAsync(string messageId, TimeSpan timeout)
	{
		var startedAt = DateTimeOffset.UtcNow;
		while ((DateTimeOffset.UtcNow - startedAt) < timeout)
		{
			var isDuplicate = await _deduplicator
				.IsDuplicateAsync(messageId, TimeSpan.FromHours(1), CancellationToken.None)
				.ConfigureAwait(false);

			if (!isDuplicate)
			{
				return;
			}

			await Task.Delay(10).ConfigureAwait(false);
		}

		throw new TimeoutException($"Message '{messageId}' did not expire within {timeout}.");
	}
}
