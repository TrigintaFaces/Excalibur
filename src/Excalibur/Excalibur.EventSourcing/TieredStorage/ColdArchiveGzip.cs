// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Buffers.Binary;
using System.IO.Compression;
using System.IO.Hashing;

namespace Excalibur.EventSourcing.TieredStorage;

/// <summary>Decodes the single-member gzip archive format without accepting partial or trailing data.</summary>
/// <remarks>
/// Concatenated members are valid gzip but are not the framework's single-member archive format.
/// Raw DeflateStream completion-before-next-input behavior is qualified against the pinned runtime.
/// The final 1024 compressed bytes are supplied singly so inflater read-ahead cannot hide trailing data.
/// </remarks>
internal static class ColdArchiveGzip
{
	internal static byte[] Decode(ReadOnlyMemory<byte> archive, int maxOutputBytes, CancellationToken cancellationToken)
	{
		ArgumentOutOfRangeException.ThrowIfNegative(maxOutputBytes);
		cancellationToken.ThrowIfCancellationRequested();
		var headerLength = ReadHeader(archive.Span);
		var footer = archive.Span[^8..];
		using var input = new RawDeflateInput(archive.Slice(headerLength, archive.Length - headerLength - 8), cancellationToken);
		using var inflater = new DeflateStream(input, CompressionMode.Decompress, leaveOpen: true);
		using var output = new MemoryStream();
		var buffer = new byte[8192];
		int count;
		while ((count = inflater.Read(buffer)) != 0)
		{
			cancellationToken.ThrowIfCancellationRequested();
			if (output.Length + count > maxOutputBytes)
			{
				throw new InvalidDataException("The decoded archive exceeds the configured limit.");
			}

			output.Write(buffer.AsSpan(0, count));
		}

		cancellationToken.ThrowIfCancellationRequested();
		if (input.Position != input.Length)
		{
			throw new InvalidDataException("Trailing data or concatenated members are not supported in an archive.");
		}

		var decoded = output.GetBuffer().AsSpan(0, checked((int)output.Length));
		if (BinaryPrimitives.ReadUInt32LittleEndian(footer) != Crc32.HashToUInt32(decoded)
			|| BinaryPrimitives.ReadUInt32LittleEndian(footer[4..]) != unchecked((uint)output.Length))
		{
			throw new InvalidDataException("The archive checksum or uncompressed length is invalid.");
		}

		return decoded.ToArray();
	}

	private static int ReadHeader(ReadOnlySpan<byte> bytes)
	{
		if (bytes.Length < 20 || bytes[0] != 0x1f || bytes[1] != 0x8b || bytes[2] != 8 || (bytes[3] & 0xe0) != 0)
		{
			throw new InvalidDataException("The archive gzip header is invalid or unsupported.");
		}

		var position = 10;
		var end = bytes.Length - 8;
		if ((bytes[3] & 4) != 0)
		{
			Advance(ref position, 2, end);
			Advance(ref position, BinaryPrimitives.ReadUInt16LittleEndian(bytes[(position - 2)..]), end);
		}

		if ((bytes[3] & 8) != 0)
		{
			SkipTerminated(bytes, ref position, end);
		}

		if ((bytes[3] & 16) != 0)
		{
			SkipTerminated(bytes, ref position, end);
		}

		if ((bytes[3] & 2) != 0)
		{
			var checksum = (ushort)(Crc32.HashToUInt32(bytes[..position]) & 0xffff);
			Advance(ref position, 2, end);
			if (BinaryPrimitives.ReadUInt16LittleEndian(bytes[(position - 2)..]) != checksum)
			{
				throw new InvalidDataException("The archive gzip header checksum is invalid.");
			}
		}

		if (position >= end)
		{
			throw new InvalidDataException("The archive has no compressed payload.");
		}

		return position;
	}

	private static void SkipTerminated(ReadOnlySpan<byte> bytes, ref int position, int end)
	{
		var length = bytes[position..end].IndexOf((byte)0);
		if (length < 0)
		{
			throw new InvalidDataException("An archive gzip header field is unterminated.");
		}

		Advance(ref position, length + 1, end);
	}

	private static void Advance(ref int position, int count, int end)
	{
		if (count > end - position)
		{
			throw new InvalidDataException("An archive gzip header field is truncated.");
		}

		position += count;
	}

	private sealed class RawDeflateInput(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken) : Stream
	{
		private int _position;
		public override bool CanRead => true;
		public override bool CanSeek => false;
		public override bool CanWrite => false;
		public override long Length => bytes.Length;
		public override long Position { get => _position; set => throw new NotSupportedException(); }
		public override void Flush() => throw new NotSupportedException();
		public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
		public override void SetLength(long value) => throw new NotSupportedException();
		public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
		public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

		public override int Read(Span<byte> buffer)
		{
			if (buffer.IsEmpty)
			{
				return 0;
			}

			cancellationToken.ThrowIfCancellationRequested();

			var remaining = bytes.Length - _position;
			if (remaining == 0)
			{
				// A completed raw inflater never needs another input byte. EOF here is truncation.
				throw new InvalidDataException("The archive deflate stream is truncated.");
			}

			var count = Math.Min(buffer.Length, remaining > 1024 ? remaining - 1024 : 1);
			bytes.Span.Slice(_position, count).CopyTo(buffer);
			_position += count;
			return count;
		}
	}
}
