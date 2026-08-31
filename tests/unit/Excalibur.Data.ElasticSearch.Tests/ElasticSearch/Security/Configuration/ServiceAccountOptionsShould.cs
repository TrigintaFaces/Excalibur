// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.Security;

namespace Excalibur.Data.Tests.ElasticSearch.Security.Configuration;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Data)]
public sealed class ServiceAccountOptionsShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		var sut = new ServiceAccountOptions();

		sut.Enabled.ShouldBeFalse();
		sut.AccountId.ShouldBeNull();
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		var sut = new ServiceAccountOptions
		{
			Enabled = true,
			AccountId = "svc-elastic-reader",
		};

		sut.Enabled.ShouldBeTrue();
		sut.AccountId.ShouldBe("svc-elastic-reader");
	}
}
