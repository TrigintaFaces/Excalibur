// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0
using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Transport.Google;

using Google.Cloud.PubSub.V1;
using Google.Protobuf;

using Tests.Shared.Categories;

using Xunit;

using TextEncoding = System.Text.Encoding;

namespace Excalibur.Dispatch.Transport.Tests.GooglePubSub;

[Trait("Category", "Unit")]
public class GooglePubSubCompressionShould
{
	[Fact]
	[UnitTest]
	[Trait("Component", "Transport")]
	[Trait("Pattern", "TRANSPORT")]
	public void DecodeBody_DecompressesWhenAttributePresent()
	{
		var payload = TextEncoding.UTF8.GetBytes("payload");
		var compressed = GooglePubSubCompression.Compress(payload, CompressionAlgorithm.Gzip);
		var pubsubMessage = new PubsubMessage { Data = ByteString.CopyFrom(compressed) };

		pubsubMessage.Attributes[GooglePubSubMessageAttributes.Compression] =
				CompressionAlgorithm.Gzip.ToString();

		var decoded = GooglePubSubMessageBodyCodec.TryDecodeBody(
				pubsubMessage,
				out var decodedBody,
				out var algorithm);

		Assert.True(decoded);
		Assert.Equal(CompressionAlgorithm.Gzip, algorithm);
		Assert.Equal(payload, decodedBody.ToByteArray());
	}

	[Fact]
	[UnitTest]
	[Trait("Component", "Transport")]
	[Trait("Pattern", "TRANSPORT")]
	public void Compress_Snappy_CompressesAndDecompresses()
	{
		// Arrange
		var payload = TextEncoding.UTF8.GetBytes(
			"This is a test payload that needs to be compressed with Snappy algorithm for high throughput scenarios.");

		// Act
		var compressed = GooglePubSubCompression.Compress(payload, CompressionAlgorithm.Snappy);
		var decompressed = GooglePubSubCompression.Decompress(compressed, CompressionAlgorithm.Snappy);

		// Assert
		Assert.NotEqual(payload, compressed); // Should be different (compressed)
		Assert.Equal(payload, decompressed); // Should roundtrip correctly
	}

	[Fact]
	[UnitTest]
	[Trait("Component", "Transport")]
	[Trait("Pattern", "TRANSPORT")]
	public void TryDetectAlgorithm_DetectsGzip()
	{
		// Arrange
		var payload = TextEncoding.UTF8.GetBytes("test data for gzip detection");
		var compressed = GooglePubSubCompression.Compress(payload, CompressionAlgorithm.Gzip);

		// Act
		var detected = GooglePubSubCompression.TryDetectAlgorithm(compressed, out var algorithm);

		// Assert
		Assert.True(detected);
		Assert.Equal(CompressionAlgorithm.Gzip, algorithm);
	}

	[Fact]
	[UnitTest]
	[Trait("Component", "Transport")]
	[Trait("Pattern", "TRANSPORT")]
	public void TryDetectAlgorithm_ReturnsNone_ForUncompressedData()
	{
		// Arrange
		var payload = TextEncoding.UTF8.GetBytes("plain text data");

		// Act
		var detected = GooglePubSubCompression.TryDetectAlgorithm(payload, out var algorithm);

		// Assert
		Assert.False(detected);
		Assert.Equal(CompressionAlgorithm.None, algorithm);
	}

	[Fact]
	[UnitTest]
	[Trait("Component", "Transport")]
	[Trait("Pattern", "TRANSPORT")]
	public void DecodeBody_AutoDetectsGzip_WhenEnabled()
	{
		// Arrange
		var payload = TextEncoding.UTF8.GetBytes("payload for auto-detection");
		var compressed = GooglePubSubCompression.Compress(payload, CompressionAlgorithm.Gzip);
		var pubsubMessage = new PubsubMessage { Data = ByteString.CopyFrom(compressed) };
		// Note: NO compression attribute set

		// Act
		var decoded = GooglePubSubMessageBodyCodec.TryDecodeBody(
			pubsubMessage,
			enableAutoDetection: true,
			out var decodedBody,
			out var algorithm);

		// Assert
		Assert.True(decoded);
		Assert.Equal(CompressionAlgorithm.Gzip, algorithm);
		Assert.Equal(payload, decodedBody.ToByteArray());
	}

	[Fact]
	[UnitTest]
	[Trait("Component", "Transport")]
	[Trait("Pattern", "TRANSPORT")]
	public void DecodeBody_DoesNotAutoDetect_WhenDisabled()
	{
		// Arrange
		var payload = TextEncoding.UTF8.GetBytes("payload without auto-detection");
		var compressed = GooglePubSubCompression.Compress(payload, CompressionAlgorithm.Gzip);
		var pubsubMessage = new PubsubMessage { Data = ByteString.CopyFrom(compressed) };
		// Note: NO compression attribute set

		// Act
		var decoded = GooglePubSubMessageBodyCodec.TryDecodeBody(
			pubsubMessage,
			enableAutoDetection: false,
			out var decodedBody,
			out var algorithm);

		// Assert
		Assert.False(decoded);
		Assert.Null(algorithm);
		Assert.Equal(compressed, decodedBody.ToByteArray()); // Returns compressed data as-is
	}

	[Fact]
	[UnitTest]
	[Trait("Component", "Transport")]
	[Trait("Pattern", "TRANSPORT")]
	public void PubSubCompressionOptions_ShouldCompress_RespectsThreshold()
	{
		// Arrange
		var options = new PubSubCompressionOptions
		{
			Enabled = true,
			Algorithm = CompressionAlgorithm.Snappy,
			ThresholdBytes = 1000,
		};

		// Act & Assert
		Assert.False(options.ShouldCompress(500)); // Below threshold
		Assert.True(options.ShouldCompress(1000)); // At threshold
		Assert.True(options.ShouldCompress(2000)); // Above threshold
	}

	[Fact]
	[UnitTest]
	[Trait("Component", "Transport")]
	[Trait("Pattern", "TRANSPORT")]
	public void PubSubCompressionOptions_ShouldCompress_SkipsCompressedContentTypes()
	{
		// Arrange
		var options = new PubSubCompressionOptions
		{
			Enabled = true,
			Algorithm = CompressionAlgorithm.Gzip,
			ThresholdBytes = 100,
			CompressAlreadyCompressedContent = false,
		};

		// Act & Assert
		Assert.False(options.ShouldCompress(2000, "image/png")); // Already compressed
		Assert.False(options.ShouldCompress(2000, "application/gzip")); // Already compressed
		Assert.True(options.ShouldCompress(2000, "application/json")); // Should compress
		Assert.True(options.ShouldCompress(2000, "text/plain")); // Should compress
	}

	[Fact]
	[UnitTest]
	[Trait("Component", "Transport")]
	[Trait("Pattern", "TRANSPORT")]
	public void Compress_EmptyPayload_ReturnsEmpty()
	{
		// Arrange
		var emptyPayload = Array.Empty<byte>();

		// Act
		var compressedGzip = GooglePubSubCompression.Compress(emptyPayload, CompressionAlgorithm.Gzip);
		var compressedSnappy = GooglePubSubCompression.Compress(emptyPayload, CompressionAlgorithm.Snappy);

		// Assert
		Assert.Empty(compressedGzip);
		Assert.Empty(compressedSnappy);
	}
}
