// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.Security;

namespace Excalibur.Data.Tests.ElasticSearch.Security.Auditing;

[Trait("Category", "Unit")]
[Trait("Component", "Data")]
public sealed class DataClassificationShould
{
	[Fact]
	public void DefineExpectedValues()
	{
		DataClassification.Public.ShouldBe((DataClassification)0);
		DataClassification.Internal.ShouldBe((DataClassification)1);
		DataClassification.Confidential.ShouldBe((DataClassification)2);
		DataClassification.Restricted.ShouldBe((DataClassification)3);
		DataClassification.PersonallyIdentifiable.ShouldBe((DataClassification)4);
		DataClassification.HealthInformation.ShouldBe((DataClassification)5);
	}

	[Fact]
	public void HaveExactlySixMembers()
	{
		Enum.GetValues<DataClassification>().Length.ShouldBe(6);
	}
}
