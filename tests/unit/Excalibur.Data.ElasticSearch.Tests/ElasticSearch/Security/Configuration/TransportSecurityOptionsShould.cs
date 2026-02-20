// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.Security;

namespace Excalibur.Data.Tests.ElasticSearch.Security.Configuration;

[Trait("Category", "Unit")]
[Trait("Component", "Data")]
public sealed class TransportSecurityOptionsShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		var sut = new TransportSecurityOptions();

		sut.EnforceEncryption.ShouldBeTrue();
		sut.MinimumTlsVersion.ShouldBe("1.2");
		sut.AllowedCipherSuites.ShouldNotBeNull();
		sut.AllowedCipherSuites.ShouldBeEmpty();
		sut.RequirePerfectForwardSecrecy.ShouldBeTrue();
		sut.ValidateHostnames.ShouldBeTrue();
		sut.UseCertificatePinning.ShouldBeFalse();
		sut.PinnedCertificates.ShouldNotBeNull();
		sut.PinnedCertificates.ShouldBeEmpty();
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		var sut = new TransportSecurityOptions
		{
			EnforceEncryption = false,
			MinimumTlsVersion = "1.3",
			AllowedCipherSuites = ["TLS_AES_256_GCM_SHA384"],
			RequirePerfectForwardSecrecy = false,
			ValidateHostnames = false,
			UseCertificatePinning = true,
			PinnedCertificates = ["AABB1122"],
		};

		sut.EnforceEncryption.ShouldBeFalse();
		sut.MinimumTlsVersion.ShouldBe("1.3");
		sut.AllowedCipherSuites.ShouldContain("TLS_AES_256_GCM_SHA384");
		sut.RequirePerfectForwardSecrecy.ShouldBeFalse();
		sut.ValidateHostnames.ShouldBeFalse();
		sut.UseCertificatePinning.ShouldBeTrue();
		sut.PinnedCertificates.ShouldContain("AABB1122");
	}
}
