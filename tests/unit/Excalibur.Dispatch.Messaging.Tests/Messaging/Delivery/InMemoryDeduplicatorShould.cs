// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Delivery;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Tests.Messaging.Delivery;

/// <summary>
///     Tests for the <see cref="InMemoryDeduplicator" /> class.
/// </summary>
[Trait("Category", "Unit")]
public sealed class InMemoryDeduplicatorShould : IDisposable
{
	private readonly InMemoryDeduplicator _sut;

	public InMemoryDeduplicatorShould()
	{
		_sut = new InMemoryDeduplicator(
			NullLogger<InMemoryDeduplicator>.Instance,
			TimeSpan.FromHours(1)); // Long cleanup interval to avoid interference
	}

	[Fact]
	public void ThrowArgumentNullExceptionForNullLogger() =>
		Should.Throw<ArgumentNullException>(() => new InMemoryDeduplicator(null!));

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
		await _sut.MarkProcessedAsync("msg-1", TimeSpan.FromMilliseconds(1), CancellationToken.None).ConfigureAwait(false);
		await global::Tests.Shared.Infrastructure.TestTiming.DelayAsync(50).ConfigureAwait(false);
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
		await _sut.MarkProcessedAsync("msg-1", TimeSpan.FromMilliseconds(1), CancellationToken.None).ConfigureAwait(false);
		await _sut.MarkProcessedAsync("msg-2", TimeSpan.FromMilliseconds(1), CancellationToken.None).ConfigureAwait(false);
		await _sut.MarkProcessedAsync("msg-3", TimeSpan.FromHours(1), CancellationToken.None).ConfigureAwait(false);
		await global::Tests.Shared.Infrastructure.TestTiming.DelayAsync(50).ConfigureAwait(false);

		var removed = await _sut.CleanupExpiredEntriesAsync(CancellationToken.None).ConfigureAwait(false);
		removed.ShouldBe(2);
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

		await _sut.ClearAsync().ConfigureAwait(false);

		var stats = _sut.GetStatistics();
		stats.TrackedMessageCount.ShouldBe(0);
	}

	[Fact]
	public void DisposeMultipleTimesSafely()
	{
		var instance = new InMemoryDeduplicator(NullLogger<InMemoryDeduplicator>.Instance);
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
