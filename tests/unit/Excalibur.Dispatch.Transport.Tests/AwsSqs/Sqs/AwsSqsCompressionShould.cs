// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text;

using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Transport.Aws;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.Sqs;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class AwsSqsCompressionShould
{
	[Theory]
	[InlineData(CompressionAlgorithm.Gzip)]
	[InlineData(CompressionAlgorithm.Deflate)]
	[InlineData(CompressionAlgorithm.Brotli)]
	public void CompressAndDecompressRoundTrip(CompressionAlgorithm algorithm)
	{
		// Arrange
		var original = Encoding.UTF8.GetBytes("Hello, this is a test message that needs compression!");

		// Act
		var compressed = AwsSqsCompression.Compress(original, algorithm);
		var decompressed = AwsSqsCompression.Decompress(compressed, algorithm);

		// Assert
		decompressed.ShouldBe(original);
	}

	[Theory]
	[InlineData(CompressionAlgorithm.Gzip)]
	[InlineData(CompressionAlgorithm.Deflate)]
	[InlineData(CompressionAlgorithm.Brotli)]
	public void ReturnEmptyArrayWhenCompressingEmpty(CompressionAlgorithm algorithm)
	{
		// Arrange
		var empty = ReadOnlySpan<byte>.Empty;

		// Act
		var result = AwsSqsCompression.Compress(empty, algorithm);

		// Assert
		result.ShouldBeEmpty();
	}

	[Theory]
	[InlineData(CompressionAlgorithm.Gzip)]
	[InlineData(CompressionAlgorithm.Deflate)]
	[InlineData(CompressionAlgorithm.Brotli)]
	public void ReturnEmptyArrayWhenDecompressingEmpty(CompressionAlgorithm algorithm)
	{
		// Arrange
		var empty = ReadOnlySpan<byte>.Empty;

		// Act
		var result = AwsSqsCompression.Decompress(empty, algorithm);

		// Assert
		result.ShouldBeEmpty();
	}

	[Fact]
	public void ThrowForSnappyCompression()
	{
		// Arrange
		byte[] payload = Encoding.UTF8.GetBytes("test");

		// Act & Assert
		Should.Throw<NotSupportedException>(() =>
			AwsSqsCompression.Compress(payload, CompressionAlgorithm.Snappy));
	}

	[Fact]
	public void ThrowForSnappyDecompression()
	{
		// Arrange
		byte[] payload = Encoding.UTF8.GetBytes("test");

		// Act & Assert
		Should.Throw<NotSupportedException>(() =>
			AwsSqsCompression.Decompress(payload, CompressionAlgorithm.Snappy));
	}

	[Fact]
	public void ThrowForUnsupportedCompressionAlgorithm()
	{
		// Arrange
		byte[] payload = Encoding.UTF8.GetBytes("test");

		// Act & Assert
		Should.Throw<NotSupportedException>(() =>
			AwsSqsCompression.Compress(payload, (CompressionAlgorithm)999));
	}

	[Fact]
	public void ThrowForUnsupportedDecompressionAlgorithm()
	{
		// Arrange
		byte[] payload = Encoding.UTF8.GetBytes("test");

		// Act & Assert
		Should.Throw<NotSupportedException>(() =>
			AwsSqsCompression.Decompress(payload, (CompressionAlgorithm)999));
	}

	[Theory]
	[InlineData(CompressionAlgorithm.Gzip)]
	[InlineData(CompressionAlgorithm.Deflate)]
	[InlineData(CompressionAlgorithm.Brotli)]
	public void ProduceSmallerOutputForRepetitiveData(CompressionAlgorithm algorithm)
	{
		// Arrange - create a payload with enough repetition
		var repetitive = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("AAAA", 1000)));

		// Act
		var compressed = AwsSqsCompression.Compress(repetitive, algorithm);

		// Assert
		compressed.Length.ShouldBeLessThan(repetitive.Length);
	}
}
