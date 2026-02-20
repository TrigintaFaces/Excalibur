// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Text;

namespace Excalibur.Dispatch.Simd;

/// <summary>
/// SIMD-accelerated JSON field extractor for fast field value extraction.
/// </summary>
public static class SimdJsonFieldExtractor
{
	private const byte Quote = (byte)'"';
	private const byte Backslash = (byte)'\\';
	private const byte Colon = (byte)':';
	private const byte Space = (byte)' ';
	private const byte Tab = (byte)'\t';
	private const byte NewLine = (byte)'\n';
	private const byte CarriageReturn = (byte)'\r';

	/// <summary>
	/// true.
	/// </summary>
	private const byte LowerT = (byte)'t';

	/// <summary>
	/// false.
	/// </summary>
	private const byte LowerF = (byte)'f';

	private const byte Minus = (byte)'-';
	private const byte Plus = (byte)'+';
	private const byte Zero = (byte)'0';
	private const byte Nine = (byte)'9';
	private const byte Dot = (byte)'.';
	private const byte LowerE = (byte)'e';
	private const byte UpperE = (byte)'E';

	/// <summary>
	/// Extracts a string field value from JSON using SIMD operations.
	/// </summary>
	/// <param name="json"> The JSON buffer. </param>
	/// <param name="fieldName"> The field name to find. </param>
	/// <param name="value"> The extracted string value. </param>
	/// <returns> True if the field was found and extracted; otherwise, false. </returns>
	public static bool TryExtractStringField(ReadOnlySpan<byte> json, ReadOnlySpan<byte> fieldName, out string? value)
	{
		value = null;

		var fieldStart = FindFieldName(json, fieldName);
		if (fieldStart == -1)
		{
			return false;
		}

		// Skip to colon
		var colonPos = FindNextNonWhitespace(json, fieldStart + fieldName.Length + 2);
		if (colonPos == -1 || json[colonPos] != Colon)
		{
			return false;
		}

		// Skip to value
		var valueStart = FindNextNonWhitespace(json, colonPos + 1);
		if (valueStart == -1 || json[valueStart] != Quote)
		{
			return false;
		}

		// Extract string value
		var stringEnd = FindClosingQuote(json, valueStart + 1);
		if (stringEnd == -1)
		{
			return false;
		}

		value = Encoding.UTF8.GetString(json.Slice(valueStart + 1, stringEnd - valueStart - 1));
		return true;
	}

	/// <summary>
	/// Extracts a numeric field value from JSON using SIMD operations.
	/// </summary>
	/// <param name="json"> The JSON buffer. </param>
	/// <param name="fieldName"> The field name to find. </param>
	/// <param name="value"> The extracted numeric value. </param>
	/// <returns> True if the field was found and extracted; otherwise, false. </returns>
	public static bool TryExtractNumericField(ReadOnlySpan<byte> json, ReadOnlySpan<byte> fieldName, out double value)
	{
		value = 0;

		var fieldStart = FindFieldName(json, fieldName);
		if (fieldStart == -1)
		{
			return false;
		}

		// Skip to colon
		var colonPos = FindNextNonWhitespace(json, fieldStart + fieldName.Length + 2);
		if (colonPos == -1 || json[colonPos] != Colon)
		{
			return false;
		}

		// Skip to value
		var valueStart = FindNextNonWhitespace(json, colonPos + 1);
		if (valueStart == -1)
		{
			return false;
		}

		// Find end of number
		var valueEnd = FindNumberEnd(json, valueStart);
		if (valueEnd == -1)
		{
			return false;
		}

		var numberSpan = json.Slice(valueStart, valueEnd - valueStart);
		var numberString = Encoding.UTF8.GetString(numberSpan);
		return double.TryParse(numberString, out value);
	}

	/// <summary>
	/// Extracts a boolean field value from JSON using SIMD operations.
	/// </summary>
	/// <param name="json"> The JSON buffer. </param>
	/// <param name="fieldName"> The field name to find. </param>
	/// <param name="value"> The extracted boolean value. </param>
	/// <returns> True if the field was found and extracted; otherwise, false. </returns>
	public static bool TryExtractBooleanField(ReadOnlySpan<byte> json, ReadOnlySpan<byte> fieldName, out bool value)
	{
		value = false;

		var fieldStart = FindFieldName(json, fieldName);
		if (fieldStart == -1)
		{
			return false;
		}

		// Skip to colon
		var colonPos = FindNextNonWhitespace(json, fieldStart + fieldName.Length + 2);
		if (colonPos == -1 || json[colonPos] != Colon)
		{
			return false;
		}

		// Skip to value
		var valueStart = FindNextNonWhitespace(json, colonPos + 1);
		if (valueStart == -1)
		{
			return false;
		}

		// Check for true/false
		if (json[valueStart] == LowerT && json.Length >= valueStart + 4)
		{
			var trueSpan = json.Slice(valueStart, 4);
			if (trueSpan.SequenceEqual("true"u8))
			{
				value = true;
				return true;
			}
		}
		else if (json[valueStart] == LowerF && json.Length >= valueStart + 5)
		{
			var falseSpan = json.Slice(valueStart, 5);
			if (falseSpan.SequenceEqual("false"u8))
			{
				value = false;
				return true;
			}
		}

		return false;
	}

	/// <summary>
	/// Extracts multiple string fields in a single pass using SIMD operations.
	/// </summary>
	/// <param name="json"> The JSON buffer. </param>
	/// <param name="fieldNames"> The field names to find. </param>
	/// <param name="values"> Dictionary to store extracted values. </param>
	/// <returns> The number of fields successfully extracted. </returns>
	public static int ExtractMultipleStringFields(ReadOnlySpan<byte> json, ReadOnlySpan<string> fieldNames,
		Dictionary<string, string> values)
	{
		ArgumentNullException.ThrowIfNull(values);
		values.Clear();
		var extracted = 0;
		var searchStart = 0;

		// Process each field
		foreach (var fieldName in fieldNames)
		{
			var fieldBytes = Encoding.UTF8.GetBytes(fieldName);

			// Search from last position for efficiency
			var fieldStart = FindFieldName(json[searchStart..], fieldBytes);
			if (fieldStart == -1)
			{
				continue;
			}

			fieldStart += searchStart;

			// Skip to colon
			var colonPos = FindNextNonWhitespace(json, fieldStart + fieldBytes.Length + 2);
			if (colonPos == -1 || json[colonPos] != Colon)
			{
				continue;
			}

			// Skip to value
			var valueStart = FindNextNonWhitespace(json, colonPos + 1);
			if (valueStart == -1 || json[valueStart] != Quote)
			{
				continue;
			}

			// Extract string value
			var stringEnd = FindClosingQuote(json, valueStart + 1);
			if (stringEnd == -1)
			{
				continue;
			}

			var value = Encoding.UTF8.GetString(json.Slice(valueStart + 1, stringEnd - valueStart - 1));
			values[fieldName] = value;
			extracted++;

			// Update search position for next field
			searchStart = stringEnd + 1;
		}

		return extracted;
	}

	/// <summary>
	/// Extracts a string field using AVX2 intrinsics for maximum performance.
	/// </summary>
	public static bool TryExtractStringFieldAvx2(ReadOnlySpan<byte> json, ReadOnlySpan<byte> fieldName, out string? value)
	{
		value = null;

		if (!Avx2.IsSupported)
		{
			return TryExtractStringField(json, fieldName, out value);
		}

		var fieldStart = FindFieldNameAvx2(json, fieldName);
		if (fieldStart == -1)
		{
			return false;
		}

		// Continue with regular extraction after finding field
		var colonPos = FindNextNonWhitespace(json, fieldStart + fieldName.Length + 2);
		if (colonPos == -1 || json[colonPos] != Colon)
		{
			return false;
		}

		var valueStart = FindNextNonWhitespace(json, colonPos + 1);
		if (valueStart == -1 || json[valueStart] != Quote)
		{
			return false;
		}

		var stringEnd = FindClosingQuoteAvx2(json, valueStart + 1);
		if (stringEnd == -1)
		{
			return false;
		}

		value = Encoding.UTF8.GetString(json.Slice(valueStart + 1, stringEnd - valueStart - 1));
		return true;
	}

	/// <summary>
	/// Finds a field name in JSON using SIMD operations.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static int FindFieldName(ReadOnlySpan<byte> json, ReadOnlySpan<byte> fieldName)
	{
		if (fieldName.Length == 0 || json.Length < fieldName.Length + 3) // Need at least ":x
		{
			return -1;
		}

		// Build search pattern: "fieldName"
		Span<byte> pattern = stackalloc byte[fieldName.Length + 2];
		pattern[0] = Quote;
		fieldName.CopyTo(pattern[1..]);
		pattern[^1] = Quote;

		return FindPattern(json, pattern);
	}

	/// <summary>
	/// Finds a pattern in the buffer using SIMD operations.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static int FindPattern(ReadOnlySpan<byte> buffer, ReadOnlySpan<byte> pattern)
	{
		if (pattern.Length == 0 || buffer.Length < pattern.Length)
		{
			return -1;
		}

		var firstByte = pattern[0];
		for (var position = 0; position <= buffer.Length - pattern.Length; position++)
		{
			// Find first byte using SIMD
			var firstBytePos = SimdMessageParser.FindFirstByte(buffer[position..], firstByte);
			if (firstBytePos == -1)
			{
				return -1;
			}

			position += firstBytePos;

			// Check if we have enough space for the pattern
			if (position + pattern.Length > buffer.Length)
			{
				return -1;
			}

			// Check full pattern
			if (buffer.Slice(position, pattern.Length).SequenceEqual(pattern))
			{
				return position;
			}
		}

		return -1;
	}

	/// <summary>
	/// Finds the next non-whitespace character using SIMD operations.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static int FindNextNonWhitespace(ReadOnlySpan<byte> buffer, int start)
	{
		if (start >= buffer.Length)
		{
			return -1;
		}

		if (Vector.IsHardwareAccelerated)
		{
			return FindNextNonWhitespaceVectorized(buffer, start);
		}

		return FindNextNonWhitespaceScalar(buffer, start);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static int FindNextNonWhitespaceVectorized(ReadOnlySpan<byte> buffer, int start)
	{
		var spaceVector = new Vector<byte>(Space);
		var tabVector = new Vector<byte>(Tab);
		var newlineVector = new Vector<byte>(NewLine);
		var crVector = new Vector<byte>(CarriageReturn);

		var offset = start;

		while (offset + Vector<byte>.Count <= buffer.Length)
		{
			var vector = new Vector<byte>(buffer.Slice(offset, Vector<byte>.Count));

			var spaceMask = Vector.Equals(vector, spaceVector);
			var tabMask = Vector.Equals(vector, tabVector);
			var newlineMask = Vector.Equals(vector, newlineVector);
			var crMask = Vector.Equals(vector, crVector);

			var whitespaceMask = Vector.BitwiseOr(
				Vector.BitwiseOr(spaceMask, tabMask),
				Vector.BitwiseOr(newlineMask, crMask));

			var nonWhitespaceMask = Vector.OnesComplement(whitespaceMask);

			if (!Vector.EqualsAll(nonWhitespaceMask, Vector<byte>.Zero))
			{
				// Found non-whitespace
				for (var i = 0; i < Vector<byte>.Count; i++)
				{
					var b = buffer[offset + i];
					if (b is not Space and not Tab and not NewLine and not CarriageReturn)
					{
						return offset + i;
					}
				}
			}

			offset += Vector<byte>.Count;
		}

		// Process remaining bytes
		return FindNextNonWhitespaceScalar(buffer, offset);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static int FindNextNonWhitespaceScalar(ReadOnlySpan<byte> buffer, int start)
	{
		for (var i = start; i < buffer.Length; i++)
		{
			var b = buffer[i];
			if (b is not Space and not Tab and not NewLine and not CarriageReturn)
			{
				return i;
			}
		}

		return -1;
	}

	/// <summary>
	/// Finds the closing quote of a string, handling escapes.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static int FindClosingQuote(ReadOnlySpan<byte> buffer, int start)
	{
		if (start >= buffer.Length)
		{
			return -1;
		}

		var position = start;
		var inEscape = false;

		while (position < buffer.Length)
		{
			var b = buffer[position];

			if (inEscape)
			{
				inEscape = false;
			}
			else if (b == Backslash)
			{
				inEscape = true;
			}
			else if (b == Quote)
			{
				return position;
			}

			position++;
		}

		return -1;
	}

	/// <summary>
	/// Finds the end of a numeric value.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static int FindNumberEnd(ReadOnlySpan<byte> buffer, int start)
	{
		if (start >= buffer.Length)
		{
			return -1;
		}

		var position = start;

		// Skip optional minus
		if (position < buffer.Length && buffer[position] == Minus)
		{
			position++;
		}

		// Must have at least one digit
		if (position >= buffer.Length || !IsDigit(buffer[position]))
		{
			return -1;
		}

		// Skip digits
		while (position < buffer.Length && IsDigit(buffer[position]))
		{
			position++;
		}

		// Optional decimal part
		if (position < buffer.Length && buffer[position] == Dot)
		{
			position++;

			// Must have at least one digit after decimal
			if (position >= buffer.Length || !IsDigit(buffer[position]))
			{
				return position - 1; // Back up to before the dot
			}

			while (position < buffer.Length && IsDigit(buffer[position]))
			{
				position++;
			}
		}

		// Optional exponent
		if (position < buffer.Length && (buffer[position] == LowerE || buffer[position] == UpperE))
		{
			position++;

			// Optional sign
			if (position < buffer.Length && (buffer[position] == Plus || buffer[position] == Minus))
			{
				position++;
			}

			// Must have at least one digit
			if (position >= buffer.Length || !IsDigit(buffer[position]))
			{
				return position - 1; // Back up
			}

			while (position < buffer.Length && IsDigit(buffer[position]))
			{
				position++;
			}
		}

		return position;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static bool IsDigit(byte b) => b is >= Zero and <= Nine;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static unsafe int FindFieldNameAvx2(ReadOnlySpan<byte> json, ReadOnlySpan<byte> fieldName)
	{
		if (fieldName.Length == 0 || json.Length < fieldName.Length + 3)
		{
			return -1;
		}

		// Build search pattern: "fieldName"
		Span<byte> pattern = stackalloc byte[fieldName.Length + 2];
		pattern[0] = Quote;
		fieldName.CopyTo(pattern[1..]);
		pattern[^1] = Quote;

		// Use AVX2 to find first byte
		var firstByte = pattern[0];
		var searchVector = Vector256.Create(firstByte);
		var position = 0;

		fixed (byte* jsonPtr = json)
		{
			fixed (byte* patternPtr = pattern)
			{
				while (position + Vector256<byte>.Count <= json.Length - pattern.Length)
				{
					var vector = Avx.LoadVector256(jsonPtr + position);
					var cmp = Avx2.CompareEqual(vector, searchVector);
					var mask = (uint)Avx2.MoveMask(cmp);

					while (mask != 0)
					{
						var bitPos = BitOperations.TrailingZeroCount(mask);
						var checkPos = position + bitPos;

						// Check full pattern
						if (checkPos + pattern.Length <= json.Length)
						{
							var match = true;
							for (var i = 0; i < pattern.Length; i++)
							{
								if (jsonPtr[checkPos + i] != patternPtr[i])
								{
									match = false;
									break;
								}
							}

							if (match)
							{
								return checkPos;
							}
						}

						// Clear the bit we just checked
						mask &= mask - 1;
					}

					position += Vector256<byte>.Count;
				}
			}
		}

		// Fall back to regular search for remaining
		if (position < json.Length)
		{
			var result = FindPattern(json[position..], pattern);
			return result >= 0 ? position + result : -1;
		}

		return -1;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static unsafe int FindClosingQuoteAvx2(ReadOnlySpan<byte> buffer, int start)
	{
		if (!Avx2.IsSupported || start >= buffer.Length)
		{
			return FindClosingQuote(buffer, start);
		}

		var quoteVector = Vector256.Create(Quote);
		var backslashVector = Vector256.Create(Backslash);
		var position = start;

		fixed (byte* ptr = buffer)
		{
			while (position + Vector256<byte>.Count <= buffer.Length)
			{
				var vector = Avx.LoadVector256(ptr + position);
				var quoteMask = (uint)Avx2.MoveMask(Avx2.CompareEqual(vector, quoteVector));
				var backslashMask = (uint)Avx2.MoveMask(Avx2.CompareEqual(vector, backslashVector));

				// Process found quotes and backslashes
				for (var i = 0; i < Vector256<byte>.Count && position + i < buffer.Length; i++)
				{
					if ((backslashMask & (1u << i)) != 0)
					{
						// Skip next character
						position += i + 2;
						goto next_vector;
					}

					if ((quoteMask & (1u << i)) != 0)
					{
						return position + i;
					}
				}

				position += Vector256<byte>.Count;
			next_vector:
				;
			}
		}

		// Fall back to scalar for remaining
		return FindClosingQuote(buffer, position);
	}
}
