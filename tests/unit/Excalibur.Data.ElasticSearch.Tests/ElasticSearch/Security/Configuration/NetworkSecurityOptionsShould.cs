// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.Security;

namespace Excalibur.Data.Tests.ElasticSearch.Security.Configuration;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Data)]
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
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		var sut = new NetworkSecurityOptions
		{
			Enabled = false,
			IpWhitelist = ["10.0.0.0/8"],
			IpBlacklist = ["192.168.1.100"],
		};

		sut.Enabled.ShouldBeFalse();
		sut.IpWhitelist.ShouldContain("10.0.0.0/8");
		sut.IpBlacklist.ShouldContain("192.168.1.100");
	}
}
