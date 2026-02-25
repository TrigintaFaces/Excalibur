// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.IndexManagement;

namespace Excalibur.Data.Tests.ElasticSearch.IndexManagement;

/// <summary>
/// Unit tests for the <see cref="AliasOperationType"/> enum.
/// </summary>
/// <remarks>
/// Sprint 514 (S514.2): IndexManagement unit tests.
/// Tests verify enum values for alias operation types.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Elasticsearch")]
[Trait("Feature", "IndexManagement")]
public sealed class AliasOperationTypeShould
{
	#region Enum Value Tests

	[Fact]
	public void DefineAddAsZero()
	{
		// Assert
		((int)AliasOperationType.Add).ShouldBe(0);
	}

	[Fact]
	public void DefineRemoveAsOne()
	{
		// Assert
		((int)AliasOperationType.Remove).ShouldBe(1);
	}

	#endregion

	#region Enum Count Tests

	[Fact]
	public void HaveTwoDefinedValues()
	{
		// Act
		var values = Enum.GetValues<AliasOperationType>();

		// Assert
		values.Length.ShouldBe(2);
	}

	[Fact]
	public void ContainAllExpectedOperationTypes()
	{
		// Act
		var values = Enum.GetValues<AliasOperationType>();

		// Assert
		values.ShouldContain(AliasOperationType.Add);
		values.ShouldContain(AliasOperationType.Remove);
	}

	#endregion

	#region Enum Parse Tests

	[Theory]
	[InlineData("Add", AliasOperationType.Add)]
	[InlineData("Remove", AliasOperationType.Remove)]
	public void ParseFromString_WithValidName(string name, AliasOperationType expected)
	{
		// Act
		var result = Enum.Parse<AliasOperationType>(name);

		// Assert
		result.ShouldBe(expected);
	}

	[Theory]
	[InlineData("add", AliasOperationType.Add)]
	[InlineData("REMOVE", AliasOperationType.Remove)]
	public void ParseFromString_WithCaseInsensitiveMatch(string name, AliasOperationType expected)
	{
		// Act
		var result = Enum.Parse<AliasOperationType>(name, ignoreCase: true);

		// Assert
		result.ShouldBe(expected);
	}

	#endregion

	#region Enum Name Tests

	[Fact]
	public void HaveCorrectNameForAdd()
	{
		// Assert
		AliasOperationType.Add.ToString().ShouldBe("Add");
	}

	[Fact]
	public void HaveCorrectNameForRemove()
	{
		// Assert
		AliasOperationType.Remove.ToString().ShouldBe("Remove");
	}

	#endregion
}
