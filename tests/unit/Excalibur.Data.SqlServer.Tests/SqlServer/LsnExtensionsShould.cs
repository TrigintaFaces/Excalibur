// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.SqlServer.Cdc;

namespace Excalibur.Data.Tests.SqlServer;

[Trait("Category", "Unit")]
[Trait("Component", "Data.SqlServer")]
public sealed class LsnExtensionsShould
{
	[Fact]
	public void CompareEqualLsns()
	{
		byte[] a = [0x01, 0x02, 0x03];
		byte[] b = [0x01, 0x02, 0x03];
		a.CompareLsn(b).ShouldBe(0);
	}

	[Fact]
	public void CompareSmallerLsnAsNegative()
	{
		byte[] a = [0x01, 0x02, 0x03];
		byte[] b = [0x01, 0x02, 0x04];
		a.CompareLsn(b).ShouldBeLessThan(0);
	}

	[Fact]
	public void CompareLargerLsnAsPositive()
	{
		byte[] a = [0x01, 0x02, 0x04];
		byte[] b = [0x01, 0x02, 0x03];
		a.CompareLsn(b).ShouldBeGreaterThan(0);
	}

	[Fact]
	public void ConvertToHexString()
	{
		byte[] lsn = [0xAA, 0xBB, 0xCC];
		lsn.ByteArrayToHex().ShouldBe("0xAABBCC");
	}

	[Fact]
	public void ConvertEmptyArrayToHex()
	{
		Array.Empty<byte>().ByteArrayToHex().ShouldBe("0x");
	}

	[Fact]
	public void CompareDifferentLengthLsns()
	{
		byte[] a = [0x01, 0x02];
		byte[] b = [0x01, 0x02, 0x03];
		a.CompareLsn(b).ShouldBeLessThan(0);
	}
}
