// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.Projections;

namespace Excalibur.Data.Tests.ElasticSearch.Projections;

/// <summary>
/// Unit tests for the <see cref="MigrationStrategy"/> enum.
/// </summary>
/// <remarks>
/// Sprint 513 (S513.2): Elasticsearch Phase 2 unit tests.
/// Tests verify enum values for schema migration strategies.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Elasticsearch")]
[Trait("Feature", "Projections")]
public sealed class MigrationStrategyShould
{
	#region Enum Value Tests

	[Fact]
	public void DefineReindexAsZero()
	{
		// Assert
		((int)MigrationStrategy.Reindex).ShouldBe(0);
	}

	[Fact]
	public void DefineUpdateInPlaceAsOne()
	{
		// Assert
		((int)MigrationStrategy.UpdateInPlace).ShouldBe(1);
	}

	[Fact]
	public void DefineAliasSwitchAsTwo()
	{
		// Assert
		((int)MigrationStrategy.AliasSwitch).ShouldBe(2);
	}

	[Fact]
	public void DefineDualWriteAsThree()
	{
		// Assert
		((int)MigrationStrategy.DualWrite).ShouldBe(3);
	}

	#endregion

	#region Enum Count Tests

	[Fact]
	public void HaveFourDefinedValues()
	{
		// Act
		var values = Enum.GetValues<MigrationStrategy>();

		// Assert
		values.Length.ShouldBe(4);
	}

	[Fact]
	public void ContainAllExpectedStrategies()
	{
		// Act
		var values = Enum.GetValues<MigrationStrategy>();

		// Assert
		values.ShouldContain(MigrationStrategy.Reindex);
		values.ShouldContain(MigrationStrategy.UpdateInPlace);
		values.ShouldContain(MigrationStrategy.AliasSwitch);
		values.ShouldContain(MigrationStrategy.DualWrite);
	}

	#endregion

	#region Enum Parse Tests

	[Theory]
	[InlineData("Reindex", MigrationStrategy.Reindex)]
	[InlineData("UpdateInPlace", MigrationStrategy.UpdateInPlace)]
	[InlineData("AliasSwitch", MigrationStrategy.AliasSwitch)]
	[InlineData("DualWrite", MigrationStrategy.DualWrite)]
	public void ParseFromString_WithValidName(string name, MigrationStrategy expected)
	{
		// Act
		var result = Enum.Parse<MigrationStrategy>(name);

		// Assert
		result.ShouldBe(expected);
	}

	[Theory]
	[InlineData("reindex", MigrationStrategy.Reindex)]
	[InlineData("UPDATEINPLACE", MigrationStrategy.UpdateInPlace)]
	[InlineData("aliasswitch", MigrationStrategy.AliasSwitch)]
	[InlineData("DUALWRITE", MigrationStrategy.DualWrite)]
	public void ParseFromString_WithCaseInsensitiveMatch(string name, MigrationStrategy expected)
	{
		// Act
		var result = Enum.Parse<MigrationStrategy>(name, ignoreCase: true);

		// Assert
		result.ShouldBe(expected);
	}

	#endregion

	#region Enum Name Tests

	[Fact]
	public void HaveCorrectNameForReindex()
	{
		// Assert
		MigrationStrategy.Reindex.ToString().ShouldBe("Reindex");
	}

	[Fact]
	public void HaveCorrectNameForUpdateInPlace()
	{
		// Assert
		MigrationStrategy.UpdateInPlace.ToString().ShouldBe("UpdateInPlace");
	}

	[Fact]
	public void HaveCorrectNameForAliasSwitch()
	{
		// Assert
		MigrationStrategy.AliasSwitch.ToString().ShouldBe("AliasSwitch");
	}

	[Fact]
	public void HaveCorrectNameForDualWrite()
	{
		// Assert
		MigrationStrategy.DualWrite.ToString().ShouldBe("DualWrite");
	}

	#endregion
}
