// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using System.IO.Compression;

using TransportCompressionAlgorithm = Excalibur.Dispatch.Serialization.CompressionAlgorithm;

namespace Excalibur.Dispatch.Transport.Aws;

internal static class AwsSqsCompression
{
	public static byte[] Compress(ReadOnlySpan<byte> payload, TransportCompressionAlgorithm algorithm)
	{
		if (payload.IsEmpty)
		{
			return [];
		}

		using var output = new MemoryStream();
		using (var compressor = CreateCompressor(output, algorithm))
		{
			compressor.Write(payload);
		}

		return output.ToArray();
	}

	/// <summary>
	/// Decompresses an inbound payload, refusing once the produced bytes exceed
	/// <paramref name="maxDecompressedBytes"/>.
	/// </summary>
	/// <remarks>
	/// The limit is applied to the <em>output</em>, not the input. The inbound check the receiver already
	/// performs bounds what the sender transmitted; the expansion ratio is the sender's to choose, so a
	/// payload small enough to accept can still name an arbitrarily large allocation. Passing
	/// <see langword="null"/> is the consumer's explicit opt-out, matching the rest of the payload-limit
	/// contract.
	/// </remarks>
	/// <param name="payload"> The compressed inbound payload. </param>
	/// <param name="algorithm"> The compression algorithm that was used. </param>
	/// <param name="maxDecompressedBytes"> The maximum decompressed length, or <see langword="null"/> to opt out. </param>
	/// <returns> The decompressed payload bytes. </returns>
	/// <exception cref="PayloadTooLargeException"> Thrown once the decompressed length would exceed the limit. </exception>
	public static byte[] Decompress(
			ReadOnlySpan<byte> payload,
			TransportCompressionAlgorithm algorithm,
			int? maxDecompressedBytes)
	{
		if (payload.IsEmpty)
		{
			return [];
		}

		using var input = new MemoryStream(payload.ToArray());
		using var decompressor = CreateDecompressor(input, algorithm);
		return PayloadSizeGuard.ReadAllWithinLimit(decompressor, maxDecompressedBytes);
	}

	private static Stream CreateCompressor(Stream output, TransportCompressionAlgorithm algorithm) =>
			algorithm switch
			{
				TransportCompressionAlgorithm.Gzip => new GZipStream(output, CompressionLevel.Fastest, leaveOpen: true),
				TransportCompressionAlgorithm.Deflate => new DeflateStream(output, CompressionLevel.Fastest, leaveOpen: true),
				TransportCompressionAlgorithm.Brotli => new BrotliStream(output, CompressionLevel.Fastest, leaveOpen: true),
				TransportCompressionAlgorithm.Snappy => throw new NotSupportedException(
							"Snappy compression is not supported for SQS payloads."),
				_ => throw new NotSupportedException($"Compression algorithm '{algorithm}' is not supported."),
			};

	private static Stream CreateDecompressor(Stream input, TransportCompressionAlgorithm algorithm) =>
			algorithm switch
			{
				TransportCompressionAlgorithm.Gzip => new GZipStream(input, CompressionMode.Decompress, leaveOpen: true),
				TransportCompressionAlgorithm.Deflate => new DeflateStream(input, CompressionMode.Decompress, leaveOpen: true),
				TransportCompressionAlgorithm.Brotli => new BrotliStream(input, CompressionMode.Decompress, leaveOpen: true),
				TransportCompressionAlgorithm.Snappy => throw new NotSupportedException(
							"Snappy compression is not supported for SQS payloads."),
				_ => throw new NotSupportedException($"Compression algorithm '{algorithm}' is not supported."),
			};
}
