// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.Security;

namespace Excalibur.Data.Tests.ElasticSearch.Security.Auditing;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Data)]
public sealed class DataClassificationShould
{
	[Fact]
	public void DefineExpectedValues()
	{
		ElasticSearchDataClassification.Public.ShouldBe((ElasticSearchDataClassification)0);
		ElasticSearchDataClassification.Internal.ShouldBe((ElasticSearchDataClassification)1);
		ElasticSearchDataClassification.Confidential.ShouldBe((ElasticSearchDataClassification)2);
		ElasticSearchDataClassification.Restricted.ShouldBe((ElasticSearchDataClassification)3);
		ElasticSearchDataClassification.PersonallyIdentifiable.ShouldBe((ElasticSearchDataClassification)4);
		ElasticSearchDataClassification.HealthInformation.ShouldBe((ElasticSearchDataClassification)5);
	}

	[Fact]
	public void HaveExactlySixMembers()
	{
		Enum.GetValues<ElasticSearchDataClassification>().Length.ShouldBe(6);
	}
}
