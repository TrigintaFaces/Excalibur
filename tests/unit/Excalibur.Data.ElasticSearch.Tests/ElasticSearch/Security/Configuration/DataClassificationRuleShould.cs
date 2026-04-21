// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.Security;

namespace Excalibur.Data.Tests.ElasticSearch.Security.Configuration;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Data)]
public sealed class DataClassificationRuleShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		var sut = new DataClassificationRule();

		sut.FieldPattern.ShouldBe(string.Empty);
		sut.Classification.ShouldBe(ElasticSearchDataClassification.Public);
		sut.Enabled.ShouldBeTrue();
		sut.EncryptionAlgorithm.ShouldBeNull();
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		var sut = new DataClassificationRule
		{
			FieldPattern = @"^(email|ssn|phone)$",
			Classification = ElasticSearchDataClassification.PersonallyIdentifiable,
			Enabled = false,
			EncryptionAlgorithm = "AES-256-GCM",
		};

		sut.FieldPattern.ShouldBe(@"^(email|ssn|phone)$");
		sut.Classification.ShouldBe(ElasticSearchDataClassification.PersonallyIdentifiable);
		sut.Enabled.ShouldBeFalse();
		sut.EncryptionAlgorithm.ShouldBe("AES-256-GCM");
	}
}
