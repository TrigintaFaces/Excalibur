// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.Persistence;

namespace Excalibur.Data.Tests.ElasticSearch.Persistence;

[Trait("Category", "Unit")]
[Trait("Component", "Data")]
public sealed class ElasticsearchRefreshPolicyShould
{
	[Fact]
	public void DefineExpectedValues()
	{
		ElasticsearchRefreshPolicy.None.ShouldBe((ElasticsearchRefreshPolicy)0);
		ElasticsearchRefreshPolicy.WaitFor.ShouldBe((ElasticsearchRefreshPolicy)1);
		ElasticsearchRefreshPolicy.Immediate.ShouldBe((ElasticsearchRefreshPolicy)2);
	}

	[Fact]
	public void HaveExactlyThreeMembers()
	{
		Enum.GetValues<ElasticsearchRefreshPolicy>().Length.ShouldBe(3);
	}
}
