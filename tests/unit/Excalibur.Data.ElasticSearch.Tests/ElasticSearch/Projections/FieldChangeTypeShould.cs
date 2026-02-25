// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.Projections;

namespace Excalibur.Data.Tests.ElasticSearch.Projections;

/// <summary>
/// Unit tests for the <see cref="FieldChangeType"/> enum.
/// </summary>
/// <remarks>
/// Sprint 513 (S513.2): Elasticsearch Phase 2 unit tests.
/// Tests verify enum values for schema field change types.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Elasticsearch")]
[Trait("Feature", "Projections")]
public sealed class FieldChangeTypeShould
{
	#region Enum Value Tests

	[Fact]
	public void DefineAddedAsZero()
	{
		// Assert
		((int)FieldChangeType.Added).ShouldBe(0);
	}

	[Fact]
	public void DefineRemovedAsOne()
	{
		// Assert
		((int)FieldChangeType.Removed).ShouldBe(1);
	}

	[Fact]
	public void DefineTypeChangedAsTwo()
	{
		// Assert
		((int)FieldChangeType.TypeChanged).ShouldBe(2);
	}

	[Fact]
	public void DefinePropertiesModifiedAsThree()
	{
		// Assert
		((int)FieldChangeType.PropertiesModified).ShouldBe(3);
	}

	[Fact]
	public void DefineRenamedAsFour()
	{
		// Assert
		((int)FieldChangeType.Renamed).ShouldBe(4);
	}

	#endregion

	#region Enum Count Tests

	[Fact]
	public void HaveFiveDefinedValues()
	{
		// Act
		var values = Enum.GetValues<FieldChangeType>();

		// Assert
		values.Length.ShouldBe(5);
	}

	[Fact]
	public void ContainAllExpectedChangeTypes()
	{
		// Act
		var values = Enum.GetValues<FieldChangeType>();

		// Assert
		values.ShouldContain(FieldChangeType.Added);
		values.ShouldContain(FieldChangeType.Removed);
		values.ShouldContain(FieldChangeType.TypeChanged);
		values.ShouldContain(FieldChangeType.PropertiesModified);
		values.ShouldContain(FieldChangeType.Renamed);
	}

	#endregion

	#region Enum Parse Tests

	[Theory]
	[InlineData("Added", FieldChangeType.Added)]
	[InlineData("Removed", FieldChangeType.Removed)]
	[InlineData("TypeChanged", FieldChangeType.TypeChanged)]
	[InlineData("PropertiesModified", FieldChangeType.PropertiesModified)]
	[InlineData("Renamed", FieldChangeType.Renamed)]
	public void ParseFromString_WithValidName(string name, FieldChangeType expected)
	{
		// Act
		var result = Enum.Parse<FieldChangeType>(name);

		// Assert
		result.ShouldBe(expected);
	}

	[Theory]
	[InlineData("added", FieldChangeType.Added)]
	[InlineData("REMOVED", FieldChangeType.Removed)]
	[InlineData("typechanged", FieldChangeType.TypeChanged)]
	public void ParseFromString_WithCaseInsensitiveMatch(string name, FieldChangeType expected)
	{
		// Act
		var result = Enum.Parse<FieldChangeType>(name, ignoreCase: true);

		// Assert
		result.ShouldBe(expected);
	}

	#endregion

	#region Enum Name Tests

	[Fact]
	public void HaveCorrectNameForAdded()
	{
		// Assert
		FieldChangeType.Added.ToString().ShouldBe("Added");
	}

	[Fact]
	public void HaveCorrectNameForRemoved()
	{
		// Assert
		FieldChangeType.Removed.ToString().ShouldBe("Removed");
	}

	[Fact]
	public void HaveCorrectNameForTypeChanged()
	{
		// Assert
		FieldChangeType.TypeChanged.ToString().ShouldBe("TypeChanged");
	}

	[Fact]
	public void HaveCorrectNameForPropertiesModified()
	{
		// Assert
		FieldChangeType.PropertiesModified.ToString().ShouldBe("PropertiesModified");
	}

	[Fact]
	public void HaveCorrectNameForRenamed()
	{
		// Assert
		FieldChangeType.Renamed.ToString().ShouldBe("Renamed");
	}

	#endregion
}
