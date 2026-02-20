// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0
using System.IO.Compression;

using TransportCompressionAlgorithm = Excalibur.Dispatch.Abstractions.Serialization.CompressionAlgorithm;

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

	public static byte[] Decompress(ReadOnlySpan<byte> payload, TransportCompressionAlgorithm algorithm)
	{
		if (payload.IsEmpty)
		{
			return [];
		}

		using var input = new MemoryStream(payload.ToArray());
		using var output = new MemoryStream();
		using (var decompressor = CreateDecompressor(input, algorithm))
		{
			decompressor.CopyTo(output);
		}

		return output.ToArray();
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
