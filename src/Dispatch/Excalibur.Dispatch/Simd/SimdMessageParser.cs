// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Text;

namespace Excalibur.Dispatch.Simd;

/// <summary>
/// SIMD-accelerated message parser for fast header and delimiter detection.
/// </summary>
public static class SimdMessageParser
{
	private const byte NewLine = (byte)'\n';
	private const byte CarriageReturn = (byte)'\r';
	private const byte Colon = (byte)':';
	private const byte Space = (byte)' ';
	private const byte Tab = (byte)'\t';
	private const byte OpenBrace = (byte)'{';
	private const byte OpenBracket = (byte)'[';

	/// <summary>
	/// Finds the index of the first occurrence of a byte value using SIMD operations.
	/// </summary>
	/// <param name="buffer"> The buffer to search. </param>
	/// <param name="value"> The byte value to find. </param>
	/// <returns> The index of the first occurrence, or -1 if not found. </returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static int FindFirstByte(ReadOnlySpan<byte> buffer, byte value)
	{
		if (Vector.IsHardwareAccelerated && buffer.Length >= Vector<byte>.Count)
		{
			return FindFirstByteVectorized(buffer, value);
		}

		return FindFirstByteScalar(buffer, value);
	}

	/// <summary>
	/// Finds the index of the first newline character (\r or \n) using SIMD.
	/// </summary>
	/// <param name="buffer"> The buffer to search. </param>
	/// <returns> The index of the first newline, or -1 if not found. </returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static int FindFirstNewline(ReadOnlySpan<byte> buffer)
	{
		if (Vector.IsHardwareAccelerated && buffer.Length >= Vector<byte>.Count)
		{
			return FindFirstNewlineVectorized(buffer);
		}

		return FindFirstNewlineScalar(buffer);
	}

	/// <summary>
	/// Finds all occurrences of a delimiter byte using SIMD operations.
	/// </summary>
	/// <param name="buffer"> The buffer to search. </param>
	/// <param name="delimiter"> The delimiter byte to find. </param>
	/// <param name="indices"> Buffer to store found indices. </param>
	/// <returns> The number of delimiters found. </returns>
	public static int FindAllDelimiters(ReadOnlySpan<byte> buffer, byte delimiter, Span<int> indices)
	{
		if (Vector.IsHardwareAccelerated && buffer.Length >= Vector<byte>.Count)
		{
			return FindAllDelimitersVectorized(buffer, delimiter, indices);
		}

		return FindAllDelimitersScalar(buffer, delimiter, indices);
	}

	/// <summary>
	/// Parses HTTP-style headers using SIMD acceleration.
	/// </summary>
	/// <param name="buffer"> The buffer containing headers. </param>
	/// <param name="headers"> Dictionary to store parsed headers. </param>
	/// <returns> The position after the headers (after double newline). </returns>
	public static int ParseHeaders(ReadOnlySpan<byte> buffer, Dictionary<string, string> headers)
	{
		ArgumentNullException.ThrowIfNull(headers);
		var position = 0;
		var headerEnd = FindDoubleNewline(buffer);

		if (headerEnd == -1)
		{
			headerEnd = buffer.Length;
		}

		while (position < headerEnd)
		{
			// Find end of line
			var lineEnd = FindFirstNewline(buffer[position..]);
			if (lineEnd == -1)
			{
				lineEnd = headerEnd - position;
			}

			var line = buffer.Slice(position, lineEnd);

			// Find colon separator
			var colonIndex = FindFirstByte(line, Colon);
			if (colonIndex > 0)
			{
				var key = line[..colonIndex];
				var valueStart = colonIndex + 1;

				// Skip whitespace after colon
				while (valueStart < line.Length && (line[valueStart] == Space || line[valueStart] == Tab))
				{
					valueStart++;
				}

				if (valueStart < line.Length)
				{
					var value = line[valueStart..];
					headers[Encoding.UTF8.GetString(key)] = Encoding.UTF8.GetString(value);
				}
			}

			position += lineEnd;

			// Skip newline characters
			while (position < buffer.Length && (buffer[position] == NewLine || buffer[position] == CarriageReturn))
			{
				position++;
			}
		}

		return headerEnd;
	}

	/// <summary>
	/// Finds the start of JSON content using SIMD operations.
	/// </summary>
	/// <param name="buffer"> The buffer to search. </param>
	/// <returns> The index of the first '{' or '[', or -1 if not found. </returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static int FindJsonStart(ReadOnlySpan<byte> buffer)
	{
		if (Vector.IsHardwareAccelerated && buffer.Length >= Vector<byte>.Count)
		{
			return FindJsonStartVectorized(buffer);
		}

		return FindJsonStartScalar(buffer);
	}

	/// <summary>
	/// Counts the number of a specific byte in the buffer using SIMD.
	/// </summary>
	/// <param name="buffer"> The buffer to search. </param>
	/// <param name="value"> The byte value to count. </param>
	/// <returns> The count of occurrences. </returns>
	public static int CountByte(ReadOnlySpan<byte> buffer, byte value)
	{
		if (Vector.IsHardwareAccelerated && buffer.Length >= Vector<byte>.Count)
		{
			return CountByteVectorized(buffer, value);
		}

		return CountByteScalar(buffer, value);
	}

	/// <summary>
	/// Uses AVX2 intrinsics for even faster searching when available.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static unsafe int FindFirstByteAvx2(ReadOnlySpan<byte> buffer, byte value)
	{
		if (!Avx2.IsSupported || buffer.Length < Vector256<byte>.Count)
		{
			return FindFirstByte(buffer, value);
		}

		var searchVector = Vector256.Create(value);
		var offset = 0;

		fixed (byte* ptr = buffer)
		{
			while (offset + Vector256<byte>.Count <= buffer.Length)
			{
				var vector = Avx.LoadVector256(ptr + offset);
				var cmp = Avx2.CompareEqual(vector, searchVector);
				var mask = (uint)Avx2.MoveMask(cmp);

				if (mask != 0)
				{
					return offset + BitOperations.TrailingZeroCount(mask);
				}

				offset += Vector256<byte>.Count;
			}
		}

		// Process remaining with regular SIMD or scalar
		if (offset < buffer.Length)
		{
			var result = FindFirstByte(buffer[offset..], value);
			return result >= 0 ? offset + result : -1;
		}

		return -1;
	}

	/// <summary>
	/// Finds the end of headers section (double CRLF).
	/// </summary>
	public static int FindHeadersEnd(ReadOnlySpan<byte> buffer)
	{
		if (Vector256.IsHardwareAccelerated && buffer.Length >= Vector256<byte>.Count)
		{
			var cr = Vector256.Create((byte)'\r');
			_ = Vector256.Create((byte)'\n');

			for (var i = 0; i <= buffer.Length - Vector256<byte>.Count - 3; i++)
			{
				var chunk = Vector256.LoadUnsafe(ref MemoryMarshal.GetReference(buffer), (nuint)i);
				var crMask = Vector256.Equals(chunk, cr);

				if (crMask != Vector256<byte>.Zero)
				{
					// Check for \r\n\r\n pattern
					if (i + 3 < buffer.Length &&
						buffer[i] == '\r' && buffer[i + 1] == '\n' &&
						buffer[i + 2] == '\r' && buffer[i + 3] == '\n')
					{
						return i + 4;
					}
				}
			}
		}

		// Fallback for small buffers
		for (var i = 0; i < buffer.Length - 3; i++)
		{
			if (buffer[i] == '\r' && buffer[i + 1] == '\n' &&
				buffer[i + 2] == '\r' && buffer[i + 3] == '\n')
			{
				return i + 4;
			}
		}

		return -1;
	}

	/// <summary>
	/// Tries to find a specific header in the buffer.
	/// </summary>
	public static bool TryFindHeader(ReadOnlySpan<byte> buffer, ReadOnlySpan<byte> headerName,
		out int valueStart, out int valueLength)
	{
		valueStart = -1;
		valueLength = 0;

		var pos = 0;
		while (pos < buffer.Length)
		{
			// Find start of line
			if (pos > 0 && buffer[pos - 1] != '\n')
			{
				pos++;
				continue;
			}

			// Check if header matches
			if (pos + headerName.Length + 2 < buffer.Length &&
				buffer.Slice(pos, headerName.Length).SequenceEqual(headerName) &&
				buffer[pos + headerName.Length] == ':')
			{
				// Skip ": "
				valueStart = pos + headerName.Length + 2;

				// Find end of line
				var end = valueStart;
				while (end < buffer.Length && buffer[end] != '\r' && buffer[end] != '\n')
				{
					end++;
				}

				valueLength = end - valueStart;
				return true;
			}

			// Move to next line
			while (pos < buffer.Length && buffer[pos] != '\n')
			{
				pos++;
			}

			pos++;
		}

		return false;
	}

	/// <summary>
	/// Counts newline characters in the buffer.
	/// </summary>
	public static int CountNewlines(ReadOnlySpan<byte> buffer)
	{
		var count = 0;

		if (Vector256.IsHardwareAccelerated && buffer.Length >= Vector256<byte>.Count)
		{
			var newline = Vector256.Create((byte)'\n');
			var i = 0;

			for (; i <= buffer.Length - Vector256<byte>.Count; i += Vector256<byte>.Count)
			{
				var chunk = Vector256.LoadUnsafe(ref MemoryMarshal.GetReference(buffer), (nuint)i);
				var mask = Vector256.Equals(chunk, newline);
				count += BitOperations.PopCount(mask.ExtractMostSignificantBits());
			}

			// Handle remainder
			for (; i < buffer.Length; i++)
			{
				if (buffer[i] == '\n')
				{
					count++;
				}
			}
		}
		else
		{
			// Scalar fallback
			foreach (var b in buffer)
			{
				if (b == '\n')
				{
					count++;
				}
			}
		}

		return count;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static int FindFirstByteVectorized(ReadOnlySpan<byte> buffer, byte value)
	{
		var searchVector = new Vector<byte>(value);
		var offset = 0;

		// Process vectors
		while (offset + Vector<byte>.Count <= buffer.Length)
		{
			var vector = new Vector<byte>(buffer.Slice(offset, Vector<byte>.Count));
			var mask = Vector.Equals(vector, searchVector);

			if (!Vector.EqualsAll(mask, Vector<byte>.Zero))
			{
				// Found a match, find the exact position
				for (var i = 0; i < Vector<byte>.Count; i++)
				{
					if (buffer[offset + i] == value)
					{
						return offset + i;
					}
				}
			}

			offset += Vector<byte>.Count;
		}

		// Process remaining bytes
		for (var i = offset; i < buffer.Length; i++)
		{
			if (buffer[i] == value)
			{
				return i;
			}
		}

		return -1;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static int FindFirstNewlineVectorized(ReadOnlySpan<byte> buffer)
	{
		var newlineVector = new Vector<byte>(NewLine);
		var crVector = new Vector<byte>(CarriageReturn);
		var offset = 0;

		while (offset + Vector<byte>.Count <= buffer.Length)
		{
			var vector = new Vector<byte>(buffer.Slice(offset, Vector<byte>.Count));
			var newlineMask = Vector.Equals(vector, newlineVector);
			var crMask = Vector.Equals(vector, crVector);
			var combinedMask = Vector.BitwiseOr(newlineMask, crMask);

			if (!Vector.EqualsAll(combinedMask, Vector<byte>.Zero))
			{
				// Found a match, find the exact position
				for (var i = 0; i < Vector<byte>.Count; i++)
				{
					var b = buffer[offset + i];
					if (b is NewLine or CarriageReturn)
					{
						return offset + i;
					}
				}
			}

			offset += Vector<byte>.Count;
		}

		// Process remaining bytes
		for (var i = offset; i < buffer.Length; i++)
		{
			var b = buffer[i];
			if (b is NewLine or CarriageReturn)
			{
				return i;
			}
		}

		return -1;
	}

	private static int FindAllDelimitersVectorized(ReadOnlySpan<byte> buffer, byte delimiter, Span<int> indices)
	{
		var delimiterVector = new Vector<byte>(delimiter);
		var offset = 0;
		var count = 0;

		while (offset + Vector<byte>.Count <= buffer.Length && count < indices.Length)
		{
			var vector = new Vector<byte>(buffer.Slice(offset, Vector<byte>.Count));
			var mask = Vector.Equals(vector, delimiterVector);

			if (!Vector.EqualsAll(mask, Vector<byte>.Zero))
			{
				// Found matches, extract positions
				for (var i = 0; i < Vector<byte>.Count && count < indices.Length; i++)
				{
					if (buffer[offset + i] == delimiter)
					{
						indices[count++] = offset + i;
					}
				}
			}

			offset += Vector<byte>.Count;
		}

		// Process remaining bytes
		for (var i = offset; i < buffer.Length && count < indices.Length; i++)
		{
			if (buffer[i] == delimiter)
			{
				indices[count++] = i;
			}
		}

		return count;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static int FindJsonStartVectorized(ReadOnlySpan<byte> buffer)
	{
		var openBraceVector = new Vector<byte>(OpenBrace);
		var openBracketVector = new Vector<byte>(OpenBracket);
		var offset = 0;

		while (offset + Vector<byte>.Count <= buffer.Length)
		{
			var vector = new Vector<byte>(buffer.Slice(offset, Vector<byte>.Count));
			var braceMask = Vector.Equals(vector, openBraceVector);
			var bracketMask = Vector.Equals(vector, openBracketVector);
			var combinedMask = Vector.BitwiseOr(braceMask, bracketMask);

			if (!Vector.EqualsAll(combinedMask, Vector<byte>.Zero))
			{
				// Found a match, find the exact position
				for (var i = 0; i < Vector<byte>.Count; i++)
				{
					var b = buffer[offset + i];
					if (b is OpenBrace or OpenBracket)
					{
						return offset + i;
					}
				}
			}

			offset += Vector<byte>.Count;
		}

		// Process remaining bytes
		for (var i = offset; i < buffer.Length; i++)
		{
			var b = buffer[i];
			if (b is OpenBrace or OpenBracket)
			{
				return i;
			}
		}

		return -1;
	}

	private static int CountByteVectorized(ReadOnlySpan<byte> buffer, byte value)
	{
		var searchVector = new Vector<byte>(value);
		var offset = 0;
		var count = 0;

		while (offset + Vector<byte>.Count <= buffer.Length)
		{
			var vector = new Vector<byte>(buffer.Slice(offset, Vector<byte>.Count));
			var mask = Vector.Equals(vector, searchVector);

			// Count set bits in the mask
			for (var i = 0; i < Vector<byte>.Count; i++)
			{
				if (mask[i] != 0)
				{
					count++;
				}
			}

			offset += Vector<byte>.Count;
		}

		// Process remaining bytes
		for (var i = offset; i < buffer.Length; i++)
		{
			if (buffer[i] == value)
			{
				count++;
			}
		}

		return count;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static int FindFirstByteScalar(ReadOnlySpan<byte> buffer, byte value)
	{
		for (var i = 0; i < buffer.Length; i++)
		{
			if (buffer[i] == value)
			{
				return i;
			}
		}

		return -1;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static int FindFirstNewlineScalar(ReadOnlySpan<byte> buffer)
	{
		for (var i = 0; i < buffer.Length; i++)
		{
			var b = buffer[i];
			if (b is NewLine or CarriageReturn)
			{
				return i;
			}
		}

		return -1;
	}

	private static int FindAllDelimitersScalar(ReadOnlySpan<byte> buffer, byte delimiter, Span<int> indices)
	{
		var count = 0;
		for (var i = 0; i < buffer.Length && count < indices.Length; i++)
		{
			if (buffer[i] == delimiter)
			{
				indices[count++] = i;
			}
		}

		return count;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static int FindJsonStartScalar(ReadOnlySpan<byte> buffer)
	{
		for (var i = 0; i < buffer.Length; i++)
		{
			var b = buffer[i];
			if (b is OpenBrace or OpenBracket)
			{
				return i;
			}
		}

		return -1;
	}

	private static int CountByteScalar(ReadOnlySpan<byte> buffer, byte value)
	{
		var count = 0;
		foreach (var t in buffer)
		{
			if (t == value)
			{
				count++;
			}
		}

		return count;
	}

	/// <summary>
	/// Finds the position of a double newline (header/body separator).
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static int FindDoubleNewline(ReadOnlySpan<byte> buffer)
	{
		const int minLength = 2; // Minimum for \n\n
		if (buffer.Length < minLength)
		{
			return -1;
		}

		for (var i = 0; i < buffer.Length - 1; i++)
		{
			var current = buffer[i];
			var next = buffer[i + 1];

			// Check for \n\n
			if (current == NewLine && next == NewLine)
			{
				return i + 2;
			}

			// Check for \r\n\r\n
			if (i < buffer.Length - 3 &&
				current == CarriageReturn &&
				next == NewLine &&
				buffer[i + 2] == CarriageReturn &&
				buffer[i + 3] == NewLine)
			{
				return i + 4;
			}
		}

		return -1;
	}
}
