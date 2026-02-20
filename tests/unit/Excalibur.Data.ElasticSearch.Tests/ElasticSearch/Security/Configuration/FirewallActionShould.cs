// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.Security;

namespace Excalibur.Data.Tests.ElasticSearch.Security.Configuration;

[Trait("Category", "Unit")]
[Trait("Component", "Data")]
public sealed class FirewallActionShould
{
	[Fact]
	public void DefineExpectedValues()
	{
		FirewallAction.Allow.ShouldBe((FirewallAction)0);
		FirewallAction.Deny.ShouldBe((FirewallAction)1);
		FirewallAction.Reject.ShouldBe((FirewallAction)2);
	}

	[Fact]
	public void HaveExactlyThreeMembers()
	{
		Enum.GetValues<FirewallAction>().Length.ShouldBe(3);
	}
}
