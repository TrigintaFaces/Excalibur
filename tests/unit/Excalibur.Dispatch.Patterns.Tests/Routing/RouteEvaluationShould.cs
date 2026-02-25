// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Routing;
using Excalibur.Dispatch.Patterns;

namespace Excalibur.Dispatch.Patterns.Tests.Routing;

/// <summary>
/// Unit tests for <see cref="RouteEvaluation"/> class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Patterns")]
public sealed class RouteEvaluationShould
{
	[Fact]
	public void HaveDefaultRoute_ByDefault()
	{
		// Arrange & Act
		var evaluation = new RouteEvaluation();

		// Assert
		evaluation.Route.ShouldNotBeNull();
	}

	[Fact]
	public void HaveEmptyMatchingRules_ByDefault()
	{
		// Arrange & Act
		var evaluation = new RouteEvaluation();

		// Assert
		evaluation.MatchingRules.ShouldNotBeNull();
		evaluation.MatchingRules.ShouldBeEmpty();
	}

	[Fact]
	public void HaveZeroScore_ByDefault()
	{
		// Arrange & Act
		var evaluation = new RouteEvaluation();

		// Assert
		evaluation.Score.ShouldBe(0);
	}

	[Fact]
	public void HaveEmptyReason_ByDefault()
	{
		// Arrange & Act
		var evaluation = new RouteEvaluation();

		// Assert
		evaluation.Reason.ShouldBe(string.Empty);
	}

	[Fact]
	public void AllowSettingRoute()
	{
		// Arrange
		var evaluation = new RouteEvaluation();
		var route = new RouteDefinition
		{
			RouteId = "route-123",
			Name = "Primary Route",
		};

		// Act
		evaluation.Route = route;

		// Assert
		evaluation.Route.RouteId.ShouldBe("route-123");
		evaluation.Route.Name.ShouldBe("Primary Route");
	}

	[Fact]
	public void AllowAddingMatchingRules()
	{
		// Arrange
		var evaluation = new RouteEvaluation();
		var rule1 = new TimeBasedRoutingRule
		{
			Name = "Business Hours Rule",
			Priority = 10,
		};
		var rule2 = new TimeBasedRoutingRule
		{
			Name = "Weekend Rule",
			Priority = 5,
		};

		// Act
		evaluation.MatchingRules.Add(rule1);
		evaluation.MatchingRules.Add(rule2);

		// Assert
		evaluation.MatchingRules.Count.ShouldBe(2);
		evaluation.MatchingRules[0].Name.ShouldBe("Business Hours Rule");
		evaluation.MatchingRules[1].Name.ShouldBe("Weekend Rule");
	}

	[Fact]
	public void AllowSettingScore()
	{
		// Arrange
		var evaluation = new RouteEvaluation();

		// Act
		evaluation.Score = 0.95;

		// Assert
		evaluation.Score.ShouldBe(0.95);
	}

	[Fact]
	public void AllowSettingNegativeScore()
	{
		// Arrange
		var evaluation = new RouteEvaluation();

		// Act
		evaluation.Score = -1.0;

		// Assert
		evaluation.Score.ShouldBe(-1.0);
	}

	[Fact]
	public void AllowSettingReason()
	{
		// Arrange
		var evaluation = new RouteEvaluation();

		// Act
		evaluation.Reason = "Route selected based on highest priority match";

		// Assert
		evaluation.Reason.ShouldBe("Route selected based on highest priority match");
	}

	[Fact]
	public void AllowCreatingWithAllProperties()
	{
		// Arrange & Act
		var evaluation = new RouteEvaluation
		{
			Route = new RouteDefinition
			{
				RouteId = "route-abc",
				Name = "Optimal Route",
				Weight = 100,
			},
			Score = 0.85,
			Reason = "Selected based on time conditions and priority",
		};

		evaluation.MatchingRules.Add(new TimeBasedRoutingRule
		{
			Name = "Peak Hours",
			Priority = 100,
			IsEnabled = true,
		});

		// Assert
		evaluation.Route.RouteId.ShouldBe("route-abc");
		evaluation.Route.Name.ShouldBe("Optimal Route");
		evaluation.Score.ShouldBe(0.85);
		evaluation.Reason.ShouldContain("time conditions");
		evaluation.MatchingRules.Count.ShouldBe(1);
		evaluation.MatchingRules[0].Priority.ShouldBe(100);
	}

	[Fact]
	public void RepresentSuccessfulEvaluation()
	{
		// Arrange & Act
		var evaluation = new RouteEvaluation
		{
			Route = new RouteDefinition
			{
				RouteId = "primary",
				Name = "Primary Route",
			},
			Score = 1.0,
			Reason = "Matched all conditions with highest priority",
		};

		evaluation.MatchingRules.Add(new TimeBasedRoutingRule
		{
			Name = "Business Hours",
			Priority = 100,
		});
		evaluation.MatchingRules.Add(new TimeBasedRoutingRule
		{
			Name = "Region Match",
			Priority = 50,
		});

		// Assert
		evaluation.Score.ShouldBe(1.0);
		evaluation.MatchingRules.Count.ShouldBe(2);
	}

	[Fact]
	public void RepresentRejectedEvaluation()
	{
		// Arrange & Act
		var evaluation = new RouteEvaluation
		{
			Route = new RouteDefinition
			{
				RouteId = "blocked",
				Name = "Blocked Route",
			},
			Score = 0,
			Reason = "Route is disabled during maintenance window",
		};

		// Assert
		evaluation.Score.ShouldBe(0);
		evaluation.Reason.ShouldContain("maintenance");
		evaluation.MatchingRules.ShouldBeEmpty();
	}
}
