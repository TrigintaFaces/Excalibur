// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Buffers.Binary;

namespace Excalibur.EventSourcing.Gcs;

/// <summary>Checks original object bytes against generation-bound GCS metadata.</summary>
/// <remarks>CRC32C detects transfer corruption; it is not an authenticity or collision-resistance guarantee.</remarks>
internal static class GcsArchiveIntegrity
{
	private static readonly uint[] Table = CreateTable();

	internal static void Validate(ReadOnlySpan<byte> content, ulong? storedSize, string? crc32c, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		if (storedSize is null || storedSize.Value != (ulong)content.Length)
		{
			throw new InvalidDataException("The archive length does not match its stored object metadata.");
		}

		Span<byte> expected = stackalloc byte[4];
		if (crc32c is null || !Convert.TryFromBase64String(crc32c, expected, out var written) || written != expected.Length)
		{
			throw new InvalidDataException("The stored archive must have a complete CRC32C checksum.");
		}

		var crc = uint.MaxValue;
		for (var i = 0; i < content.Length; i++)
		{
			if ((i & 0xffff) == 0)
			{
				cancellationToken.ThrowIfCancellationRequested();
			}

			crc = Table[(crc ^ content[i]) & 0xff] ^ (crc >> 8);
		}

		cancellationToken.ThrowIfCancellationRequested();
		// GCS encodes the finalized Castagnoli checksum as four big-endian bytes in Base64.
		if (~crc != BinaryPrimitives.ReadUInt32BigEndian(expected))
		{
			throw new InvalidDataException("The archive bytes do not match their stored CRC32C checksum.");
		}
	}

	private static uint[] CreateTable()
	{
		var table = new uint[256];
		for (var i = 0; i < table.Length; i++)
		{
			var value = (uint)i;
			for (var bit = 0; bit < 8; bit++)
			{
				value = (value >> 1) ^ ((value & 1) == 0 ? 0u : 0x82f63b78u);
			}

			table[i] = value;
		}

		return table;
	}
}
