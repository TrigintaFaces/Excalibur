// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0
using Google.Cloud.PubSub.V1;
using Google.Protobuf;

using TransportCompressionAlgorithm = Excalibur.Dispatch.Abstractions.Serialization.CompressionAlgorithm;

namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Provides encoding and decoding utilities for Google Pub/Sub message bodies,
/// including compression/decompression support with auto-detection.
/// </summary>
internal static class GooglePubSubMessageBodyCodec
{
	/// <summary>
	/// Attempts to decode the message body, handling decompression if the message
	/// indicates it was compressed via the dispatch-compression attribute.
	/// </summary>
	/// <param name="message">The Pub/Sub message to decode.</param>
	/// <param name="decodedBody">The decoded body (decompressed if applicable).</param>
	/// <param name="compressionAlgorithm">The algorithm used, if compression was detected.</param>
	/// <returns>True if decompression was performed; otherwise false.</returns>
	public static bool TryDecodeBody(
			PubsubMessage message,
			out ByteString decodedBody,
			out TransportCompressionAlgorithm? compressionAlgorithm)
	{
		return TryDecodeBody(message, enableAutoDetection: false, out decodedBody, out compressionAlgorithm);
	}

	/// <summary>
	/// Attempts to decode the message body, handling decompression if the message
	/// indicates it was compressed via the dispatch-compression attribute, or
	/// optionally via magic byte auto-detection.
	/// </summary>
	/// <param name="message">The Pub/Sub message to decode.</param>
	/// <param name="enableAutoDetection">
	/// When true, attempts to detect Gzip/Deflate compression by examining magic bytes
	/// even if the compression attribute is not present.
	/// </param>
	/// <param name="decodedBody">The decoded body (decompressed if applicable).</param>
	/// <param name="compressionAlgorithm">The algorithm used, if compression was detected.</param>
	/// <returns>True if decompression was performed; otherwise false.</returns>
	public static bool TryDecodeBody(
			PubsubMessage message,
			bool enableAutoDetection,
			out ByteString decodedBody,
			out TransportCompressionAlgorithm? compressionAlgorithm)
	{
		decodedBody = message.Data;
		compressionAlgorithm = null;

		// First, check for explicit compression attribute
		if (message.Attributes.TryGetValue(
						GooglePubSubMessageAttributes.Compression,
						out var compressionAttribute))
		{
			if (Enum.TryParse(
							compressionAttribute,
							ignoreCase: true,
							out TransportCompressionAlgorithm algorithm) &&
				algorithm != TransportCompressionAlgorithm.None)
			{
				decodedBody = ByteString.CopyFrom(
						GooglePubSubCompression.Decompress(message.Data.Span, algorithm));
				compressionAlgorithm = algorithm;
				return true;
			}
		}

		// If auto-detection is enabled, try to detect compression from magic bytes
		if (enableAutoDetection && !message.Data.IsEmpty)
		{
			if (GooglePubSubCompression.TryDetectAlgorithm(message.Data.Span, out var detectedAlgorithm))
			{
				try
				{
					decodedBody = ByteString.CopyFrom(
							GooglePubSubCompression.Decompress(message.Data.Span, detectedAlgorithm));
					compressionAlgorithm = detectedAlgorithm;
					return true;
				}
				catch
				{
					// Auto-detection false positive - payload is not actually compressed
					// Return the original data
					decodedBody = message.Data;
					compressionAlgorithm = null;
					return false;
				}
			}
		}

		return false;
	}
}
