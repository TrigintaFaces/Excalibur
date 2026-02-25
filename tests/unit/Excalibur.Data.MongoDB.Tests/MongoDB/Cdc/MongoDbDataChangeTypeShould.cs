// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc;
using Excalibur.Data.MongoDB.Cdc;

namespace Excalibur.Data.Tests.MongoDB.Cdc;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class MongoDbDataChangeTypeShould
{
	[Theory]
	[InlineData(MongoDbDataChangeType.Unknown, 0)]
	[InlineData(MongoDbDataChangeType.Insert, 1)]
	[InlineData(MongoDbDataChangeType.Update, 2)]
	[InlineData(MongoDbDataChangeType.Replace, 3)]
	[InlineData(MongoDbDataChangeType.Delete, 4)]
	[InlineData(MongoDbDataChangeType.Invalidate, 5)]
	[InlineData(MongoDbDataChangeType.Drop, 6)]
	[InlineData(MongoDbDataChangeType.DropDatabase, 7)]
	[InlineData(MongoDbDataChangeType.Rename, 8)]
	public void HaveCorrectValues(MongoDbDataChangeType type, int expected)
	{
		((int)type).ShouldBe(expected);
	}

	[Theory]
	[InlineData(MongoDbDataChangeType.Insert, CdcChangeType.Insert)]
	[InlineData(MongoDbDataChangeType.Update, CdcChangeType.Update)]
	[InlineData(MongoDbDataChangeType.Replace, CdcChangeType.Replace)]
	[InlineData(MongoDbDataChangeType.Delete, CdcChangeType.Delete)]
	[InlineData(MongoDbDataChangeType.Invalidate, CdcChangeType.Invalidate)]
	[InlineData(MongoDbDataChangeType.Drop, CdcChangeType.Drop)]
	[InlineData(MongoDbDataChangeType.DropDatabase, CdcChangeType.DropDatabase)]
	[InlineData(MongoDbDataChangeType.Rename, CdcChangeType.Rename)]
	[InlineData(MongoDbDataChangeType.Unknown, CdcChangeType.None)]
	public void MapToCdcChangeType(MongoDbDataChangeType mongoType, CdcChangeType expectedCdcType)
	{
		mongoType.ToCdcChangeType().ShouldBe(expectedCdcType);
	}
}
