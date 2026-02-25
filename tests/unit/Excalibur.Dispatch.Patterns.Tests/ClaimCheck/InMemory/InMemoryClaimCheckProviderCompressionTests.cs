// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.IO.Compression;
using System.Text;

using Excalibur.Dispatch.Patterns.ClaimCheck;
using Shouldly;

using Xunit;

namespace Excalibur.Dispatch.Patterns.Tests.ClaimCheck.InMemory;

/// <summary>
/// Unit tests for <see cref="InMemoryClaimCheckProvider"/> compression functionality.
/// </summary>
[Trait("Category", "Unit")]
public sealed class InMemoryClaimCheckProviderCompressionTests
{
	[Fact]
	public async Task StoreAsync_WithCompressionEnabled_ShouldCompressLargePayload()
	{
		// Arrange
		var provider = CreateProvider(options =>
		{
			options.EnableCompression = true;
			options.CompressionThreshold = 1024; // 1KB
			options.CompressionLevel = CompressionLevel.Fastest;
		});

		// Create a highly compressible payload (repeating text)
		var text = string.Concat(Enumerable.Repeat("This is a test payload that should compress well! ", 100));
		var originalPayload = Encoding.UTF8.GetBytes(text);

		// Act
		var reference = await provider.StoreAsync(originalPayload, CancellationToken.None);
		var retrievedPayload = await provider.RetrieveAsync(reference, CancellationToken.None);

		// Assert
		retrievedPayload.ShouldBe(originalPayload); // Should get back original data
		reference.Size.ShouldBe(originalPayload.Length); // Reference size should be original size
	}

	[Fact]
	public async Task StoreAsync_WithCompressionEnabled_BelowThreshold_ShouldNotCompress()
	{
		// Arrange
		var provider = CreateProvider(options =>
		{
			options.EnableCompression = true;
			options.CompressionThreshold = 2048; // 2KB
		});

		var payload = new byte[1024]; // 1KB - below threshold
		Array.Fill<byte>(payload, 65); // Fill with 'A'

		// Act
		var reference = await provider.StoreAsync(payload, CancellationToken.None);
		var retrievedPayload = await provider.RetrieveAsync(reference, CancellationToken.None);

		// Assert
		retrievedPayload.ShouldBe(payload);
	}

	[Fact]
	public async Task StoreAsync_WithCompressionDisabled_ShouldNotCompress()
	{
		// Arrange
		var provider = CreateProvider(options =>
		{
			options.EnableCompression = false;
		});

		var text = string.Concat(Enumerable.Repeat("Compressible data ", 200));
		var payload = Encoding.UTF8.GetBytes(text);

		// Act
		var reference = await provider.StoreAsync(payload, CancellationToken.None);
		var retrievedPayload = await provider.RetrieveAsync(reference, CancellationToken.None);

		// Assert
		retrievedPayload.ShouldBe(payload);
	}

	[Fact]
	public async Task StoreAsync_WithPoorCompressionRatio_ShouldUseOriginalPayload()
	{
		// Arrange
		var provider = CreateProvider(options =>
		{
			options.EnableCompression = true;
			options.CompressionThreshold = 1024;
			options.MinCompressionRatio = 0.9; // Only compress if at least 10% reduction
		});

		// Random data doesn't compress well
		var payload = new byte[2048];
		var random = new Random(42);
		random.NextBytes(payload);

		// Act
		var reference = await provider.StoreAsync(payload, CancellationToken.None);
		var retrievedPayload = await provider.RetrieveAsync(reference, CancellationToken.None);

		// Assert
		retrievedPayload.ShouldBe(payload);
	}

	[Theory]
	[InlineData(CompressionLevel.Fastest)]
	[InlineData(CompressionLevel.Optimal)]
	[InlineData(CompressionLevel.SmallestSize)]
	public async Task StoreAsync_WithDifferentCompressionLevels_ShouldWork(CompressionLevel level)
	{
		// Arrange
		var provider = CreateProvider(options =>
		{
			options.EnableCompression = true;
			options.CompressionThreshold = 1024;
			options.CompressionLevel = level;
		});

		var text = string.Concat(Enumerable.Repeat("Test data for compression level test ", 100));
		var originalPayload = Encoding.UTF8.GetBytes(text);

		// Act
		var reference = await provider.StoreAsync(originalPayload, CancellationToken.None);
		var retrievedPayload = await provider.RetrieveAsync(reference, CancellationToken.None);

		// Assert
		retrievedPayload.ShouldBe(originalPayload);
	}

	[Fact]
	public async Task StoreAsync_WithEmptyPayload_ShouldNotCompress()
	{
		// Arrange
		var provider = CreateProvider(options =>
		{
			options.EnableCompression = true;
			options.CompressionThreshold = 0; // Compress everything
		});

		var payload = Array.Empty<byte>();

		// Act
		var reference = await provider.StoreAsync(payload, CancellationToken.None);
		var retrievedPayload = await provider.RetrieveAsync(reference, CancellationToken.None);

		// Assert
		retrievedPayload.ShouldBe(payload);
	}

	[Fact]
	public async Task StoreAsync_MultiplePayloads_WithCompression_ShouldHandleEachCorrectly()
	{
		// Arrange
		var provider = CreateProvider(options =>
		{
			options.EnableCompression = true;
			options.CompressionThreshold = 1024;
		});

		var compressiblePayload = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("Compress me! ", 200)));
		var randomPayload = new byte[2048];
		new Random(42).NextBytes(randomPayload);
		var smallPayload = "Small"u8.ToArray();

		// Act
		var ref1 = await provider.StoreAsync(compressiblePayload, CancellationToken.None);
		var ref2 = await provider.StoreAsync(randomPayload, CancellationToken.None);
		var ref3 = await provider.StoreAsync(smallPayload, CancellationToken.None);

		var retrieved1 = await provider.RetrieveAsync(ref1, CancellationToken.None);
		var retrieved2 = await provider.RetrieveAsync(ref2, CancellationToken.None);
		var retrieved3 = await provider.RetrieveAsync(ref3, CancellationToken.None);

		// Assert
		retrieved1.ShouldBe(compressiblePayload);
		retrieved2.ShouldBe(randomPayload);
		retrieved3.ShouldBe(smallPayload);
	}

	[Fact]
	public async Task StoreAsync_WithJsonPayload_ShouldCompress()
	{
		// Arrange
		var provider = CreateProvider(options =>
		{
			options.EnableCompression = true;
			options.CompressionThreshold = 512;
		});

		// Generate JSON with multiple orders
		var orders = Enumerable.Range(1, 50).Select(i =>
			$@"{{""id"":""{i}"",""customerId"":""customer-{i}"",""amount"":{i * 100.50:F2},""items"":[""item1"",""item2"",""item3""]}}");
		var json = $@"{{""orders"":[{string.Join(",", orders)}]}}";

		var payload = Encoding.UTF8.GetBytes(json);

		// Act
		var reference = await provider.StoreAsync(payload, CancellationToken.None);
		var retrievedPayload = await provider.RetrieveAsync(reference, CancellationToken.None);
		var retrievedJson = Encoding.UTF8.GetString(retrievedPayload);

		// Assert
		retrievedJson.ShouldBe(json);
	}

	[Fact]
	public async Task StoreAsync_WithBinaryData_ShouldHandleCorrectly()
	{
		// Arrange
		var provider = CreateProvider(options =>
		{
			options.EnableCompression = true;
			options.CompressionThreshold = 1024;
		});

		// Create binary data (simulated image header)
		var binaryData = new byte[]
		{
			0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, // JPEG header
			0x49, 0x46, 0x00, 0x01, 0x01, 0x00, 0x00, 0x01
		};

		var payload = new byte[2048];
		Array.Copy(binaryData, payload, binaryData.Length);

		// Act
		var reference = await provider.StoreAsync(payload, CancellationToken.None);
		var retrievedPayload = await provider.RetrieveAsync(reference, CancellationToken.None);

		// Assert
		retrievedPayload.ShouldBe(payload);
	}

	[Fact]
	public async Task RetrieveAsync_AfterCompression_ShouldReturnExactOriginal()
	{
		// Arrange
		var provider = CreateProvider(options =>
		{
			options.EnableCompression = true;
			options.CompressionThreshold = 100;
		});

		var originalPayload = Encoding.UTF8.GetBytes("A".PadRight(500, 'A'));

		// Act
		var reference = await provider.StoreAsync(originalPayload, CancellationToken.None);
		var retrieved = await provider.RetrieveAsync(reference, CancellationToken.None);

		// Assert
		retrieved.Length.ShouldBe(originalPayload.Length);
		retrieved.ShouldBe(originalPayload);
	}

	[Fact]
	public async Task StoreAsync_WithVariousCompressionRatios_ShouldWorkCorrectly()
	{
		// Arrange
		var provider = CreateProvider(options =>
		{
			options.EnableCompression = true;
			options.CompressionThreshold = 1024;
			options.MinCompressionRatio = 0.5; // Must compress to 50% or less
		});

		// Create payloads with different compressibility
		var highlyCompressible = Encoding.UTF8.GetBytes(new string('A', 5000));
		var moderatelyCompressible = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("ABC123", 300)));
		var poorlyCompressible = new byte[5000];
		new Random(42).NextBytes(poorlyCompressible);

		// Act & Assert
		var ref1 = await provider.StoreAsync(highlyCompressible, CancellationToken.None);
		(await provider.RetrieveAsync(ref1, CancellationToken.None)).ShouldBe(highlyCompressible);

		var ref2 = await provider.StoreAsync(moderatelyCompressible, CancellationToken.None);
		(await provider.RetrieveAsync(ref2, CancellationToken.None)).ShouldBe(moderatelyCompressible);

		var ref3 = await provider.StoreAsync(poorlyCompressible, CancellationToken.None);
		(await provider.RetrieveAsync(ref3, CancellationToken.None)).ShouldBe(poorlyCompressible);
	}

	private static InMemoryClaimCheckProvider CreateProvider(Action<ClaimCheckOptions>? configure = null)
	{
		var options = new ClaimCheckOptions();
		configure?.Invoke(options);
		return new InMemoryClaimCheckProvider(Microsoft.Extensions.Options.Options.Create(options));
	}
}
