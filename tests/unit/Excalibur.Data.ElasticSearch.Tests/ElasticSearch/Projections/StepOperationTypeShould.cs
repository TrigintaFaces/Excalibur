// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.Projections;

namespace Excalibur.Data.Tests.ElasticSearch.Projections;

/// <summary>
/// Unit tests for the <see cref="StepOperationType"/> enum.
/// </summary>
/// <remarks>
/// Sprint 513 (S513.2): Elasticsearch Phase 2 unit tests.
/// Tests verify enum values for migration step operation types.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Elasticsearch")]
[Trait("Feature", "Projections")]
public sealed class StepOperationTypeShould
{
	#region Enum Value Tests

	[Fact]
	public void DefineCreateIndexAsZero()
	{
		// Assert
		((int)StepOperationType.CreateIndex).ShouldBe(0);
	}

	[Fact]
	public void DefineUpdateMappingAsOne()
	{
		// Assert
		((int)StepOperationType.UpdateMapping).ShouldBe(1);
	}

	[Fact]
	public void DefineReindexAsTwo()
	{
		// Assert
		((int)StepOperationType.Reindex).ShouldBe(2);
	}

	[Fact]
	public void DefineTransformAsThree()
	{
		// Assert
		((int)StepOperationType.Transform).ShouldBe(3);
	}

	[Fact]
	public void DefineValidateAsFour()
	{
		// Assert
		((int)StepOperationType.Validate).ShouldBe(4);
	}

	[Fact]
	public void DefineSwitchAliasAsFive()
	{
		// Assert
		((int)StepOperationType.SwitchAlias).ShouldBe(5);
	}

	[Fact]
	public void DefineDeleteIndexAsSix()
	{
		// Assert
		((int)StepOperationType.DeleteIndex).ShouldBe(6);
	}

	[Fact]
	public void DefineBackupAsSeven()
	{
		// Assert
		((int)StepOperationType.Backup).ShouldBe(7);
	}

	#endregion

	#region Enum Count Tests

	[Fact]
	public void HaveEightDefinedValues()
	{
		// Act
		var values = Enum.GetValues<StepOperationType>();

		// Assert
		values.Length.ShouldBe(8);
	}

	[Fact]
	public void ContainAllExpectedOperationTypes()
	{
		// Act
		var values = Enum.GetValues<StepOperationType>();

		// Assert
		values.ShouldContain(StepOperationType.CreateIndex);
		values.ShouldContain(StepOperationType.UpdateMapping);
		values.ShouldContain(StepOperationType.Reindex);
		values.ShouldContain(StepOperationType.Transform);
		values.ShouldContain(StepOperationType.Validate);
		values.ShouldContain(StepOperationType.SwitchAlias);
		values.ShouldContain(StepOperationType.DeleteIndex);
		values.ShouldContain(StepOperationType.Backup);
	}

	#endregion

	#region Enum Parse Tests

	[Theory]
	[InlineData("CreateIndex", StepOperationType.CreateIndex)]
	[InlineData("UpdateMapping", StepOperationType.UpdateMapping)]
	[InlineData("Reindex", StepOperationType.Reindex)]
	[InlineData("Transform", StepOperationType.Transform)]
	[InlineData("Validate", StepOperationType.Validate)]
	[InlineData("SwitchAlias", StepOperationType.SwitchAlias)]
	[InlineData("DeleteIndex", StepOperationType.DeleteIndex)]
	[InlineData("Backup", StepOperationType.Backup)]
	public void ParseFromString_WithValidName(string name, StepOperationType expected)
	{
		// Act
		var result = Enum.Parse<StepOperationType>(name);

		// Assert
		result.ShouldBe(expected);
	}

	[Theory]
	[InlineData("createindex", StepOperationType.CreateIndex)]
	[InlineData("UPDATEMAPPING", StepOperationType.UpdateMapping)]
	[InlineData("reindex", StepOperationType.Reindex)]
	public void ParseFromString_WithCaseInsensitiveMatch(string name, StepOperationType expected)
	{
		// Act
		var result = Enum.Parse<StepOperationType>(name, ignoreCase: true);

		// Assert
		result.ShouldBe(expected);
	}

	#endregion

	#region Enum Name Tests

	[Fact]
	public void HaveCorrectNameForCreateIndex()
	{
		// Assert
		StepOperationType.CreateIndex.ToString().ShouldBe("CreateIndex");
	}

	[Fact]
	public void HaveCorrectNameForSwitchAlias()
	{
		// Assert
		StepOperationType.SwitchAlias.ToString().ShouldBe("SwitchAlias");
	}

	[Fact]
	public void HaveCorrectNameForBackup()
	{
		// Assert
		StepOperationType.Backup.ToString().ShouldBe("Backup");
	}

	#endregion
}
