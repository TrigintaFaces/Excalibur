// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.Health;

namespace Excalibur.EventSourcing.Tests.MaterializedViews;

/// <summary>
/// Unit tests for <see cref="MaterializedViewHealthCheckOptions"/>.
/// </summary>
/// <remarks>
/// Sprint 518: Health Checks &amp; OpenTelemetry Metrics tests.
/// Tests verify options defaults, property behavior, and validation.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "MaterializedViews")]
[Trait("Feature", "HealthChecks")]
public sealed class MaterializedViewHealthCheckOptionsShould
{
	#region Default Value Tests

	[Fact]
	public void HaveDefaultStalenessThresholdOfFiveMinutes()
	{
		// Arrange & Act
		var options = new MaterializedViewHealthCheckOptions();

		// Assert
		options.StalenessThreshold.ShouldBe(TimeSpan.FromMinutes(5));
	}

	[Fact]
	public void HaveDefaultFailureRateThresholdOfTenPercent()
	{
		// Arrange & Act
		var options = new MaterializedViewHealthCheckOptions();

		// Assert
		options.FailureRateThresholdPercent.ShouldBe(10.0);
	}

	[Fact]
	public void HaveIncludeDetailsEnabledByDefault()
	{
		// Arrange & Act
		var options = new MaterializedViewHealthCheckOptions();

		// Assert
		options.IncludeDetails.ShouldBeTrue();
	}

	[Fact]
	public void HaveDefaultTagsOfReadyAndEventSourcing()
	{
		// Arrange & Act
		var options = new MaterializedViewHealthCheckOptions();

		// Assert
		options.Tags.ShouldContain("ready");
		options.Tags.ShouldContain("event-sourcing");
	}

	[Fact]
	public void HaveDefaultNameOfMaterializedViews()
	{
		// Arrange & Act
		var options = new MaterializedViewHealthCheckOptions();

		// Assert
		options.Name.ShouldBe("materialized-views");
	}

	#endregion

	#region Property Setter Tests

	[Fact]
	public void AllowSettingStalenessThreshold()
	{
		// Arrange
		var options = new MaterializedViewHealthCheckOptions();
		var customThreshold = TimeSpan.FromMinutes(10);

		// Act
		options.StalenessThreshold = customThreshold;

		// Assert
		options.StalenessThreshold.ShouldBe(customThreshold);
	}

	[Fact]
	public void AllowSettingFailureRateThreshold()
	{
		// Arrange
		var options = new MaterializedViewHealthCheckOptions();

		// Act
		options.FailureRateThresholdPercent = 25.0;

		// Assert
		options.FailureRateThresholdPercent.ShouldBe(25.0);
	}

	[Fact]
	public void AllowDisablingIncludeDetails()
	{
		// Arrange
		var options = new MaterializedViewHealthCheckOptions();

		// Act
		options.IncludeDetails = false;

		// Assert
		options.IncludeDetails.ShouldBeFalse();
	}

	[Fact]
	public void AllowSettingCustomTags()
	{
		// Arrange
		var options = new MaterializedViewHealthCheckOptions();
		// Act
		options.Tags = ["custom", "tags"];

		// Assert
		options.Tags.ShouldContain("custom");
		options.Tags.ShouldContain("tags");
	}

	[Fact]
	public void AllowSettingCustomName()
	{
		// Arrange
		var options = new MaterializedViewHealthCheckOptions();

		// Act
		options.Name = "custom-health-check";

		// Assert
		options.Name.ShouldBe("custom-health-check");
	}

	#endregion
}
