// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc;
using Excalibur.Data.SqlServer.Cdc;

namespace Excalibur.Data.Tests.SqlServer.Cdc;

[Trait("Category", "Unit")]
[Trait("Component", "Data.SqlServer")]
public sealed class DataChangeTypeExtensionsShould
{
	[Theory]
	[InlineData(DataChangeType.Insert, CdcChangeType.Insert)]
	[InlineData(DataChangeType.Update, CdcChangeType.Update)]
	[InlineData(DataChangeType.Delete, CdcChangeType.Delete)]
	[InlineData(DataChangeType.Unknown, CdcChangeType.None)]
	public void ConvertToCanonicalCdcChangeType(DataChangeType input, CdcChangeType expected)
	{
		input.ToCdcChangeType().ShouldBe(expected);
	}

	[Fact]
	public void ReturnNoneForUndefinedEnumValue()
	{
		((DataChangeType)99).ToCdcChangeType().ShouldBe(CdcChangeType.None);
	}
}
