// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Routing.LoadBalancing;

namespace Excalibur.Dispatch.Tests.Routing;

/// <summary>
/// Depth tests for <see cref="RouteHealthMonitorOptions"/>.
/// Covers all default values and property assignment.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class RouteHealthMonitorOptionsShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		// Arrange & Act
		var options = new RouteHealthMonitorOptions();

		// Assert
		options.CheckInterval.ShouldBe(TimeSpan.FromSeconds(30));
		options.InitialDelay.ShouldBe(TimeSpan.FromSeconds(10));
		options.MaxConcurrentHealthChecks.ShouldBe(10);
		options.HttpTimeout.ShouldBe(TimeSpan.FromSeconds(5));
		options.UnhealthyThreshold.ShouldBe(3);
		options.HealthyThreshold.ShouldBe(2);
	}

	[Fact]
	public void AllowCustomValues()
	{
		// Arrange & Act
		var options = new RouteHealthMonitorOptions
		{
			CheckInterval = TimeSpan.FromMinutes(1),
			InitialDelay = TimeSpan.FromSeconds(5),
			MaxConcurrentHealthChecks = 20,
			HttpTimeout = TimeSpan.FromSeconds(10),
			UnhealthyThreshold = 5,
			HealthyThreshold = 3,
		};

		// Assert
		options.CheckInterval.ShouldBe(TimeSpan.FromMinutes(1));
		options.InitialDelay.ShouldBe(TimeSpan.FromSeconds(5));
		options.MaxConcurrentHealthChecks.ShouldBe(20);
		options.HttpTimeout.ShouldBe(TimeSpan.FromSeconds(10));
		options.UnhealthyThreshold.ShouldBe(5);
		options.HealthyThreshold.ShouldBe(3);
	}
}
