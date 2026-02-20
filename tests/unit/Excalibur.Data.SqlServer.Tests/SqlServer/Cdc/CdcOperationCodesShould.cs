// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.SqlServer.Cdc;

namespace Excalibur.Data.Tests.SqlServer.Cdc;

[Trait("Category", "Unit")]
[Trait("Component", "Data.SqlServer")]
public sealed class CdcOperationCodesShould
{
	[Theory]
	[InlineData(CdcOperationCodes.Unknown, 0)]
	[InlineData(CdcOperationCodes.Delete, 1)]
	[InlineData(CdcOperationCodes.Insert, 2)]
	[InlineData(CdcOperationCodes.UpdateBefore, 3)]
	[InlineData(CdcOperationCodes.UpdateAfter, 4)]
	public void MapToCorrectIntegerValues(CdcOperationCodes code, int expected)
	{
		((int)code).ShouldBe(expected);
	}

	[Fact]
	public void DefaultToUnknown()
	{
		default(CdcOperationCodes).ShouldBe(CdcOperationCodes.Unknown);
	}

	[Fact]
	public void HaveFiveDefinedValues()
	{
		Enum.GetValues<CdcOperationCodes>().Length.ShouldBe(5);
	}
}
