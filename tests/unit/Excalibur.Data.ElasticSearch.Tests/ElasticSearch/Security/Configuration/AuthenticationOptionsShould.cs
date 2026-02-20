// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.Security;

namespace Excalibur.Data.Tests.ElasticSearch.Security.Configuration;

[Trait("Category", "Unit")]
[Trait("Component", "Data")]
public sealed class AuthenticationOptionsShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		var sut = new AuthenticationOptions();

		sut.Username.ShouldBeNull();
		sut.ApiKeyId.ShouldBeNull();
		sut.Base64ApiKey.ShouldBeNull();
		sut.Certificate.ShouldNotBeNull();
		sut.OAuth2.ShouldNotBeNull();
		sut.ServiceAccount.ShouldNotBeNull();
		sut.CredentialRotation.ShouldNotBeNull();
		sut.Protection.ShouldNotBeNull();
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		var cert = new CertificateAuthenticationOptions();
		var oauth = new OAuth2Options();
		var sa = new ServiceAccountOptions();
		var rotation = new CredentialRotationOptions();
		var protection = new AuthenticationProtectionOptions();

		var sut = new AuthenticationOptions
		{
			Username = "elastic",
			ApiKeyId = "key-id-123",
			Base64ApiKey = "dGVzdA==",
			Certificate = cert,
			OAuth2 = oauth,
			ServiceAccount = sa,
			CredentialRotation = rotation,
			Protection = protection,
		};

		sut.Username.ShouldBe("elastic");
		sut.ApiKeyId.ShouldBe("key-id-123");
		sut.Base64ApiKey.ShouldBe("dGVzdA==");
		sut.Certificate.ShouldBeSameAs(cert);
		sut.OAuth2.ShouldBeSameAs(oauth);
		sut.ServiceAccount.ShouldBeSameAs(sa);
		sut.CredentialRotation.ShouldBeSameAs(rotation);
		sut.Protection.ShouldBeSameAs(protection);
	}
}
