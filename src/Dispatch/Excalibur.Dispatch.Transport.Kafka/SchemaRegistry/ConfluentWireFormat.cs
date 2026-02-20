// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Buffers.Binary;

namespace Excalibur.Dispatch.Transport.Kafka;

/// <summary>
/// Provides utilities for working with the Confluent Schema Registry wire format.
/// </summary>
/// <remarks>
/// <para>
/// The Confluent wire format prepends a 5-byte header to serialized messages:
/// </para>
/// <code>
/// +--------+--------+--------+--------+--------+--------+...
/// | Magic  |       Schema ID (4 bytes)        | Payload
/// | 0x00   |  Big-endian 32-bit integer       | N bytes
/// +--------+--------+--------+--------+--------+--------+...
/// </code>
/// <para>
/// This format is used by all Confluent Schema Registry-aware serializers.
/// </para>
/// </remarks>
public static class ConfluentWireFormat
{
	/// <summary>
	/// The magic byte indicating Confluent wire format.
	/// </summary>
	public const byte MagicByte = 0x00;

	/// <summary>
	/// The size of the wire format header in bytes (1 magic + 4 schema ID).
	/// </summary>
	public const int HeaderSize = 5;

	/// <summary>
	/// Writes the wire format header to the destination span.
	/// </summary>
	/// <param name="destination">The destination span (must be at least <see cref="HeaderSize"/> bytes).</param>
	/// <param name="schemaId">The schema ID to write.</param>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="destination"/> is smaller than <see cref="HeaderSize"/> bytes.
	/// </exception>
	public static void WriteHeader(Span<byte> destination, int schemaId)
	{
		if (destination.Length < HeaderSize)
		{
			throw new ArgumentException(
				$"Destination must be at least {HeaderSize} bytes.",
				nameof(destination));
		}

		destination[0] = MagicByte;
		BinaryPrimitives.WriteInt32BigEndian(destination[1..], schemaId);
	}

	/// <summary>
	/// Reads the schema ID from a wire format message.
	/// </summary>
	/// <param name="source">The source span containing the wire format message.</param>
	/// <returns>The schema ID extracted from the header.</returns>
	/// <exception cref="InvalidOperationException">
	/// Thrown when the source is too short or has an invalid magic byte.
	/// </exception>
	public static int ReadSchemaId(ReadOnlySpan<byte> source)
	{
		if (source.Length < HeaderSize)
		{
			throw new InvalidOperationException(
				$"Invalid Confluent wire format: message must be at least {HeaderSize} bytes.");
		}

		if (source[0] != MagicByte)
		{
			throw new InvalidOperationException(
				$"Invalid Confluent wire format: expected magic byte 0x{MagicByte:X2}, got 0x{source[0]:X2}.");
		}

		return BinaryPrimitives.ReadInt32BigEndian(source[1..]);
	}

	/// <summary>
	/// Attempts to read the schema ID from a wire format message.
	/// </summary>
	/// <param name="source">The source span containing the wire format message.</param>
	/// <param name="schemaId">When successful, contains the schema ID.</param>
	/// <returns>
	/// <see langword="true"/> if the schema ID was read successfully; otherwise, <see langword="false"/>.
	/// </returns>
	public static bool TryReadSchemaId(ReadOnlySpan<byte> source, out int schemaId)
	{
		schemaId = 0;

		if (source.Length < HeaderSize || source[0] != MagicByte)
		{
			return false;
		}

		schemaId = BinaryPrimitives.ReadInt32BigEndian(source[1..]);
		return true;
	}

	/// <summary>
	/// Gets the payload portion of a wire format message (after the header).
	/// </summary>
	/// <param name="source">The source span containing the wire format message.</param>
	/// <returns>A span containing only the payload bytes.</returns>
	/// <exception cref="InvalidOperationException">
	/// Thrown when the source is too short or has an invalid magic byte.
	/// </exception>
	public static ReadOnlySpan<byte> GetPayload(ReadOnlySpan<byte> source)
	{
		if (source.Length < HeaderSize)
		{
			throw new InvalidOperationException(
				$"Invalid Confluent wire format: message must be at least {HeaderSize} bytes.");
		}

		if (source[0] != MagicByte)
		{
			throw new InvalidOperationException(
				$"Invalid Confluent wire format: expected magic byte 0x{MagicByte:X2}, got 0x{source[0]:X2}.");
		}

		return source[HeaderSize..];
	}

	/// <summary>
	/// Attempts to get the payload portion of a wire format message.
	/// </summary>
	/// <param name="source">The source span containing the wire format message.</param>
	/// <param name="payload">When successful, contains the payload bytes.</param>
	/// <returns>
	/// <see langword="true"/> if the payload was extracted successfully; otherwise, <see langword="false"/>.
	/// </returns>
	public static bool TryGetPayload(ReadOnlySpan<byte> source, out ReadOnlySpan<byte> payload)
	{
		payload = default;

		if (source.Length < HeaderSize || source[0] != MagicByte)
		{
			return false;
		}

		payload = source[HeaderSize..];
		return true;
	}

	/// <summary>
	/// Checks if the source bytes appear to be in Confluent wire format.
	/// </summary>
	/// <param name="source">The source bytes to check.</param>
	/// <returns>
	/// <see langword="true"/> if the source has the correct header; otherwise, <see langword="false"/>.
	/// </returns>
	public static bool IsWireFormat(ReadOnlySpan<byte> source)
	{
		return source.Length >= HeaderSize && source[0] == MagicByte;
	}
}
