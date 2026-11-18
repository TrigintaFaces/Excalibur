// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using System.IO.Compression;

using Snappier;

using TransportCompressionAlgorithm = Excalibur.Dispatch.Serialization.CompressionAlgorithm;

namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Provides compression and decompression utilities for Google Pub/Sub message payloads.
/// Supports Gzip (best ratio), Snappy (fastest), Deflate, and Brotli algorithms.
/// </summary>
internal static class GooglePubSubCompression
{
	/// <summary>
	/// Compresses the specified payload using the given algorithm.
	/// </summary>
	/// <param name="payload">The payload to compress.</param>
	/// <param name="algorithm">The compression algorithm to use.</param>
	/// <returns>The compressed payload bytes.</returns>
	/// <exception cref="NotSupportedException">Thrown when the algorithm is not supported.</exception>
	public static byte[] Compress(ReadOnlySpan<byte> payload, TransportCompressionAlgorithm algorithm)
	{
		if (payload.IsEmpty)
		{
			return [];
		}

		// Snappy uses a different API (not Stream-based) for best performance
		if (algorithm == TransportCompressionAlgorithm.Snappy)
		{
			return CompressSnappy(payload);
		}

		using var output = new MemoryStream();
		using (var compressor = CreateCompressor(output, algorithm))
		{
			compressor.Write(payload);
		}

		return output.ToArray();
	}

	/// <summary>
	/// Decompresses the specified payload using the given algorithm.
	/// </summary>
	/// <param name="payload">The compressed payload to decompress.</param>
	/// <param name="algorithm">The compression algorithm that was used.</param>
	/// <param name="maxDecompressedBytes">
	/// The maximum decompressed length, in bytes, or <see langword="null"/> to opt out.
	/// </param>
	/// <returns>The decompressed payload bytes.</returns>
	/// <exception cref="NotSupportedException">Thrown when the algorithm is not supported.</exception>
	/// <exception cref="PayloadTooLargeException">
	/// Thrown once the decompressed length would exceed <paramref name="maxDecompressedBytes"/>.
	/// </exception>
	/// <remarks>
	/// The limit is applied to the <em>output</em>, not the input. The inbound check the receiver already
	/// performs bounds what the sender transmitted; the expansion ratio is the sender's to choose, so a
	/// payload small enough to accept can still name an arbitrarily large allocation. For Snappy the bound
	/// is checked against the length declared in the payload's own header, <strong>before</strong> a buffer
	/// is sized from it — that header is the sender's claim, not a measurement.
	/// </remarks>
	public static byte[] Decompress(
			ReadOnlySpan<byte> payload,
			TransportCompressionAlgorithm algorithm,
			int? maxDecompressedBytes)
	{
		if (payload.IsEmpty)
		{
			return [];
		}

		// Snappy uses a different API (not Stream-based) for best performance
		if (algorithm == TransportCompressionAlgorithm.Snappy)
		{
			return DecompressSnappy(payload, maxDecompressedBytes);
		}

		using var input = new MemoryStream(payload.ToArray());
		using var decompressor = CreateDecompressor(input, algorithm);
		return PayloadSizeGuard.ReadAllWithinLimit(decompressor, maxDecompressedBytes);
	}

	/// <summary>
	/// Attempts to detect the compression algorithm from a compressed payload.
	/// This checks for known magic bytes/headers.
	/// </summary>
	/// <param name="payload">The potentially compressed payload.</param>
	/// <param name="algorithm">The detected algorithm, if any.</param>
	/// <returns>True if a compression algorithm was detected; otherwise, false.</returns>
	public static bool TryDetectAlgorithm(ReadOnlySpan<byte> payload, out TransportCompressionAlgorithm algorithm)
	{
		algorithm = TransportCompressionAlgorithm.None;

		if (payload.Length < 2)
		{
			return false;
		}

		// Gzip magic bytes: 0x1F 0x8B
		if (payload[0] == 0x1F && payload[1] == 0x8B)
		{
			algorithm = TransportCompressionAlgorithm.Gzip;
			return true;
		}

		// Deflate: Check for zlib header (0x78 followed by 0x01, 0x5E, 0x9C, or 0xDA)
		if (payload[0] == 0x78 &&
			(payload[1] == 0x01 || payload[1] == 0x5E || payload[1] == 0x9C || payload[1] == 0xDA))
		{
			algorithm = TransportCompressionAlgorithm.Deflate;
			return true;
		}

		// Snappy framing format starts with stream identifier: 0xFF 0x06 0x00 0x00 "sNaPpY"
		// However, Snappier uses raw Snappy format which doesn't have a standard magic header.
		// Detection is unreliable for raw Snappy - rely on message attributes instead.

		// Brotli doesn't have a standard magic header in its raw format.
		// Detection is unreliable - rely on message attributes instead.

		return false;
	}

	private static byte[] CompressSnappy(ReadOnlySpan<byte> payload)
	{
		// Snappier provides high-performance Snappy compression
		// Use the block-based API for best performance with message payloads
		var maxCompressedLength = Snappy.GetMaxCompressedLength(payload.Length);
		var compressedBuffer = new byte[maxCompressedLength];
		var compressedLength = Snappy.Compress(payload, compressedBuffer);
		return compressedBuffer.AsSpan(0, compressedLength).ToArray();
	}

	private static byte[] DecompressSnappy(ReadOnlySpan<byte> payload, int? maxDecompressedBytes)
	{
		// The Snappy header DECLARES the uncompressed length; it is the sender's claim and nothing has
		// verified it. Sizing a buffer from it first and checking afterwards would have performed the
		// allocation the limit exists to prevent, so the claim is refused before it is honoured.
		var uncompressedLength = Snappy.GetUncompressedLength(payload);
		PayloadSizeGuard.EnsureWithinLimit(uncompressedLength, maxDecompressedBytes);
		var decompressedBuffer = new byte[uncompressedLength];
		var actualLength = Snappy.Decompress(payload, decompressedBuffer);
		return decompressedBuffer.AsSpan(0, actualLength).ToArray();
	}

	private static Stream CreateCompressor(Stream output, TransportCompressionAlgorithm algorithm) =>
			algorithm switch
			{
				TransportCompressionAlgorithm.Gzip => new GZipStream(output, CompressionLevel.Fastest, leaveOpen: true),
				TransportCompressionAlgorithm.Deflate => new DeflateStream(output, CompressionLevel.Fastest, leaveOpen: true),
				TransportCompressionAlgorithm.Brotli => new BrotliStream(output, CompressionLevel.Fastest, leaveOpen: true),
				TransportCompressionAlgorithm.Snappy => throw new InvalidOperationException(
							"Snappy compression should use the dedicated CompressSnappy method."),
				_ => throw new NotSupportedException($"Compression algorithm '{algorithm}' is not supported."),
			};

	private static Stream CreateDecompressor(Stream input, TransportCompressionAlgorithm algorithm) =>
			algorithm switch
			{
				TransportCompressionAlgorithm.Gzip => new GZipStream(input, CompressionMode.Decompress, leaveOpen: true),
				TransportCompressionAlgorithm.Deflate => new DeflateStream(input, CompressionMode.Decompress, leaveOpen: true),
				TransportCompressionAlgorithm.Brotli => new BrotliStream(input, CompressionMode.Decompress, leaveOpen: true),
				TransportCompressionAlgorithm.Snappy => throw new InvalidOperationException(
							"Snappy decompression should use the dedicated DecompressSnappy method."),
				_ => throw new NotSupportedException($"Compression algorithm '{algorithm}' is not supported."),
			};
}
