// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.Projections;

namespace Excalibur.Data.Tests.ElasticSearch.Projections;

/// <summary>
/// Unit tests for the <see cref="CompatibilityLevel"/> enum.
/// </summary>
/// <remarks>
/// Sprint 513 (S513.2): Elasticsearch Phase 2 unit tests.
/// Tests verify enum values for schema compatibility levels.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Elasticsearch")]
[Trait("Feature", "Projections")]
public sealed class CompatibilityLevelShould
{
	#region Enum Value Tests

	[Fact]
	public void DefineFullAsZero()
	{
		// Assert
		((int)CompatibilityLevel.Full).ShouldBe(0);
	}

	[Fact]
	public void DefinePartialAsOne()
	{
		// Assert
		((int)CompatibilityLevel.Partial).ShouldBe(1);
	}

	[Fact]
	public void DefineNoneAsTwo()
	{
		// Assert
		((int)CompatibilityLevel.None).ShouldBe(2);
	}

	#endregion

	#region Enum Count Tests

	[Fact]
	public void HaveThreeDefinedValues()
	{
		// Act
		var values = Enum.GetValues<CompatibilityLevel>();

		// Assert
		values.Length.ShouldBe(3);
	}

	[Fact]
	public void ContainAllExpectedLevels()
	{
		// Act
		var values = Enum.GetValues<CompatibilityLevel>();

		// Assert
		values.ShouldContain(CompatibilityLevel.Full);
		values.ShouldContain(CompatibilityLevel.Partial);
		values.ShouldContain(CompatibilityLevel.None);
	}

	#endregion

	#region Enum Parse Tests

	[Theory]
	[InlineData("Full", CompatibilityLevel.Full)]
	[InlineData("Partial", CompatibilityLevel.Partial)]
	[InlineData("None", CompatibilityLevel.None)]
	public void ParseFromString_WithValidName(string name, CompatibilityLevel expected)
	{
		// Act
		var result = Enum.Parse<CompatibilityLevel>(name);

		// Assert
		result.ShouldBe(expected);
	}

	[Theory]
	[InlineData("full", CompatibilityLevel.Full)]
	[InlineData("PARTIAL", CompatibilityLevel.Partial)]
	[InlineData("none", CompatibilityLevel.None)]
	public void ParseFromString_WithCaseInsensitiveMatch(string name, CompatibilityLevel expected)
	{
		// Act
		var result = Enum.Parse<CompatibilityLevel>(name, ignoreCase: true);

		// Assert
		result.ShouldBe(expected);
	}

	#endregion

	#region Enum Name Tests

	[Fact]
	public void HaveCorrectNameForFull()
	{
		// Assert
		CompatibilityLevel.Full.ToString().ShouldBe("Full");
	}

	[Fact]
	public void HaveCorrectNameForPartial()
	{
		// Assert
		CompatibilityLevel.Partial.ToString().ShouldBe("Partial");
	}

	[Fact]
	public void HaveCorrectNameForNone()
	{
		// Assert
		CompatibilityLevel.None.ToString().ShouldBe("None");
	}

	#endregion

	#region Compatibility Ordering Tests

	[Fact]
	public void FullShouldBeHighestCompatibility()
	{
		// Assert - Full = 0 is the best compatibility
		CompatibilityLevel.Full.ShouldBe(Enum.GetValues<CompatibilityLevel>().Min());
	}

	[Fact]
	public void NoneShouldBeLowestCompatibility()
	{
		// Assert - None = 2 is the worst compatibility
		CompatibilityLevel.None.ShouldBe(Enum.GetValues<CompatibilityLevel>().Max());
	}

	[Fact]
	public void HaveCompatibilityValuesInDecreasingOrder()
	{
		// Assert - Verify compatibility values decrease (Full > Partial > None)
		((int)CompatibilityLevel.Full).ShouldBeLessThan((int)CompatibilityLevel.Partial);
		((int)CompatibilityLevel.Partial).ShouldBeLessThan((int)CompatibilityLevel.None);
	}

	#endregion
}
