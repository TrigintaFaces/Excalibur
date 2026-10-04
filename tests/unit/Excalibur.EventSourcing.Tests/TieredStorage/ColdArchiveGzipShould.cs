// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Buffers;
using System.Buffers.Binary;
using System.IO.Compression;
using System.IO.Hashing;
using System.Text;

using Excalibur.EventSourcing.TieredStorage;

namespace Excalibur.EventSourcing.Tests.TieredStorage;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Pattern", "EventSourcing")]
public sealed class ColdArchiveGzipShould
{
	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	[InlineData(1018)]
	[InlineData(1019)]
	[InlineData(1020)]
	[InlineData(1023)]
	[InlineData(1024)]
	[InlineData(1025)]
	[InlineData(8192)]
	[InlineData(65536)]
	public void DecodeAcrossBufferBoundariesAndCompressionLevels(int size)
	{
		var payload = new byte[size];
		new Random(42).NextBytes(payload);
		foreach (var level in new[] { CompressionLevel.NoCompression, CompressionLevel.Fastest, CompressionLevel.SmallestSize })
		{
			ColdArchiveGzip.Decode(Compress(payload, level), size, CancellationToken.None).ShouldBe(payload);
		}
	}

	[Theory]
	[InlineData(1)]
	[InlineData(1023)]
	[InlineData(1024)]
	[InlineData(1025)]
	[InlineData(16384)]
	public void RejectTrailingDeflateBytesEvenWithTheOriginalValidFooter(int extra)
	{
		var gzip = Compress(Encoding.UTF8.GetBytes("A complete archive."));
		byte[] corrupted = [.. gzip.AsSpan(0, gzip.Length - 8), .. new byte[extra], .. gzip.AsSpan(gzip.Length - 8)];
		Should.Throw<InvalidDataException>(() => ColdArchiveGzip.Decode(corrupted, 100, CancellationToken.None));
	}

	[Fact]
	public void RejectTruncatedDeflateWithAnIntactFooter()
	{
		var gzip = Compress(Encoding.UTF8.GetBytes("A complete archive."));
		byte[] corrupted = [.. gzip.AsSpan(0, gzip.Length - 9), .. gzip.AsSpan(gzip.Length - 8)];
		Should.Throw<InvalidDataException>(() => ColdArchiveGzip.Decode(corrupted, 100, CancellationToken.None));
	}

	[Fact]
	public void RejectEveryTruncatedPrefix()
	{
		var gzip = Compress(Encoding.UTF8.GetBytes("A complete archive."));
		for (var length = 0; length < gzip.Length; length++)
		{
			var prefix = gzip.AsMemory(0, length);
			Should.Throw<InvalidDataException>(() => ColdArchiveGzip.Decode(prefix, 100, CancellationToken.None));
		}
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public void RejectConcatenatedMembersIncludingAnEmptyLastMember(bool emptyLast)
	{
		byte[] gzip = [.. Compress(Encoding.UTF8.GetBytes("first")), .. Compress(emptyLast ? [] : Encoding.UTF8.GetBytes("second"))];
		Should.Throw<InvalidDataException>(() => ColdArchiveGzip.Decode(gzip, 100, CancellationToken.None));
	}

	[Theory]
	[InlineData(8)]
	[InlineData(4)]
	public void ValidateBothFooterFields(int fromEnd)
	{
		var gzip = Compress(Encoding.UTF8.GetBytes("payload"));
		gzip[^fromEnd] ^= 1;
		Should.Throw<InvalidDataException>(() => ColdArchiveGzip.Decode(gzip, 100, CancellationToken.None));
	}

	[Fact]
	public void ValidateOptionalHeaderFieldsAndTheirChecksum()
	{
		var payload = Encoding.UTF8.GetBytes("payload");
		var original = Compress(payload);
		byte[] header = [.. original.AsSpan(0, 10), 3, 0, 1, 2, 3, (byte)'n', 0, (byte)'c', 0];
		header[3] = 4 | 8 | 16 | 2;
		var crc = (ushort)(Crc32.HashToUInt32(header) & 0xffff);
		byte[] gzip = [.. header, (byte)crc, (byte)(crc >> 8), .. original.AsSpan(10)];
		ColdArchiveGzip.Decode(gzip, 100, CancellationToken.None).ShouldBe(payload);
		gzip[header.Length] ^= 1;
		Should.Throw<InvalidDataException>(() => ColdArchiveGzip.Decode(gzip, 100, CancellationToken.None));
	}

	[Fact]
	public void RejectInvalidHeaderLengthsAndReservedFlags()
	{
		var gzip = Compress(Encoding.UTF8.GetBytes("payload"));
		gzip[3] = 4;
		BinaryPrimitives.WriteUInt16LittleEndian(gzip.AsSpan(10), ushort.MaxValue);
		Should.Throw<InvalidDataException>(() => ColdArchiveGzip.Decode(gzip, 100, CancellationToken.None));
		gzip[3] = 0x20;
		Should.Throw<InvalidDataException>(() => ColdArchiveGzip.Decode(gzip, 100, CancellationToken.None));
		gzip[3] = 8;
		gzip.AsSpan(10, gzip.Length - 18).Fill(1);
		Should.Throw<InvalidDataException>(() => ColdArchiveGzip.Decode(gzip, 100, CancellationToken.None));
	}

	[Fact]
	public void EnforceOutputLimitAndCancellation()
	{
		var gzip = Compress(new byte[10000]);
		Should.Throw<InvalidDataException>(() => ColdArchiveGzip.Decode(gzip, 9999, CancellationToken.None));
		Should.Throw<OperationCanceledException>(() => ColdArchiveGzip.Decode(gzip, 10000, new CancellationToken(true)));
	}

	[Theory]
	[InlineData(1, "H4sIAAAAAAAA/3N0HAWjYBSMglEwCkbBKBgFo2AUjIJRMApGwSgYBaNgFIyCUTAKRsEoGAWjYBSMglEwCkbBKBgFo2AUjIJRMApGwSgYBaNgFIyCUTAKRsEoGAWjYBSMglEwCkbBKBgFo2AUjIJRMApGwSgYBaNgFIyCUTAKRsEoGAWjYBSMglEwGAAAGxCa2CBOAAA=")]
	[InlineData(2, "H4sIAAAAAAAA/+3BMQEAAADCoGzrX8oaHkABAAAAAAAAAAAAAAAAAAAAAAAA8GAbEJrYIE4AAA==")]
	public void DecodeKnownFixedAndDynamicHuffmanBlocks(int blockType, string fixture)
	{
		// Independent zlib fixtures encode 20000 ASCII A bytes and span multiple output reads.
		var gzip = Convert.FromBase64String(fixture);
		((gzip[10] >> 1) & 3).ShouldBe(blockType);
		var expected = new byte[20000];
		expected.AsSpan().Fill((byte)'A');
		ColdArchiveGzip.Decode(gzip, expected.Length, CancellationToken.None).ShouldBe(expected);
	}

	[Fact]
	public void ObserveCancellationDuringInputReadsThatProduceNoOutput()
	{
		// 4000 empty nonfinal stored blocks followed by an empty final block and zero CRC/ISIZE.
		var gzip = new byte[10 + (4001 * 5) + 8];
		gzip[0] = 0x1f;
		gzip[1] = 0x8b;
		gzip[2] = 8;
		for (var block = 0; block <= 4000; block++)
		{
			var offset = 10 + (block * 5);
			gzip[offset] = block == 4000 ? (byte)1 : (byte)0;
			gzip[offset + 3] = 0xff;
			gzip[offset + 4] = 0xff;
		}

		ColdArchiveGzip.Decode(gzip, 0, CancellationToken.None).ShouldBeEmpty();
		using var cancellation = new CancellationTokenSource();
		using var memory = new CancelDuringReadMemory(gzip, cancellation);
		Should.Throw<OperationCanceledException>(() => ColdArchiveGzip.Decode(memory.Memory, 0, cancellation.Token));
		memory.CancelledAt.ShouldBe(10);
		memory.SpanReads.ShouldBe(memory.CancelledAt);
	}

	private static byte[] Compress(byte[] bytes, CompressionLevel level = CompressionLevel.Optimal)
	{
		if (bytes.Length == 0)
		{
			// A valid empty member; GZipStream need not emit a member when no input is written.
			return [0x1f, 0x8b, 8, 0, 0, 0, 0, 0, 0, 0xff, 3, 0, 0, 0, 0, 0, 0, 0, 0, 0];
		}

		using var result = new MemoryStream();
		using (var gzip = new GZipStream(result, level, leaveOpen: true))
		{
			gzip.Write(bytes);
		}

		return result.ToArray();
	}

	private sealed class CancelDuringReadMemory(byte[] bytes, CancellationTokenSource cancellation) : MemoryManager<byte>
	{
		internal int SpanReads { get; private set; }
		internal int CancelledAt { get; private set; }
		public override Span<byte> GetSpan()
		{
			if (++SpanReads == 10)
			{
				CancelledAt = SpanReads;
				cancellation.Cancel();
			}

			return bytes;
		}

		public override MemoryHandle Pin(int elementIndex = 0) => throw new NotSupportedException();
		public override void Unpin() { }
		protected override void Dispose(bool disposing) { }
	}
}
