// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.Security;

namespace Excalibur.Data.Tests.ElasticSearch.Security.Configuration;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Data)]
public sealed class AuthenticationOptionsShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		var sut = new AuthenticationOptions();

		sut.Username.ShouldBeNull();
		sut.ApiKeyId.ShouldBeNull();
		sut.Base64ApiKey.ShouldBeNull();
		sut.OAuth2.ShouldNotBeNull();
		sut.ServiceAccount.ShouldNotBeNull();
		sut.CredentialRotation.ShouldNotBeNull();
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		var oauth = new OAuth2Options();
		var sa = new ServiceAccountOptions();
		var rotation = new CredentialRotationOptions();

		var sut = new AuthenticationOptions
		{
			Username = "elastic",
			ApiKeyId = "key-id-123",
			Base64ApiKey = "dGVzdA==",
			OAuth2 = oauth,
			ServiceAccount = sa,
			CredentialRotation = rotation,
		};

		sut.Username.ShouldBe("elastic");
		sut.ApiKeyId.ShouldBe("key-id-123");
		sut.Base64ApiKey.ShouldBe("dGVzdA==");
		sut.OAuth2.ShouldBeSameAs(oauth);
		sut.ServiceAccount.ShouldBeSameAs(sa);
		sut.CredentialRotation.ShouldBeSameAs(rotation);
	}
}
