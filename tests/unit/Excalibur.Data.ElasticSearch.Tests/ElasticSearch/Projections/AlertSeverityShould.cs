// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.Projections;

namespace Excalibur.Data.Tests.ElasticSearch.Projections;

/// <summary>
/// Unit tests for the <see cref="AlertSeverity"/> enum.
/// </summary>
/// <remarks>
/// Sprint 513 (S513.2): Elasticsearch Phase 2 unit tests.
/// Tests verify enum values for alert severity levels.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Elasticsearch")]
[Trait("Feature", "Projections")]
public sealed class AlertSeverityShould
{
	#region Enum Value Tests

	[Fact]
	public void DefineInfoAsZero()
	{
		// Assert
		((int)AlertSeverity.Info).ShouldBe(0);
	}

	[Fact]
	public void DefineWarningAsOne()
	{
		// Assert
		((int)AlertSeverity.Warning).ShouldBe(1);
	}

	[Fact]
	public void DefineErrorAsTwo()
	{
		// Assert
		((int)AlertSeverity.Error).ShouldBe(2);
	}

	[Fact]
	public void DefineCriticalAsThree()
	{
		// Assert
		((int)AlertSeverity.Critical).ShouldBe(3);
	}

	#endregion

	#region Enum Count Tests

	[Fact]
	public void HaveFourDefinedValues()
	{
		// Act
		var values = Enum.GetValues<AlertSeverity>();

		// Assert
		values.Length.ShouldBe(4);
	}

	[Fact]
	public void ContainAllExpectedSeverities()
	{
		// Act
		var values = Enum.GetValues<AlertSeverity>();

		// Assert
		values.ShouldContain(AlertSeverity.Info);
		values.ShouldContain(AlertSeverity.Warning);
		values.ShouldContain(AlertSeverity.Error);
		values.ShouldContain(AlertSeverity.Critical);
	}

	#endregion

	#region Enum Parse Tests

	[Theory]
	[InlineData("Info", AlertSeverity.Info)]
	[InlineData("Warning", AlertSeverity.Warning)]
	[InlineData("Error", AlertSeverity.Error)]
	[InlineData("Critical", AlertSeverity.Critical)]
	public void ParseFromString_WithValidName(string name, AlertSeverity expected)
	{
		// Act
		var result = Enum.Parse<AlertSeverity>(name);

		// Assert
		result.ShouldBe(expected);
	}

	[Theory]
	[InlineData("info", AlertSeverity.Info)]
	[InlineData("WARNING", AlertSeverity.Warning)]
	[InlineData("error", AlertSeverity.Error)]
	[InlineData("CRITICAL", AlertSeverity.Critical)]
	public void ParseFromString_WithCaseInsensitiveMatch(string name, AlertSeverity expected)
	{
		// Act
		var result = Enum.Parse<AlertSeverity>(name, ignoreCase: true);

		// Assert
		result.ShouldBe(expected);
	}

	#endregion

	#region Severity Ordering Tests

	[Fact]
	public void HaveSeverityValuesInIncreasingOrder()
	{
		// Assert - Verify severity values increase (Info < Warning < Error < Critical)
		((int)AlertSeverity.Info).ShouldBeLessThan((int)AlertSeverity.Warning);
		((int)AlertSeverity.Warning).ShouldBeLessThan((int)AlertSeverity.Error);
		((int)AlertSeverity.Error).ShouldBeLessThan((int)AlertSeverity.Critical);
	}

	[Fact]
	public void InfoShouldBeLowestSeverity()
	{
		// Assert
		AlertSeverity.Info.ShouldBe(Enum.GetValues<AlertSeverity>().Min());
	}

	[Fact]
	public void CriticalShouldBeHighestSeverity()
	{
		// Assert
		AlertSeverity.Critical.ShouldBe(Enum.GetValues<AlertSeverity>().Max());
	}

	#endregion
}
