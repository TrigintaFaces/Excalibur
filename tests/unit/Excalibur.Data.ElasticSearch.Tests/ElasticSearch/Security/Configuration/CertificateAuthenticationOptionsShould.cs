// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.Security;

namespace Excalibur.Data.Tests.ElasticSearch.Security.Configuration;

[Trait("Category", "Unit")]
[Trait("Component", "Data")]
public sealed class CertificateAuthenticationOptionsShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		var sut = new CertificateAuthenticationOptions();

		sut.Enabled.ShouldBeFalse();
		sut.CertificateStore.ShouldBeNull();
		sut.CertificateIdentifier.ShouldBeNull();
		sut.CertificateFilePath.ShouldBeNull();
		sut.ValidateCertificateChain.ShouldBeTrue();
		sut.CheckCertificateRevocation.ShouldBeTrue();
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		var sut = new CertificateAuthenticationOptions
		{
			Enabled = true,
			CertificateStore = "CurrentUser/My",
			CertificateIdentifier = "CN=client.elastic.local",
			CertificateFilePath = "/certs/client.pfx",
			ValidateCertificateChain = false,
			CheckCertificateRevocation = false,
		};

		sut.Enabled.ShouldBeTrue();
		sut.CertificateStore.ShouldBe("CurrentUser/My");
		sut.CertificateIdentifier.ShouldBe("CN=client.elastic.local");
		sut.CertificateFilePath.ShouldBe("/certs/client.pfx");
		sut.ValidateCertificateChain.ShouldBeFalse();
		sut.CheckCertificateRevocation.ShouldBeFalse();
	}
}
