// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Text;

using Excalibur.EventSourcing.Gcs;

namespace Excalibur.EventSourcing.Tests.TieredStorage;

[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
[Trait("Pattern", "EventSourcing")]
public sealed class GcsArchiveIntegrityShould
{
	[Theory]
	[InlineData("", "AAAAAA==")]
	[InlineData("123456789", "4waSgw==")]
	public void AcceptKnownCastagnoliCheckVectors(string text, string crc)
	{
		var bytes = Encoding.ASCII.GetBytes(text);
		GcsArchiveIntegrity.Validate(bytes, (ulong)bytes.Length, crc, CancellationToken.None);
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("not-base64")]
	[InlineData("AA==")]
	[InlineData("AAAAAAA=")]
	[InlineData("g5IG4w==")]
	[InlineData("y/Q5Jg==")]
	public void RejectMissingMalformedWrongEndianOrIeeeChecksum(string? crc)
	{
		Should.Throw<InvalidDataException>(() => GcsArchiveIntegrity.Validate("123456789"u8, 9, crc, CancellationToken.None));
	}

	[Theory]
	[InlineData(null)]
	[InlineData(8L)]
	[InlineData(10L)]
	public void RejectMissingOrDifferentStoredLength(long? size)
	{
		Should.Throw<InvalidDataException>(() => GcsArchiveIntegrity.Validate("123456789"u8, (ulong?)size, "4waSgw==", CancellationToken.None));
	}

	[Fact]
	public void RejectEverySingleBitCorruptionOfKnownVector()
	{
		var original = "123456789"u8.ToArray();
		for (var bit = 0; bit < original.Length * 8; bit++)
		{
			var altered = original.ToArray();
			altered[bit / 8] ^= (byte)(1 << (bit % 8));
			Should.Throw<InvalidDataException>(() => GcsArchiveIntegrity.Validate(altered, 9, "4waSgw==", CancellationToken.None));
		}
	}

	[Fact]
	public void HonorCancellationEvenForEmptyContent()
	{
		using var cancelled = new CancellationTokenSource();
		cancelled.Cancel();
		Should.Throw<OperationCanceledException>(() => GcsArchiveIntegrity.Validate([], 0, "AAAAAA==", cancelled.Token));
	}
}
