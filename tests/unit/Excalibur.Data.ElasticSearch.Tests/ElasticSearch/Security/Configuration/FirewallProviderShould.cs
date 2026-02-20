// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.Security;

namespace Excalibur.Data.Tests.ElasticSearch.Security.Configuration;

[Trait("Category", "Unit")]
[Trait("Component", "Data")]
public sealed class FirewallProviderShould
{
	[Fact]
	public void DefineExpectedValues()
	{
		FirewallProvider.WindowsFirewall.ShouldBe((FirewallProvider)0);
		FirewallProvider.IpTables.ShouldBe((FirewallProvider)1);
		FirewallProvider.PfSense.ShouldBe((FirewallProvider)2);
		FirewallProvider.CloudProvider.ShouldBe((FirewallProvider)3);
	}

	[Fact]
	public void HaveExactlyFourMembers()
	{
		Enum.GetValues<FirewallProvider>().Length.ShouldBe(4);
	}
}
