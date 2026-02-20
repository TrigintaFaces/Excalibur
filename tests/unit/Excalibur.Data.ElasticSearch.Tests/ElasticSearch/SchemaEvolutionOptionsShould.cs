// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch;

namespace Excalibur.Data.Tests.ElasticSearch;

[Trait("Category", "Unit")]
[Trait("Component", "Data")]
public sealed class SchemaEvolutionOptionsShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		var sut = new SchemaEvolutionOptions();

		sut.Enabled.ShouldBeTrue();
		sut.ValidateBackwardsCompatibility.ShouldBeTrue();
		sut.AllowBreakingChanges.ShouldBeFalse();
		sut.DefaultMigrationStrategy.ShouldBe("AliasSwitch");
		sut.AutoBackup.ShouldBeTrue();
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		var sut = new SchemaEvolutionOptions
		{
			Enabled = false,
			ValidateBackwardsCompatibility = false,
			AllowBreakingChanges = true,
			DefaultMigrationStrategy = "Reindex",
			AutoBackup = false,
		};

		sut.Enabled.ShouldBeFalse();
		sut.ValidateBackwardsCompatibility.ShouldBeFalse();
		sut.AllowBreakingChanges.ShouldBeTrue();
		sut.DefaultMigrationStrategy.ShouldBe("Reindex");
		sut.AutoBackup.ShouldBeFalse();
	}
}
