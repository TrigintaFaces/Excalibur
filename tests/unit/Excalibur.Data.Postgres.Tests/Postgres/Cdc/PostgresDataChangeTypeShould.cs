// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc;
using Excalibur.Data.Postgres.Cdc;

namespace Excalibur.Data.Tests.Postgres.Cdc;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class PostgresDataChangeTypeShould
{
	[Theory]
	[InlineData(PostgresDataChangeType.Unknown, 0)]
	[InlineData(PostgresDataChangeType.Insert, 1)]
	[InlineData(PostgresDataChangeType.Update, 2)]
	[InlineData(PostgresDataChangeType.Delete, 3)]
	[InlineData(PostgresDataChangeType.Truncate, 4)]
	public void HaveCorrectValues(PostgresDataChangeType type, int expected)
	{
		((int)type).ShouldBe(expected);
	}

	[Theory]
	[InlineData(PostgresDataChangeType.Insert, CdcChangeType.Insert)]
	[InlineData(PostgresDataChangeType.Update, CdcChangeType.Update)]
	[InlineData(PostgresDataChangeType.Delete, CdcChangeType.Delete)]
	[InlineData(PostgresDataChangeType.Truncate, CdcChangeType.Truncate)]
	[InlineData(PostgresDataChangeType.Unknown, CdcChangeType.None)]
	public void MapToCdcChangeType(PostgresDataChangeType pgType, CdcChangeType expectedCdcType)
	{
		pgType.ToCdcChangeType().ShouldBe(expectedCdcType);
	}
}
