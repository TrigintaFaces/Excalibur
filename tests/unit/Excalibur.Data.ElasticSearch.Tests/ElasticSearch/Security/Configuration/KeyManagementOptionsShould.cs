// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.Security;

namespace Excalibur.Data.Tests.ElasticSearch.Security.Configuration;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Data)]
public sealed class KeyManagementOptionsShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		var sut = new KeyManagementOptions();

		sut.Provider.ShouldBe(KeyManagementProvider.Local);
		sut.KeyRotationInterval.ShouldBe(TimeSpan.FromDays(90));
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		var sut = new KeyManagementOptions
		{
			Provider = KeyManagementProvider.AzureKeyVault,
			KeyRotationInterval = TimeSpan.FromDays(30),
		};

		sut.Provider.ShouldBe(KeyManagementProvider.AzureKeyVault);
		sut.KeyRotationInterval.ShouldBe(TimeSpan.FromDays(30));
	}
}
