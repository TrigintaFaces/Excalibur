// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using System.Text;

using Amazon.SQS.Model;

using TransportCompressionAlgorithm = Excalibur.Dispatch.Serialization.CompressionAlgorithm;

namespace Excalibur.Dispatch.Transport.Aws;

internal static class AwsSqsMessageBodyCodec
{
	/// <summary>
	/// Decodes strictly, so that a byte sequence which is not well-formed UTF-8 raises
	/// <see cref="DecoderFallbackException"/> instead of being silently rewritten to U+FFFD.
	/// </summary>
	private static readonly UTF8Encoding StrictUtf8 =
		new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

	/// <summary>
	/// Renders a message body as an SQS message body string that decodes back to the same bytes.
	/// </summary>
	/// <param name="body">The message body bytes.</param>
	/// <param name="isBase64">
	/// On return, <see langword="true"/> when the returned string is a Base64 rendering of
	/// <paramref name="body"/> and the receiver must Base64-decode it; <see langword="false"/> when the
	/// returned string is the body's own UTF-8 text.
	/// </param>
	/// <returns>The SQS message body string.</returns>
	/// <remarks>
	/// A body is carried as text only when that is reversible. SQS bodies are text: the service accepts
	/// the XML 1.0 character set (#x9, #xA, #xD, #x20-#xD7FF, #xE000-#xFFFD, #x10000-#x10FFFF) and
	/// rejects anything else with <c>InvalidMessageContents</c>. Two kinds of byte buffer cannot survive
	/// that round trip and are therefore Base64-encoded instead: one that is not well-formed UTF-8 (a
	/// lossy decode would replace each invalid sequence with U+FFFD, so the receiver would hand the
	/// consumer different bytes than the producer sent), and one whose text contains a character SQS
	/// does not accept. Bodies that are already reversible text are unchanged on the wire, so existing
	/// producers and consumers see the same bytes they see today.
	/// </remarks>
	public static string EncodeBody(ReadOnlySpan<byte> body, out bool isBase64)
	{
		if (body.IsEmpty)
		{
			isBase64 = false;
			return string.Empty;
		}

		string text;
		try
		{
			text = StrictUtf8.GetString(body);
		}
		catch (DecoderFallbackException)
		{
			isBase64 = true;
			return Convert.ToBase64String(body);
		}

		if (!IsAcceptedBySqs(text))
		{
			isBase64 = true;
			return Convert.ToBase64String(body);
		}

		isBase64 = false;
		return text;
	}

	/// <summary>
	/// Recovers the original body bytes from a received SQS message.
	/// </summary>
	/// <param name="message">The received SQS message.</param>
	/// <returns>The body bytes as the producer supplied them.</returns>
	/// <exception cref="FormatException">
	/// The message declares a Base64 body encoding but its body is not valid Base64. The bytes are not
	/// recoverable, and returning the raw text would hand the consumer a different payload than the
	/// producer sent.
	/// </exception>
	public static byte[] DecodeBody(Message message)
	{
		ArgumentNullException.ThrowIfNull(message);

		if (TryDecodeBody(message, out var decompressed, out _))
		{
			return decompressed;
		}

		var body = message.Body ?? string.Empty;

		return IsBase64Encoded(message) ? Convert.FromBase64String(body) : Encoding.UTF8.GetBytes(body);
	}

	private static bool IsBase64Encoded(Message message) =>
		message.MessageAttributes is { Count: > 0 } &&
		message.MessageAttributes.TryGetValue(AwsSqsMessageAttributes.BodyEncoding, out var encodingAttribute) &&
		string.Equals(
			encodingAttribute.StringValue,
			AwsSqsMessageAttributes.BodyEncodingBase64,
			StringComparison.OrdinalIgnoreCase);

	/// <summary>
	/// Reports whether every character is in the XML 1.0 set SQS accepts in a message body.
	/// </summary>
	/// <remarks>
	/// Surrogates are accepted because the caller has already decoded strictly, which guarantees they
	/// are well-formed pairs and therefore denote #x10000-#x10FFFF.
	/// </remarks>
	private static bool IsAcceptedBySqs(string text)
	{
		foreach (var c in text)
		{
			if (c is '\t' or '\n' or '\r')
			{
				continue;
			}

			if (c is (>= '\u0020' and <= '\uD7FF') or (>= '\uE000' and <= '\uFFFD'))
			{
				continue;
			}

			if (char.IsSurrogate(c))
			{
				continue;
			}

			return false;
		}

		return true;
	}

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
