// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.Projections;

namespace Excalibur.Data.Tests.ElasticSearch.Projections;

/// <summary>
/// Unit tests for the <see cref="ProjectionAlertSeverity"/> enum.
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
		((int)ProjectionAlertSeverity.Info).ShouldBe(0);
	}

	[Fact]
	public void DefineWarningAsOne()
	{
		// Assert
		((int)ProjectionAlertSeverity.Warning).ShouldBe(1);
	}

	[Fact]
	public void DefineErrorAsTwo()
	{
		// Assert
		((int)ProjectionAlertSeverity.Error).ShouldBe(2);
	}

	[Fact]
	public void DefineCriticalAsThree()
	{
		// Assert
		((int)ProjectionAlertSeverity.Critical).ShouldBe(3);
	}

	#endregion

	#region Enum Count Tests

	[Fact]
	public void HaveFourDefinedValues()
	{
		// Act
		var values = Enum.GetValues<ProjectionAlertSeverity>();

		// Assert
		values.Length.ShouldBe(4);
	}

	[Fact]
	public void ContainAllExpectedSeverities()
	{
		// Act
		var values = Enum.GetValues<ProjectionAlertSeverity>();

		// Assert
		values.ShouldContain(ProjectionAlertSeverity.Info);
		values.ShouldContain(ProjectionAlertSeverity.Warning);
		values.ShouldContain(ProjectionAlertSeverity.Error);
		values.ShouldContain(ProjectionAlertSeverity.Critical);
	}

	#endregion

	#region Enum Parse Tests

	[Theory]
	[InlineData("Info", ProjectionAlertSeverity.Info)]
	[InlineData("Warning", ProjectionAlertSeverity.Warning)]
	[InlineData("Error", ProjectionAlertSeverity.Error)]
	[InlineData("Critical", ProjectionAlertSeverity.Critical)]
	public void ParseFromString_WithValidName(string name, ProjectionAlertSeverity expected)
	{
		// Act
		var result = Enum.Parse<ProjectionAlertSeverity>(name);

		// Assert
		result.ShouldBe(expected);
	}

	[Theory]
	[InlineData("info", ProjectionAlertSeverity.Info)]
	[InlineData("WARNING", ProjectionAlertSeverity.Warning)]
	[InlineData("error", ProjectionAlertSeverity.Error)]
	[InlineData("CRITICAL", ProjectionAlertSeverity.Critical)]
	public void ParseFromString_WithCaseInsensitiveMatch(string name, ProjectionAlertSeverity expected)
	{
		// Act
		var result = Enum.Parse<ProjectionAlertSeverity>(name, ignoreCase: true);

		// Assert
		result.ShouldBe(expected);
	}

	#endregion

	#region Severity Ordering Tests

	[Fact]
	public void HaveSeverityValuesInIncreasingOrder()
	{
		// Assert - Verify severity values increase (Info < Warning < Error < Critical)
		((int)ProjectionAlertSeverity.Info).ShouldBeLessThan((int)ProjectionAlertSeverity.Warning);
		((int)ProjectionAlertSeverity.Warning).ShouldBeLessThan((int)ProjectionAlertSeverity.Error);
		((int)ProjectionAlertSeverity.Error).ShouldBeLessThan((int)ProjectionAlertSeverity.Critical);
	}

	[Fact]
	public void InfoShouldBeLowestSeverity()
	{
		// Assert
		ProjectionAlertSeverity.Info.ShouldBe(Enum.GetValues<ProjectionAlertSeverity>().Min());
	}

	[Fact]
	public void CriticalShouldBeHighestSeverity()
	{
		// Assert
		ProjectionAlertSeverity.Critical.ShouldBe(Enum.GetValues<ProjectionAlertSeverity>().Max());
	}

	#endregion
}
