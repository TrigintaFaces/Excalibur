// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Buffers;
using System.Text.Json;

namespace Excalibur.EventSourcing.Abstractions;

/// <summary>
/// Encodes and decodes opaque cursor strings for cursor-based (keyset) pagination.
/// </summary>
/// <remarks>
/// <para>
/// Cursors are Base64url-encoded JSON arrays of sort values from the boundary item
/// in a paged result set. They are safe to pass as URL query parameters without
/// additional encoding.
/// </para>
/// <para>
/// The cursor format is opaque to consumers — they should not parse or construct it.
/// The encoding is an implementation detail that may change between versions.
/// </para>
/// <para>
/// This encoder is backend-agnostic. It works with any data store that supports
/// keyset pagination (Elasticsearch <c>search_after</c>, SQL Server keyset queries,
/// Cosmos DB continuation tokens, etc.). Backend-specific packages provide helpers
/// that convert native sort values to/from the <see cref="object"/>[] format
/// expected by this encoder.
/// </para>
/// </remarks>
public static class CursorEncoder
{
	/// <summary>
	/// Encodes an array of sort values into an opaque Base64url cursor string.
	/// </summary>
	/// <param name="sortValues">
	/// The sort values from the boundary item (e.g., the last item on the current page).
	/// Supported types: <see cref="string"/>, <see cref="long"/>, <see cref="int"/>,
	/// <see cref="double"/>, <see cref="float"/>, <see cref="decimal"/>,
	/// <see cref="DateTimeOffset"/>, <see cref="DateTime"/>,
	/// <see cref="DateOnly"/>, <see cref="TimeOnly"/>,
	/// <see cref="bool"/>, and <c>null</c>.
	/// <see cref="DateTimeOffset"/> and <see cref="DateTime"/> values are stored as
	/// Unix epoch milliseconds for reliable round-tripping. <see cref="DateOnly"/> and
	/// <see cref="TimeOnly"/> values are stored as ISO 8601 strings which sort lexicographically.
	/// </param>
	/// <returns>A Base64url-encoded cursor string.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="sortValues"/> is <c>null</c>.</exception>
	/// <exception cref="ArgumentException">Thrown when <paramref name="sortValues"/> is empty.</exception>
	public static string Encode(params object?[] sortValues)
	{
		ArgumentNullException.ThrowIfNull(sortValues);

		if (sortValues.Length == 0)
		{
			throw new ArgumentException("Sort values must not be empty.", nameof(sortValues));
		}

		var buffer = new ArrayBufferWriter<byte>();

		using (var writer = new Utf8JsonWriter(buffer))
		{
			writer.WriteStartArray();

			for (var i = 0; i < sortValues.Length; i++)
			{
				WriteValue(writer, sortValues[i]);
			}

			writer.WriteEndArray();
		}

		return ToBase64Url(Convert.ToBase64String(buffer.WrittenSpan));
	}

	/// <summary>
	/// Decodes a Base64url cursor string back into an array of sort values.
	/// </summary>
	/// <param name="cursor">
	/// The opaque cursor string, or <c>null</c>/<see cref="string.Empty"/> for the first page.
	/// </param>
	/// <returns>
	/// An array of sort values (strings, numbers, booleans, or nulls),
	/// or <c>null</c> if the cursor is empty, whitespace, or invalid.
	/// </returns>
	/// <remarks>
	/// Invalid cursors (malformed Base64, corrupt JSON) are treated as "no cursor"
	/// and return <c>null</c>, causing the query to start from the beginning.
	/// This is intentional — a corrupt cursor should not fail the request.
	/// </remarks>
	public static object?[]? Decode(string? cursor)
	{
		if (string.IsNullOrWhiteSpace(cursor))
		{
			return null;
		}

		try
		{
			var json = Convert.FromBase64String(ToBase64(cursor));

			using var doc = JsonDocument.Parse(json);
			var root = doc.RootElement;

			if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() == 0)
			{
				return null;
			}

			var result = new object?[root.GetArrayLength()];
			var i = 0;

			foreach (var element in root.EnumerateArray())
			{
				result[i++] = element.ValueKind switch
				{
					JsonValueKind.String => element.GetString(),
					JsonValueKind.Number when element.TryGetInt64(out var l) => l,
					JsonValueKind.Number => element.GetDouble(),
					JsonValueKind.True => true,
					JsonValueKind.False => false,
					JsonValueKind.Null => null,
					_ => element.GetRawText()
				};
			}

			return result;
		}
		catch (Exception)
		{
			// Invalid cursor — treat as first page
			return null;
		}
	}

	/// <summary>
	/// Writes a single sort value to the JSON writer.
	/// </summary>
	private static void WriteValue(Utf8JsonWriter writer, object? value)
	{
		switch (value)
		{
			case null:
				writer.WriteNullValue();
				break;
			case string s:
				writer.WriteStringValue(s);
				break;
			case long l:
				writer.WriteNumberValue(l);
				break;
			case int i:
				writer.WriteNumberValue(i);
				break;
			case double d:
				writer.WriteNumberValue(d);
				break;
			case float f:
				writer.WriteNumberValue(f);
				break;
			case decimal m:
				writer.WriteNumberValue(m);
				break;
			case bool b:
				writer.WriteBooleanValue(b);
				break;
			case DateTimeOffset dto:
				writer.WriteNumberValue(dto.ToUnixTimeMilliseconds());
				break;
			case DateTime dt:
				writer.WriteNumberValue(new DateTimeOffset(dt).ToUnixTimeMilliseconds());
				break;
			case DateOnly d:
				writer.WriteStringValue(d.ToString("O"));
				break;
			case TimeOnly t:
				writer.WriteStringValue(t.ToString("O"));
				break;
			default:
				writer.WriteStringValue(value.ToString());
				break;
		}
	}

	/// <summary>
	/// Converts standard Base64 to Base64url (URL-safe, no padding).
	/// </summary>
	private static string ToBase64Url(string base64) =>
		base64.TrimEnd('=').Replace('+', '-').Replace('/', '_');

	/// <summary>
	/// Converts Base64url back to standard Base64 for decoding.
	/// </summary>
	private static string ToBase64(string base64Url)
	{
		var s = base64Url.Replace('-', '+').Replace('_', '/');

		// Restore padding
		return (s.Length % 4) switch
		{
			2 => s + "==",
			3 => s + "=",
			_ => s
		};
	}
}
