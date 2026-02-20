// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0
using Amazon.SQS.Model;

using TransportCompressionAlgorithm = Excalibur.Dispatch.Abstractions.Serialization.CompressionAlgorithm;

namespace Excalibur.Dispatch.Transport.Aws;

internal static class AwsSqsMessageBodyCodec
{
	public static bool TryDecodeBody(
			Message message,
			out byte[] bodyBytes,
			out TransportCompressionAlgorithm? compressionAlgorithm)
	{
		bodyBytes = [];
		compressionAlgorithm = null;

		if (message.MessageAttributes is null || message.MessageAttributes.Count == 0)
		{
			return false;
		}

		if (!message.MessageAttributes.TryGetValue(AwsSqsMessageAttributes.Compression, out var compressionAttribute))
		{
			return false;
		}

		if (!message.MessageAttributes.TryGetValue(AwsSqsMessageAttributes.BodyEncoding, out var encodingAttribute))
		{
			return false;
		}

		if (!string.Equals(
						encodingAttribute.StringValue,
						AwsSqsMessageAttributes.BodyEncodingBase64,
						StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}

		if (!Enum.TryParse(
						compressionAttribute.StringValue,
						ignoreCase: true,
						out TransportCompressionAlgorithm algorithm) ||
			algorithm == TransportCompressionAlgorithm.None)
		{
			return false;
		}

		var encodedBody = message.Body ?? string.Empty;
		var compressedBytes = Convert.FromBase64String(encodedBody);
		bodyBytes = AwsSqsCompression.Decompress(compressedBytes, algorithm);
		compressionAlgorithm = algorithm;
		return true;
	}
}
