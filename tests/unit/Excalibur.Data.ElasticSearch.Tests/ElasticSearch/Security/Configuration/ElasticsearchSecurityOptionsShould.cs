// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.Security;

namespace Excalibur.Data.Tests.ElasticSearch.Security.Configuration;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Data)]
public sealed class ElasticsearchSecurityOptionsShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		var sut = new ElasticsearchSecurityOptions();

		sut.Enabled.ShouldBeTrue();
		sut.Mode.ShouldBe(SecurityMode.Strict);
		sut.Authentication.ShouldNotBeNull();
		sut.Encryption.ShouldNotBeNull();
		sut.NetworkSecurity.ShouldNotBeNull();
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		var auth = new AuthenticationOptions();
		var encryption = new EncryptionOptions();
		var network = new NetworkSecurityOptions();

		var sut = new ElasticsearchSecurityOptions
		{
			Enabled = false,
			Mode = SecurityMode.Permissive,
			Authentication = auth,
			Encryption = encryption,
			NetworkSecurity = network,
		};

		sut.Enabled.ShouldBeFalse();
		sut.Mode.ShouldBe(SecurityMode.Permissive);
		sut.Authentication.ShouldBeSameAs(auth);
		sut.Encryption.ShouldBeSameAs(encryption);
		sut.NetworkSecurity.ShouldBeSameAs(network);
	}
}
