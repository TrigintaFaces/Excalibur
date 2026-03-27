// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Options.Delivery;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Tests.Messaging;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class InMemoryDeduplicatorShould : IDisposable
{
	private static readonly TimeSpan ShortExpiry = TimeSpan.FromMilliseconds(100);

	private readonly InMemoryDeduplicator _deduplicator;

	public InMemoryDeduplicatorShould()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new InMemoryDeduplicatorOptions
		{
			EnableAutomaticCleanup = false, // Disable timer to avoid interference in tests
			CleanupInterval = TimeSpan.FromMinutes(30),
		});
		_deduplicator = new InMemoryDeduplicator(
			options,
			NullLogger<InMemoryDeduplicator>.Instance);
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
		await _deduplicator.MarkProcessedAsync("msg-1", ShortExpiry, CancellationToken.None);
		var expirationObserved = await global::Tests.Shared.Infrastructure.WaitHelpers.WaitUntilAsync(
				async () => !await _deduplicator.IsDuplicateAsync("msg-1", TimeSpan.FromHours(1), CancellationToken.None).ConfigureAwait(false),
				global::Tests.Shared.Infrastructure.TestTimeouts.Scale(TimeSpan.FromSeconds(20)),
				TimeSpan.FromMilliseconds(50))
			.ConfigureAwait(false);
		expirationObserved.ShouldBeTrue("Expected deduplication entry to expire within timeout.");

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
		await _deduplicator.MarkProcessedAsync("expired-1", ShortExpiry, CancellationToken.None);
		await _deduplicator.MarkProcessedAsync("active-1", TimeSpan.FromHours(1), CancellationToken.None);
		await global::Tests.Shared.Infrastructure.TestTiming.PauseAsync(
			global::Tests.Shared.Infrastructure.TestTimeouts.Scale(TimeSpan.FromMilliseconds(500)))
			.ConfigureAwait(false);

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
		var lowerBound = DateTimeOffset.UtcNow;

		// Act
		var stats = _deduplicator.GetStatistics();
		var upperBound = DateTimeOffset.UtcNow;

		// Assert
		stats.TrackedMessageCount.ShouldBe(2);
		stats.TotalChecks.ShouldBeGreaterThanOrEqualTo(2);
		stats.DuplicatesDetected.ShouldBeGreaterThanOrEqualTo(1);
		stats.EstimatedMemoryUsageBytes.ShouldBeGreaterThan(0);
		stats.CapturedAt.ShouldBeGreaterThanOrEqualTo(lowerBound);
		stats.CapturedAt.ShouldBeLessThanOrEqualTo(upperBound);
	}

	[Fact]
	public async Task ClearAsyncRemovesAllEntries()
	{
		// Arrange
		await _deduplicator.MarkProcessedAsync("msg-1", TimeSpan.FromHours(1), CancellationToken.None);
		await _deduplicator.MarkProcessedAsync("msg-2", TimeSpan.FromHours(1), CancellationToken.None);

		// Act
		await _deduplicator.ClearAsync(CancellationToken.None);

		// Assert
		var stats = _deduplicator.GetStatistics();
		stats.TrackedMessageCount.ShouldBe(0);
	}

	[Fact]
	public void ThrowWhenOptionsIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => new InMemoryDeduplicator(
			null!, NullLogger<InMemoryDeduplicator>.Instance));
	}

	[Fact]
	public void ThrowWhenLoggerIsNull()
	{
		// Act & Assert
		var options = Microsoft.Extensions.Options.Options.Create(new InMemoryDeduplicatorOptions());
		Should.Throw<ArgumentNullException>(() => new InMemoryDeduplicator(options, null!));
	}

	[Fact]
	public void UseDefaultOptionsWhenNoneCustomized()
	{
		// Act & Assert - should not throw
		var options = Microsoft.Extensions.Options.Options.Create(new InMemoryDeduplicatorOptions());
		using var dedup = new InMemoryDeduplicator(options, NullLogger<InMemoryDeduplicator>.Instance);
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
}
