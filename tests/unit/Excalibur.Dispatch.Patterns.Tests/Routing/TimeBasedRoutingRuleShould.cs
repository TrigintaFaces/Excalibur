// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Routing;
using Excalibur.Dispatch.Patterns;

namespace Excalibur.Dispatch.Patterns.Tests.Routing;

/// <summary>
/// Unit tests for <see cref="TimeBasedRoutingRule"/> class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Patterns")]
public sealed class TimeBasedRoutingRuleShould
{
	[Fact]
	public void HaveNonEmptyId_ByDefault()
	{
		// Arrange & Act
		var rule = new TimeBasedRoutingRule();

		// Assert
		rule.Id.ShouldNotBeNullOrEmpty();
		Guid.TryParse(rule.Id, out _).ShouldBeTrue();
	}

	[Fact]
	public void HaveUniqueId_ForEachInstance()
	{
		// Arrange & Act
		var rule1 = new TimeBasedRoutingRule();
		var rule2 = new TimeBasedRoutingRule();

		// Assert
		rule1.Id.ShouldNotBe(rule2.Id);
	}

	[Fact]
	public void HaveEmptyName_ByDefault()
	{
		// Arrange & Act
		var rule = new TimeBasedRoutingRule();

		// Assert
		rule.Name.ShouldBe(string.Empty);
	}

	[Fact]
	public void HaveNullDescription_ByDefault()
	{
		// Arrange & Act
		var rule = new TimeBasedRoutingRule();

		// Assert
		rule.Description.ShouldBeNull();
	}

	[Fact]
	public void HaveZeroPriority_ByDefault()
	{
		// Arrange & Act
		var rule = new TimeBasedRoutingRule();

		// Assert
		rule.Priority.ShouldBe(0);
	}

	[Fact]
	public void HaveDefaultConditions_ByDefault()
	{
		// Arrange & Act
		var rule = new TimeBasedRoutingRule();

		// Assert
		rule.Conditions.ShouldNotBeNull();
	}

	[Fact]
	public void HaveEmptyRoutes_ByDefault()
	{
		// Arrange & Act
		var rule = new TimeBasedRoutingRule();

		// Assert
		rule.Routes.ShouldNotBeNull();
		rule.Routes.ShouldBeEmpty();
	}

	[Fact]
	public void HaveTrueIsEnabled_ByDefault()
	{
		// Arrange & Act
		var rule = new TimeBasedRoutingRule();

		// Assert
		rule.IsEnabled.ShouldBeTrue();
	}

	[Fact]
	public void HaveEmptyMetadata_ByDefault()
	{
		// Arrange & Act
		var rule = new TimeBasedRoutingRule();

		// Assert
		rule.Metadata.ShouldNotBeNull();
		rule.Metadata.ShouldBeEmpty();
	}

	[Fact]
	public void AllowSettingId()
	{
		// Arrange
		var rule = new TimeBasedRoutingRule();

		// Act
		rule.Id = "custom-rule-id";

		// Assert
		rule.Id.ShouldBe("custom-rule-id");
	}

	[Fact]
	public void AllowSettingName()
	{
		// Arrange
		var rule = new TimeBasedRoutingRule();

		// Act
		rule.Name = "Business Hours Rule";

		// Assert
		rule.Name.ShouldBe("Business Hours Rule");
	}

	[Fact]
	public void AllowSettingDescription()
	{
		// Arrange
		var rule = new TimeBasedRoutingRule();

		// Act
		rule.Description = "Routes messages to primary queue during business hours";

		// Assert
		rule.Description.ShouldBe("Routes messages to primary queue during business hours");
	}

	[Fact]
	public void AllowSettingPriority()
	{
		// Arrange
		var rule = new TimeBasedRoutingRule();

		// Act
		rule.Priority = 100;

		// Assert
		rule.Priority.ShouldBe(100);
	}

	[Fact]
	public void AllowSettingNegativePriority()
	{
		// Arrange
		var rule = new TimeBasedRoutingRule();

		// Act
		rule.Priority = -10;

		// Assert
		rule.Priority.ShouldBe(-10);
	}

	[Fact]
	public void AllowSettingConditions()
	{
		// Arrange
		var rule = new TimeBasedRoutingRule();
		var conditions = new TimeConditions
		{
			TimeZoneId = "UTC",
		};
		conditions.ActiveDaysOfWeek.Add(DayOfWeek.Monday);

		// Act
		rule.Conditions = conditions;

		// Assert
		rule.Conditions.TimeZoneId.ShouldBe("UTC");
		rule.Conditions.ActiveDaysOfWeek.Count.ShouldBe(1);
	}

	[Fact]
	public void AllowAddingRoutes()
	{
		// Arrange
		var rule = new TimeBasedRoutingRule();
		var route1 = new RouteDefinition
		{
			RouteId = "primary",
			Name = "Primary Queue",
		};
		var route2 = new RouteDefinition
		{
			RouteId = "fallback",
			Name = "Fallback Queue",
		};

		// Act
		rule.Routes.Add(route1);
		rule.Routes.Add(route2);

		// Assert
		rule.Routes.Count.ShouldBe(2);
		rule.Routes[0].RouteId.ShouldBe("primary");
		rule.Routes[1].RouteId.ShouldBe("fallback");
	}

	[Fact]
	public void AllowSettingIsEnabled()
	{
		// Arrange
		var rule = new TimeBasedRoutingRule();

		// Act
		rule.IsEnabled = false;

		// Assert
		rule.IsEnabled.ShouldBeFalse();
	}

	[Fact]
	public void AllowAddingMetadata()
	{
		// Arrange
		var rule = new TimeBasedRoutingRule();

		// Act
		rule.Metadata["created_by"] = "admin";
		rule.Metadata["version"] = 1;
		rule.Metadata["tags"] = new[] { "production", "high-priority" };

		// Assert
		rule.Metadata.Count.ShouldBe(3);
		rule.Metadata["created_by"].ShouldBe("admin");
		rule.Metadata["version"].ShouldBe(1);
	}

	[Fact]
	public void AllowCreatingWithAllProperties()
	{
		// Arrange & Act
		var rule = new TimeBasedRoutingRule
		{
			Id = "rule-001",
			Name = "Peak Hours Routing",
			Description = "Routes high-volume traffic to dedicated queues during peak hours",
			Priority = 50,
			IsEnabled = true,
			Conditions = new TimeConditions
			{
				TimeZoneId = "America/New_York",
			},
		};

		rule.Conditions.ActiveTimeRanges.Add(new TimeRange
		{
			StartTime = TimeSpan.FromHours(8),
			EndTime = TimeSpan.FromHours(18),
		});

		rule.Conditions.ActiveDaysOfWeek.Add(DayOfWeek.Monday);
		rule.Conditions.ActiveDaysOfWeek.Add(DayOfWeek.Tuesday);
		rule.Conditions.ActiveDaysOfWeek.Add(DayOfWeek.Wednesday);
		rule.Conditions.ActiveDaysOfWeek.Add(DayOfWeek.Thursday);
		rule.Conditions.ActiveDaysOfWeek.Add(DayOfWeek.Friday);

		rule.Routes.Add(new RouteDefinition
		{
			RouteId = "peak-queue",
			Name = "Peak Hours Queue",
		});

		rule.Metadata["region"] = "us-east-1";
		rule.Metadata["cost_center"] = "operations";

		// Assert
		rule.Id.ShouldBe("rule-001");
		rule.Name.ShouldBe("Peak Hours Routing");
		rule.Description.ShouldContain("dedicated queues");
		rule.Priority.ShouldBe(50);
		rule.IsEnabled.ShouldBeTrue();
		rule.Conditions.TimeZoneId.ShouldBe("America/New_York");
		rule.Conditions.ActiveTimeRanges.Count.ShouldBe(1);
		rule.Conditions.ActiveDaysOfWeek.Count.ShouldBe(5);
		rule.Routes.Count.ShouldBe(1);
		rule.Metadata.Count.ShouldBe(2);
	}

	[Fact]
	public void RepresentDisabledRule()
	{
		// Arrange & Act
		var rule = new TimeBasedRoutingRule
		{
			Name = "Maintenance Window Rule",
			IsEnabled = false,
			Description = "Temporarily disabled for system maintenance",
		};

		// Assert
		rule.IsEnabled.ShouldBeFalse();
	}
}
