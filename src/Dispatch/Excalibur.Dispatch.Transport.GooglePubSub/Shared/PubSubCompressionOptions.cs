// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Serialization;

namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Configuration options for message compression in Google Pub/Sub transport.
/// </summary>
/// <remarks>
/// <para>
/// Compression can significantly reduce message size and bandwidth costs, especially
/// for large JSON or text-based payloads. The trade-off is increased CPU usage for
/// compression/decompression operations.
/// </para>
/// <para>
/// <strong>Algorithm Recommendations:</strong>
/// <list type="bullet">
///   <item><description><see cref="CompressionAlgorithm.Snappy"/>: Fastest compression/decompression. Best for high-throughput scenarios where speed matters more than compression ratio.</description></item>
///   <item><description><see cref="CompressionAlgorithm.Gzip"/>: Best compression ratio. Good for bandwidth-constrained scenarios where CPU is not a bottleneck.</description></item>
///   <item><description><see cref="CompressionAlgorithm.Brotli"/>: Better ratio than Gzip, but slower. Best for pre-compressed static content.</description></item>
///   <item><description><see cref="CompressionAlgorithm.Deflate"/>: Similar to Gzip but without header/trailer overhead. Moderate speed and ratio.</description></item>
/// </list>
/// </para>
/// </remarks>
/// <example>
/// <code>
/// services.AddGooglePubSubTransport(options =>
/// {
///     options.Compression.Enabled = true;
///     options.Compression.Algorithm = CompressionAlgorithm.Snappy;
///     options.Compression.ThresholdBytes = 1024; // Only compress messages > 1KB
/// });
/// </code>
/// </example>
public sealed class PubSubCompressionOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether message compression is enabled.
	/// </summary>
	/// <value>
	/// <see langword="true"/> if compression is enabled; otherwise, <see langword="false"/>.
	/// Defaults to <see langword="false"/>.
	/// </value>
	public bool Enabled { get; set; }

	/// <summary>
	/// Gets or sets the compression algorithm to use for outgoing messages.
	/// </summary>
	/// <value>
	/// The compression algorithm. Defaults to <see cref="CompressionAlgorithm.Gzip"/>.
	/// </value>
	/// <remarks>
	/// <para>
	/// <see cref="CompressionAlgorithm.Snappy"/> is recommended for high-throughput scenarios
	/// as it offers the fastest compression/decompression with moderate compression ratio.
	/// </para>
	/// <para>
	/// <see cref="CompressionAlgorithm.Gzip"/> is recommended when bandwidth cost is the primary
	/// concern and CPU resources are available.
	/// </para>
	/// </remarks>
	public CompressionAlgorithm Algorithm { get; set; } = CompressionAlgorithm.Gzip;

	/// <summary>
	/// Gets or sets the minimum message size in bytes that triggers compression.
	/// Messages smaller than this threshold are sent uncompressed.
	/// </summary>
	/// <value>
	/// The threshold in bytes. Defaults to 1024 (1 KB).
	/// </value>
	/// <remarks>
	/// <para>
	/// Compressing very small messages can actually increase their size due to compression
	/// overhead. Setting an appropriate threshold ensures compression is only applied when
	/// beneficial.
	/// </para>
	/// <para>
	/// Recommended values:
	/// <list type="bullet">
	///   <item><description>1 KB (1024): Good for general use - most messages under 1KB don't compress well.</description></item>
	///   <item><description>256 bytes: More aggressive - use if most of your messages are text/JSON.</description></item>
	///   <item><description>4 KB (4096): Conservative - only compress larger messages.</description></item>
	/// </list>
	/// </para>
	/// </remarks>
	public int ThresholdBytes { get; set; } = 1024;

	/// <summary>
	/// Gets or sets a value indicating whether to automatically detect and decompress
	/// incoming compressed messages that don't have compression metadata.
	/// </summary>
	/// <value>
	/// <see langword="true"/> to enable auto-detection; otherwise, <see langword="false"/>.
	/// Defaults to <see langword="false"/>.
	/// </value>
	/// <remarks>
	/// <para>
	/// When enabled, the consumer will attempt to detect Gzip and Deflate compressed
	/// payloads by checking for magic bytes, even if the message doesn't include the
	/// <c>dispatch-compression</c> attribute.
	/// </para>
	/// <para>
	/// This is useful for interoperability with systems that compress messages but don't
	/// follow the Dispatch compression attribute convention. However, it adds overhead
	/// for checking every message's payload bytes.
	/// </para>
	/// <para>
	/// <strong>Note:</strong> Snappy and Brotli payloads cannot be reliably auto-detected
	/// as they lack distinctive magic byte signatures.
	/// </para>
	/// </remarks>
	public bool EnableAutoDetection { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to compress messages that are already
	/// in a compressed format (e.g., images, archives).
	/// </summary>
	/// <value>
	/// <see langword="true"/> to compress all messages regardless of content type;
	/// <see langword="false"/> to skip compression for already-compressed content types.
	/// Defaults to <see langword="false"/>.
	/// </value>
	/// <remarks>
	/// When <see langword="false"/>, messages with content types indicating already-compressed
	/// data (e.g., <c>image/png</c>, <c>application/zip</c>, <c>application/gzip</c>) will not
	/// be compressed, as re-compression typically provides no benefit and wastes CPU cycles.
	/// </remarks>
	public bool CompressAlreadyCompressedContent { get; set; }

	/// <summary>
	/// Gets a list of content types that are considered already compressed and should
	/// not be compressed again when <see cref="CompressAlreadyCompressedContent"/> is
	/// <see langword="false"/>.
	/// </summary>
	/// <value>
	/// A list of content type prefixes that indicate compressed content.
	/// </value>
	/// <remarks>
	/// <para>
	/// The default list includes common compressed formats:
	/// <list type="bullet">
	///   <item><description><c>image/</c> - Most image formats (PNG, JPEG, WebP, etc.)</description></item>
	///   <item><description><c>video/</c> - Video formats are typically compressed</description></item>
	///   <item><description><c>audio/</c> - Audio formats (MP3, AAC, etc.)</description></item>
	///   <item><description><c>application/zip</c></description></item>
	///   <item><description><c>application/gzip</c></description></item>
	///   <item><description><c>application/x-7z-compressed</c></description></item>
	///   <item><description><c>application/x-rar-compressed</c></description></item>
	///   <item><description><c>application/pdf</c> - PDFs are internally compressed</description></item>
	/// </list>
	/// </para>
	/// <para>
	/// You can add additional content types to this list if needed.
	/// </para>
	/// </remarks>
	public List<string> CompressedContentTypes { get; } =
	[
		"image/",
		"video/",
		"audio/",
		"application/zip",
		"application/gzip",
		"application/x-gzip",
		"application/x-7z-compressed",
		"application/x-rar-compressed",
		"application/x-bzip2",
		"application/x-xz",
		"application/pdf",
	];

	/// <summary>
	/// Determines whether the specified content type represents already-compressed content.
	/// </summary>
	/// <param name="contentType">The content type to check.</param>
	/// <returns>
	/// <see langword="true"/> if the content type indicates compressed content;
	/// otherwise, <see langword="false"/>.
	/// </returns>
	public bool IsAlreadyCompressedContentType(string? contentType)
	{
		if (string.IsNullOrWhiteSpace(contentType))
		{
			return false;
		}

		foreach (var compressedType in CompressedContentTypes)
		{
			if (contentType.StartsWith(compressedType, StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
		}

		return false;
	}

	/// <summary>
	/// Determines whether the specified payload should be compressed based on the
	/// current configuration.
	/// </summary>
	/// <param name="payloadSize">The size of the payload in bytes.</param>
	/// <param name="contentType">The content type of the message, if known.</param>
	/// <returns>
	/// <see langword="true"/> if the payload should be compressed;
	/// otherwise, <see langword="false"/>.
	/// </returns>
	public bool ShouldCompress(int payloadSize, string? contentType = null)
	{
		if (!Enabled)
		{
			return false;
		}

		if (payloadSize < ThresholdBytes)
		{
			return false;
		}

		if (!CompressAlreadyCompressedContent && IsAlreadyCompressedContentType(contentType))
		{
			return false;
		}

		return true;
	}
}
