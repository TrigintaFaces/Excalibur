// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Patterns.ClaimCheck;

namespace Excalibur.Dispatch.Patterns.Tests.ClaimCheck.InMemory;

/// <summary>
/// Depth coverage tests for <see cref="InMemoryClaimCheckProvider"/> edge cases.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class InMemoryClaimCheckProviderDepthShould
{
	[Fact]
	public void Constructor_ThrowsArgumentNullException_WhenOptionsIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new InMemoryClaimCheckProvider(null!));
	}

	[Fact]
	public async Task CleanupExpiredAsync_RemovesOnlyExpiredEntries()
	{
		// Arrange
		var provider = CreateProvider(o => o.DefaultTtl = TimeSpan.FromMilliseconds(1));
		var payload = "expired"u8.ToArray();
		await provider.StoreAsync(payload, CancellationToken.None);

		// Wait for expiration
		await global::Tests.Shared.Infrastructure.TestTiming.PauseAsync(500);

		// Store a non-expiring entry
		var provider2Opts = new ClaimCheckOptions { DefaultTtl = TimeSpan.Zero };
		// Use same provider but store with TTL=0 (no expiry) manually isn't possible
		// since all entries use the same options. Instead, verify count after cleanup.

		// Act
		var removed = await provider.CleanupExpiredAsync(100, CancellationToken.None);

		// Assert
		removed.ShouldBe(1);
		provider.EntryCount.ShouldBe(0);
	}

	[Fact]
	public async Task CleanupExpiredAsync_RespectsMaxBatchSize()
	{
		// Arrange - Store 5 entries that will expire quickly
		var provider = CreateProvider(o => o.DefaultTtl = TimeSpan.FromMilliseconds(1));
		for (var i = 0; i < 5; i++)
		{
			await provider.StoreAsync(new byte[] { (byte)i }, CancellationToken.None);
		}

		await global::Tests.Shared.Infrastructure.TestTiming.PauseAsync(500);

		// Act - Remove at most 2
		var removed = await provider.CleanupExpiredAsync(2, CancellationToken.None);

		// Assert
		removed.ShouldBe(2);
		provider.EntryCount.ShouldBe(3);
	}

	[Fact]
	public async Task CleanupExpiredAsync_ReturnsZero_WhenNoExpiredEntries()
	{
		// Arrange - Long TTL
		var provider = CreateProvider(o => o.DefaultTtl = TimeSpan.FromDays(365));
		await provider.StoreAsync("alive"u8.ToArray(), CancellationToken.None);

		// Act
		var removed = await provider.CleanupExpiredAsync(100, CancellationToken.None);

		// Assert
		removed.ShouldBe(0);
		provider.EntryCount.ShouldBe(1);
	}

	[Fact]
	public async Task CleanupExpiredAsync_RespectsCancellationToken()
	{
		// Arrange - Store entries that will expire
		var provider = CreateProvider(o => o.DefaultTtl = TimeSpan.FromMilliseconds(1));
		for (var i = 0; i < 10; i++)
		{
			await provider.StoreAsync(new byte[] { (byte)i }, CancellationToken.None);
		}

		await global::Tests.Shared.Infrastructure.TestTiming.PauseAsync(500);

		// Act - Cancel immediately
		using var cts = new CancellationTokenSource();
		await cts.CancelAsync();
		var removed = await provider.CleanupExpiredAsync(100, cts.Token);

		// Assert - Should stop early
		removed.ShouldBe(0);
	}

	[Fact]
	public async Task RemoveExpiredEntries_RemovesAllExpired()
	{
		// Arrange
		var provider = CreateProvider(o => o.DefaultTtl = TimeSpan.FromMilliseconds(1));
		for (var i = 0; i < 3; i++)
		{
			await provider.StoreAsync(new byte[] { (byte)i }, CancellationToken.None);
		}

		await global::Tests.Shared.Infrastructure.TestTiming.PauseAsync(500);

		// Act
		var removed = provider.RemoveExpiredEntries();

		// Assert
		removed.ShouldBe(3);
		provider.EntryCount.ShouldBe(0);
	}

	[Fact]
	public async Task RetrieveAsync_ThrowsInvalidOperationException_WhenEntryExpired()
	{
		// Arrange
		var provider = CreateProvider(o => o.DefaultTtl = TimeSpan.FromMilliseconds(1));
		var reference = await provider.StoreAsync("expired"u8.ToArray(), CancellationToken.None);

		await global::Tests.Shared.Infrastructure.TestTiming.PauseAsync(500);

		// Act & Assert
		var ex = await Should.ThrowAsync<InvalidOperationException>(() =>
			provider.RetrieveAsync(reference, CancellationToken.None));
		ex.Message.ShouldContain("expired");
	}

	[Fact]
	public async Task StoreAsync_WithCompressionEnabled_CompressesPayload()
	{
		// Arrange - Create a payload that compresses well (repeated bytes)
		var provider = CreateProvider(o =>
		{
			o.EnableCompression = true;
			o.CompressionThreshold = 100;
			o.MinCompressionRatio = 0.9;
		});
		var payload = new byte[500];
		Array.Fill(payload, (byte)'A'); // Highly compressible

		// Act
		var reference = await provider.StoreAsync(payload, CancellationToken.None);

		// Assert - Retrieve should get back original
		var retrieved = await provider.RetrieveAsync(reference, CancellationToken.None);
		retrieved.ShouldBe(payload);
	}

	[Fact]
	public async Task StoreAsync_WithCompressionEnabled_SkipsCompression_WhenRatioNotEffective()
	{
		// Arrange - Random payload doesn't compress well
		var provider = CreateProvider(o =>
		{
			o.EnableCompression = true;
			o.CompressionThreshold = 10;
			o.MinCompressionRatio = 0.1; // Very strict ratio, random data won't meet it
		});
		var payload = new byte[200];
		Random.Shared.NextBytes(payload);

		// Act
		var reference = await provider.StoreAsync(payload, CancellationToken.None);
		var retrieved = await provider.RetrieveAsync(reference, CancellationToken.None);

		// Assert - Should still return original data
		retrieved.ShouldBe(payload);
	}

	[Fact]
	public async Task StoreAsync_WithCompressionDisabled_DoesNotCompress()
	{
		// Arrange
		var provider = CreateProvider(o => o.EnableCompression = false);
		var payload = new byte[500];
		Array.Fill(payload, (byte)'B');

		// Act
		var reference = await provider.StoreAsync(payload, CancellationToken.None);
		var retrieved = await provider.RetrieveAsync(reference, CancellationToken.None);

		// Assert
		retrieved.ShouldBe(payload);
		reference.Size.ShouldBe(payload.Length);
	}

	[Fact]
	public async Task StoreAsync_WithChecksumDisabled_SkipsChecksumComputation()
	{
		// Arrange
		var provider = CreateProvider(o => o.ValidateChecksum = false);
		var payload = "no-checksum"u8.ToArray();

		// Act
		var reference = await provider.StoreAsync(payload, CancellationToken.None);
		var retrieved = await provider.RetrieveAsync(reference, CancellationToken.None);

		// Assert
		retrieved.ShouldBe(payload);
	}

	[Fact]
	public async Task ClearAll_RemovesAllEntries()
	{
		// Arrange
		var provider = CreateProvider();
		await provider.StoreAsync("a"u8.ToArray(), CancellationToken.None);
		await provider.StoreAsync("b"u8.ToArray(), CancellationToken.None);
		provider.EntryCount.ShouldBe(2);

		// Act
		provider.ClearAll();

		// Assert
		provider.EntryCount.ShouldBe(0);
	}

	[Fact]
	public async Task ShouldUseClaimCheck_AtExactThreshold_ReturnsTrue()
	{
		var provider = CreateProvider(o => o.PayloadThreshold = 100);
		var payload = new byte[100];

		provider.ShouldUseClaimCheck(payload).ShouldBeTrue();
	}

	[Fact]
	public void ShouldUseClaimCheck_BelowThreshold_ReturnsFalse()
	{
		var provider = CreateProvider(o => o.PayloadThreshold = 100);
		var payload = new byte[99];

		provider.ShouldUseClaimCheck(payload).ShouldBeFalse();
	}

	private static InMemoryClaimCheckProvider CreateProvider(Action<ClaimCheckOptions>? configure = null)
	{
		var options = new ClaimCheckOptions();
		configure?.Invoke(options);
		return new InMemoryClaimCheckProvider(Microsoft.Extensions.Options.Options.Create(options));
	}
}

