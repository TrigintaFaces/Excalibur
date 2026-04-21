// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Options.Delivery;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Tests.Messaging.Delivery;

/// <summary>
///     Tests for the <see cref="InMemoryDeduplicator" /> class.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Dispatch.Core")]
public sealed class InMemoryDeduplicatorShould : IDisposable
{
	private static readonly TimeSpan ShortExpiry = TimeSpan.FromMilliseconds(100);
	private static readonly TimeSpan ExpirationTimeout = TimeSpan.FromSeconds(30);

	private readonly InMemoryDeduplicator _sut;

	public InMemoryDeduplicatorShould()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new InMemoryDeduplicatorOptions
		{
			EnableAutomaticCleanup = false,
			CleanupInterval = TimeSpan.FromHours(1),
		});
		_sut = new InMemoryDeduplicator(
			options,
			NullLogger<InMemoryDeduplicator>.Instance);
	}

	[Fact]
	public void ThrowArgumentNullExceptionForNullOptions() =>
		Should.Throw<ArgumentNullException>(() => new InMemoryDeduplicator(
			null!, NullLogger<InMemoryDeduplicator>.Instance));

	[Fact]
	public void ThrowArgumentNullExceptionForNullLogger() =>
		Should.Throw<ArgumentNullException>(() => new InMemoryDeduplicator(
			Microsoft.Extensions.Options.Options.Create(new InMemoryDeduplicatorOptions()), null!));

	[Fact]
	public async Task ReturnFalseForNewMessage()
	{
		var result = await _sut.IsDuplicateAsync("msg-1", TimeSpan.FromMinutes(5), CancellationToken.None).ConfigureAwait(false);
		result.ShouldBeFalse();
	}

	[Fact]
	public async Task ReturnTrueForDuplicateMessage()
	{
		await _sut.MarkProcessedAsync("msg-1", TimeSpan.FromMinutes(5), CancellationToken.None).ConfigureAwait(false);
		var result = await _sut.IsDuplicateAsync("msg-1", TimeSpan.FromMinutes(5), CancellationToken.None).ConfigureAwait(false);
		result.ShouldBeTrue();
	}

	[Fact]
	public async Task ReturnFalseForExpiredMessage()
	{
		await _sut.MarkProcessedAsync("msg-1", ShortExpiry, CancellationToken.None).ConfigureAwait(false);

		var expired = await global::Tests.Shared.Infrastructure.WaitHelpers.WaitUntilAsync(
				async () => !await _sut.IsDuplicateAsync("msg-1", TimeSpan.FromMinutes(5), CancellationToken.None).ConfigureAwait(false),
				global::Tests.Shared.Infrastructure.TestTimeouts.Scale(ExpirationTimeout),
				TimeSpan.FromMilliseconds(100))
			.ConfigureAwait(false);
		expired.ShouldBeTrue("Expected deduplication entry to expire within timeout.");

		var result = await _sut.IsDuplicateAsync("msg-1", TimeSpan.FromMinutes(5), CancellationToken.None).ConfigureAwait(false);
		result.ShouldBeFalse();
	}

	[Fact]
	public async Task ThrowForNullOrWhiteSpaceMessageIdOnIsDuplicate()
	{
		await Should.ThrowAsync<ArgumentException>(() =>
			_sut.IsDuplicateAsync(null!, TimeSpan.FromMinutes(1), CancellationToken.None)).ConfigureAwait(false);
		await Should.ThrowAsync<ArgumentException>(() =>
			_sut.IsDuplicateAsync("  ", TimeSpan.FromMinutes(1), CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task ThrowForZeroOrNegativeExpiryOnIsDuplicate() =>
		await Should.ThrowAsync<ArgumentOutOfRangeException>(() =>
			_sut.IsDuplicateAsync("msg-1", TimeSpan.Zero, CancellationToken.None)).ConfigureAwait(false);

	[Fact]
	public async Task ThrowForNullOrWhiteSpaceMessageIdOnMarkProcessed()
	{
		await Should.ThrowAsync<ArgumentException>(() =>
			_sut.MarkProcessedAsync(null!, TimeSpan.FromMinutes(1), CancellationToken.None)).ConfigureAwait(false);
		await Should.ThrowAsync<ArgumentException>(() =>
			_sut.MarkProcessedAsync("", TimeSpan.FromMinutes(1), CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task ThrowForZeroOrNegativeExpiryOnMarkProcessed() =>
		await Should.ThrowAsync<ArgumentOutOfRangeException>(() =>
			_sut.MarkProcessedAsync("msg-1", TimeSpan.Zero, CancellationToken.None)).ConfigureAwait(false);

	[Fact]
	public async Task CleanupExpiredEntries()
	{
		await _sut.MarkProcessedAsync("msg-1", ShortExpiry, CancellationToken.None).ConfigureAwait(false);
		await _sut.MarkProcessedAsync("msg-2", ShortExpiry, CancellationToken.None).ConfigureAwait(false);
		await _sut.MarkProcessedAsync("msg-3", TimeSpan.FromHours(1), CancellationToken.None).ConfigureAwait(false);

		var removed = 0;
		var totalRemoved = 0;
		var cleaned = await global::Tests.Shared.Infrastructure.WaitHelpers.WaitUntilAsync(
				async () =>
				{
					removed = await _sut.CleanupExpiredEntriesAsync(CancellationToken.None).ConfigureAwait(false);
					totalRemoved += removed;
					return totalRemoved >= 2;
				},
				global::Tests.Shared.Infrastructure.TestTimeouts.Scale(ExpirationTimeout),
				TimeSpan.FromMilliseconds(100))
			.ConfigureAwait(false);
		cleaned.ShouldBeTrue("Expected cleanup to remove expired entries within timeout.");
		totalRemoved.ShouldBeGreaterThanOrEqualTo(2);
	}

	[Fact]
	public async Task ReturnStatistics()
	{
		await _sut.MarkProcessedAsync("msg-1", TimeSpan.FromMinutes(5), CancellationToken.None).ConfigureAwait(false);
		await _sut.IsDuplicateAsync("msg-1", TimeSpan.FromMinutes(5), CancellationToken.None).ConfigureAwait(false);
		await _sut.IsDuplicateAsync("msg-2", TimeSpan.FromMinutes(5), CancellationToken.None).ConfigureAwait(false);

		var stats = _sut.GetStatistics();
		stats.TrackedMessageCount.ShouldBe(1);
		stats.TotalChecks.ShouldBe(2);
		stats.DuplicatesDetected.ShouldBe(1);
		stats.EstimatedMemoryUsageBytes.ShouldBeGreaterThan(0);
	}

	[Fact]
	public async Task ClearAllEntries()
	{
		await _sut.MarkProcessedAsync("msg-1", TimeSpan.FromMinutes(5), CancellationToken.None).ConfigureAwait(false);
		await _sut.MarkProcessedAsync("msg-2", TimeSpan.FromMinutes(5), CancellationToken.None).ConfigureAwait(false);

		await _sut.ClearAsync(CancellationToken.None).ConfigureAwait(false);

		var stats = _sut.GetStatistics();
		stats.TrackedMessageCount.ShouldBe(0);
	}

	[Fact]
	public void DisposeMultipleTimesSafely()
	{
		var opts = Microsoft.Extensions.Options.Options.Create(new InMemoryDeduplicatorOptions());
		var instance = new InMemoryDeduplicator(opts, NullLogger<InMemoryDeduplicator>.Instance);
		Should.NotThrow(() =>
		{
			instance.Dispose();
			instance.Dispose();
		});
	}

	[Fact]
	public async Task HandleConcurrentMarksCorrectly()
	{
		var tasks = Enumerable.Range(0, 100)
			.Select(i => _sut.MarkProcessedAsync($"msg-{i}", TimeSpan.FromMinutes(5), CancellationToken.None));

		await Task.WhenAll(tasks).ConfigureAwait(false);

		var stats = _sut.GetStatistics();
		stats.TrackedMessageCount.ShouldBe(100);
	}

	public void Dispose() => _sut.Dispose();
}
