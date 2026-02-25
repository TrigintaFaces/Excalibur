// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.Security;

namespace Excalibur.Data.Tests.ElasticSearch.Security.Configuration;

[Trait("Category", "Unit")]
[Trait("Component", "Data")]
public sealed class KeyManagementOptionsShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		var sut = new KeyManagementOptions();

		sut.Provider.ShouldBe(KeyManagementProvider.Local);
		sut.EndpointUrl.ShouldBeNull();
		sut.AuthenticationConfig.ShouldBeNull();
		sut.KeyRotationInterval.ShouldBe(TimeSpan.FromDays(90));
		sut.RequireHsm.ShouldBeFalse();
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		var sut = new KeyManagementOptions
		{
			Provider = KeyManagementProvider.AzureKeyVault,
			EndpointUrl = "https://myvault.vault.azure.net",
			AuthenticationConfig = "managed-identity",
			KeyRotationInterval = TimeSpan.FromDays(30),
			RequireHsm = true,
		};

		sut.Provider.ShouldBe(KeyManagementProvider.AzureKeyVault);
		sut.EndpointUrl.ShouldBe("https://myvault.vault.azure.net");
		sut.AuthenticationConfig.ShouldBe("managed-identity");
		sut.KeyRotationInterval.ShouldBe(TimeSpan.FromDays(30));
		sut.RequireHsm.ShouldBeTrue();
	}
}
