// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Patterns;

namespace Excalibur.Dispatch.Patterns.Tests.Routing;

/// <summary>
/// Unit tests for <see cref="RoutingMetrics"/> class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Patterns")]
public sealed class RoutingMetricsShould
{
	[Fact]
	public void HaveZeroTotalRoutingDecisions_ByDefault()
	{
		// Arrange & Act
		var metrics = new RoutingMetrics();

		// Assert
		metrics.TotalRoutingDecisions.ShouldBe(0);
	}

	[Fact]
	public void HaveZeroSuccessfulRoutings_ByDefault()
	{
		// Arrange & Act
		var metrics = new RoutingMetrics();

		// Assert
		metrics.SuccessfulRoutings.ShouldBe(0);
	}

	[Fact]
	public void HaveZeroFailedRoutings_ByDefault()
	{
		// Arrange & Act
		var metrics = new RoutingMetrics();

		// Assert
		metrics.FailedRoutings.ShouldBe(0);
	}

	[Fact]
	public void HaveEmptyRouteUsage_ByDefault()
	{
		// Arrange & Act
		var metrics = new RoutingMetrics();

		// Assert
		metrics.RouteUsage.ShouldNotBeNull();
		metrics.RouteUsage.ShouldBeEmpty();
	}

	[Fact]
	public void HaveEmptyRuleMatches_ByDefault()
	{
		// Arrange & Act
		var metrics = new RoutingMetrics();

		// Assert
		metrics.RuleMatches.ShouldNotBeNull();
		metrics.RuleMatches.ShouldBeEmpty();
	}

	[Fact]
	public void HaveDefaultAverageDecisionTime_ByDefault()
	{
		// Arrange & Act
		var metrics = new RoutingMetrics();

		// Assert
		metrics.AverageDecisionTime.ShouldBe(default);
	}

	[Fact]
	public void HaveLastReset_SetToUtcNow()
	{
		// Arrange
		var before = DateTimeOffset.UtcNow;

		// Act
		var metrics = new RoutingMetrics();

		// Assert
		var after = DateTimeOffset.UtcNow;
		metrics.LastReset.ShouldBeGreaterThanOrEqualTo(before);
		metrics.LastReset.ShouldBeLessThanOrEqualTo(after);
	}

	[Fact]
	public void AllowSettingTotalRoutingDecisions()
	{
		// Arrange
		var metrics = new RoutingMetrics();

		// Act
		metrics.TotalRoutingDecisions = 1000;

		// Assert
		metrics.TotalRoutingDecisions.ShouldBe(1000);
	}

	[Fact]
	public void AllowSettingSuccessfulRoutings()
	{
		// Arrange
		var metrics = new RoutingMetrics();

		// Act
		metrics.SuccessfulRoutings = 950;

		// Assert
		metrics.SuccessfulRoutings.ShouldBe(950);
	}

	[Fact]
	public void AllowSettingFailedRoutings()
	{
		// Arrange
		var metrics = new RoutingMetrics();

		// Act
		metrics.FailedRoutings = 50;

		// Assert
		metrics.FailedRoutings.ShouldBe(50);
	}

	[Fact]
	public void AllowAddingRouteUsage()
	{
		// Arrange
		var metrics = new RoutingMetrics();

		// Act
		metrics.RouteUsage["route-a"] = 100;
		metrics.RouteUsage["route-b"] = 200;

		// Assert
		metrics.RouteUsage.Count.ShouldBe(2);
		metrics.RouteUsage["route-a"].ShouldBe(100);
		metrics.RouteUsage["route-b"].ShouldBe(200);
	}

	[Fact]
	public void AllowAddingRuleMatches()
	{
		// Arrange
		var metrics = new RoutingMetrics();

		// Act
		metrics.RuleMatches["rule-1"] = 500;
		metrics.RuleMatches["rule-2"] = 300;

		// Assert
		metrics.RuleMatches.Count.ShouldBe(2);
		metrics.RuleMatches["rule-1"].ShouldBe(500);
		metrics.RuleMatches["rule-2"].ShouldBe(300);
	}

	[Fact]
	public void AllowSettingAverageDecisionTime()
	{
		// Arrange
		var metrics = new RoutingMetrics();
		var avgTime = TimeSpan.FromMilliseconds(15);

		// Act
		metrics.AverageDecisionTime = avgTime;

		// Assert
		metrics.AverageDecisionTime.ShouldBe(avgTime);
	}

	[Fact]
	public void AllowSettingLastReset()
	{
		// Arrange
		var metrics = new RoutingMetrics();
		var resetTime = DateTimeOffset.UtcNow.AddDays(-1);

		// Act
		metrics.LastReset = resetTime;

		// Assert
		metrics.LastReset.ShouldBe(resetTime);
	}

	[Fact]
	public void AllowCreatingWithAllProperties()
	{
		// Arrange & Act
		var resetTime = DateTimeOffset.UtcNow;
		var metrics = new RoutingMetrics
		{
			TotalRoutingDecisions = 1000,
			SuccessfulRoutings = 980,
			FailedRoutings = 20,
			AverageDecisionTime = TimeSpan.FromMilliseconds(5),
			LastReset = resetTime,
		};
		metrics.RouteUsage["primary"] = 800;
		metrics.RouteUsage["secondary"] = 180;
		metrics.RuleMatches["time-based"] = 700;
		metrics.RuleMatches["priority"] = 280;

		// Assert
		metrics.TotalRoutingDecisions.ShouldBe(1000);
		metrics.SuccessfulRoutings.ShouldBe(980);
		metrics.FailedRoutings.ShouldBe(20);
		metrics.AverageDecisionTime.ShouldBe(TimeSpan.FromMilliseconds(5));
		metrics.LastReset.ShouldBe(resetTime);
		metrics.RouteUsage.Count.ShouldBe(2);
		metrics.RuleMatches.Count.ShouldBe(2);
	}
}
