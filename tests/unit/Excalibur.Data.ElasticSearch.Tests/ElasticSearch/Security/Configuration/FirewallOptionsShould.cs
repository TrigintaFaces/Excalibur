// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.Security;

namespace Excalibur.Data.Tests.ElasticSearch.Security.Configuration;

[Trait("Category", "Unit")]
[Trait("Component", "Data")]
public sealed class FirewallOptionsShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		var sut = new FirewallOptions();

		sut.Enabled.ShouldBeFalse();
		sut.Provider.ShouldBe(FirewallProvider.WindowsFirewall);
		sut.AutoCreateRules.ShouldBeTrue();
		sut.DefaultAction.ShouldBe(FirewallAction.Deny);
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		var sut = new FirewallOptions
		{
			Enabled = true,
			Provider = FirewallProvider.CloudProvider,
			AutoCreateRules = false,
			DefaultAction = FirewallAction.Allow,
		};

		sut.Enabled.ShouldBeTrue();
		sut.Provider.ShouldBe(FirewallProvider.CloudProvider);
		sut.AutoCreateRules.ShouldBeFalse();
		sut.DefaultAction.ShouldBe(FirewallAction.Allow);
	}
}
