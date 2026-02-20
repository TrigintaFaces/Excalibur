// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch;

namespace Excalibur.Data.Tests.ElasticSearch;

[Trait("Category", "Unit")]
[Trait("Component", "Data")]
public sealed class ConnectionPoolTypeShould
{
	[Fact]
	public void DefineExpectedValues()
	{
		ConnectionPoolType.Static.ShouldBe((ConnectionPoolType)0);
		ConnectionPoolType.Sniffing.ShouldBe((ConnectionPoolType)1);
	}

	[Fact]
	public void HaveExactlyTwoMembers()
	{
		Enum.GetValues<ConnectionPoolType>().Length.ShouldBe(2);
	}
}
