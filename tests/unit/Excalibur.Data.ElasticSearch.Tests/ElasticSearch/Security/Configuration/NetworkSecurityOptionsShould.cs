// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.Security;

namespace Excalibur.Data.Tests.ElasticSearch.Security.Configuration;

[Trait("Category", "Unit")]
[Trait("Component", "Data")]
public sealed class NetworkSecurityOptionsShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		var sut = new NetworkSecurityOptions();

		sut.Enabled.ShouldBeTrue();
		sut.IpWhitelist.ShouldNotBeNull();
		sut.IpWhitelist.ShouldBeEmpty();
		sut.IpBlacklist.ShouldNotBeNull();
		sut.IpBlacklist.ShouldBeEmpty();
		sut.RequirePrivateNetwork.ShouldBeFalse();
		sut.AllowedNetworkInterfaces.ShouldNotBeNull();
		sut.AllowedNetworkInterfaces.ShouldBeEmpty();
		sut.Firewall.ShouldNotBeNull();
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		var fw = new FirewallOptions();

		var sut = new NetworkSecurityOptions
		{
			Enabled = false,
			IpWhitelist = ["10.0.0.0/8"],
			IpBlacklist = ["192.168.1.100"],
			RequirePrivateNetwork = true,
			AllowedNetworkInterfaces = ["eth0"],
			Firewall = fw,
		};

		sut.Enabled.ShouldBeFalse();
		sut.IpWhitelist.ShouldContain("10.0.0.0/8");
		sut.IpBlacklist.ShouldContain("192.168.1.100");
		sut.RequirePrivateNetwork.ShouldBeTrue();
		sut.AllowedNetworkInterfaces.ShouldContain("eth0");
		sut.Firewall.ShouldBeSameAs(fw);
	}
}
