// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Buffers;
using System.Text;
using System.Text.Json;

using Amazon.DynamoDBv2.Model;

namespace Excalibur.Outbox.DynamoDb;

/// <summary>Writes the closed String/Number/Binary shape of a DynamoDB query continuation key.</summary>
/// <remarks>
/// The token is opaque. Query keys support S, N and B only; unsupported or ambiguous SDK values fail
/// instead of producing a token that silently drops a term. Numeric text is preserved without parsing.
/// Binary values contain the entire stream, independent of its current position. This writer has no
/// reflection-based serializer and does not expose an incoming pagination API.
/// </remarks>
internal static class DynamoDbContinuationTokenWriter
{
	internal static string? Serialize(Dictionary<string, AttributeValue>? key)
	{
		if (key is null || key.Count == 0)
		{
			return null;
		}

		var buffer = new ArrayBufferWriter<byte>();
		using (var writer = new Utf8JsonWriter(buffer))
		{
			writer.WriteStartObject();
			foreach (var (name, value) in key)
			{
				if (value is null || value.BOOL.HasValue || value.NULL.HasValue
					|| value.IsBSSet || value.IsSSSet || value.IsNSSet || value.IsLSet || value.IsMSet
					|| (value.S is not null ? 1 : 0) + (value.N is not null ? 1 : 0) + (value.B is not null ? 1 : 0) != 1)
				{
					throw new InvalidOperationException($"DynamoDB continuation key '{name}' must contain exactly one String, Number or Binary value.");
				}
				writer.WritePropertyName(name);
				writer.WriteStartObject();
				if (value.S is not null)
				{
					writer.WriteString("S", value.S);
				}
				else if (value.N is not null)
				{
					writer.WriteString("N", value.N);
				}
				else
				{
					writer.WriteBase64String("B", value.B!.ToArray());
				}
				writer.WriteEndObject();
			}
			writer.WriteEndObject();
		}
		return Encoding.UTF8.GetString(buffer.WrittenSpan);
	}
}
