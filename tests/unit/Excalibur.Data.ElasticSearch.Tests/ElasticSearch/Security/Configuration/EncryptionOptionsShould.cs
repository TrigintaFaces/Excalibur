// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.Security;

namespace Excalibur.Data.Tests.ElasticSearch.Security.Configuration;

[Trait("Category", "Unit")]
[Trait("Component", "Data")]
public sealed class EncryptionOptionsShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		var sut = new EncryptionOptions();

		sut.FieldLevelEncryption.ShouldBeFalse();
		sut.DocumentLevelSecurity.ShouldBeFalse();
		sut.EncryptionAlgorithm.ShouldBe("AES-256-GCM");
		sut.KeyManagement.ShouldNotBeNull();
		sut.ClassificationRules.ShouldNotBeNull();
		sut.ClassificationRules.ShouldBeEmpty();
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		var km = new KeyManagementOptions();
		var rules = new List<DataClassificationRule>
		{
			new() { FieldPattern = "email*" },
		};

		var sut = new EncryptionOptions
		{
			FieldLevelEncryption = true,
			DocumentLevelSecurity = true,
			EncryptionAlgorithm = "AES-128-CBC",
			KeyManagement = km,
			ClassificationRules = rules,
		};

		sut.FieldLevelEncryption.ShouldBeTrue();
		sut.DocumentLevelSecurity.ShouldBeTrue();
		sut.EncryptionAlgorithm.ShouldBe("AES-128-CBC");
		sut.KeyManagement.ShouldBeSameAs(km);
		sut.ClassificationRules.ShouldBeSameAs(rules);
	}
}
