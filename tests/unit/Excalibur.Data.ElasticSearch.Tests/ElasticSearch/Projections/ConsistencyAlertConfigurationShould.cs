// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.Projections;

namespace Excalibur.Data.Tests.ElasticSearch.Projections;

[Trait("Category", "Unit")]
[Trait("Component", "Data")]
public sealed class ConsistencyAlertConfigurationShould
{
	[Fact]
	public void SetRequiredProperties()
	{
		var sut = new ConsistencyAlertConfiguration
		{
			MaxAcceptableLag = TimeSpan.FromSeconds(30),
		};

		sut.MaxAcceptableLag.ShouldBe(TimeSpan.FromSeconds(30));
	}

	[Fact]
	public void HaveCorrectDefaults()
	{
		var sut = new ConsistencyAlertConfiguration
		{
			MaxAcceptableLag = TimeSpan.FromSeconds(30),
		};

		sut.RequiredSLAPercentage.ShouldBe(99.0);
		sut.MetricsWindow.ShouldBe(TimeSpan.FromMinutes(5));
		sut.ProjectionSpecificThresholds.ShouldBeNull();
		sut.AlertOnIndividualEvents.ShouldBeFalse();
		sut.AlertCooldownPeriod.ShouldBe(TimeSpan.FromMinutes(15));
		sut.SeverityThresholds.ShouldBeNull();
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		var thresholds = new Dictionary<string, TimeSpan>
		{
			["OrderProjection"] = TimeSpan.FromSeconds(10),
			["CustomerProjection"] = TimeSpan.FromSeconds(60),
		};
		var severities = new List<AlertSeverityThreshold>
		{
			new() { LagThreshold = TimeSpan.FromSeconds(30), Severity = AlertSeverity.Warning },
			new() { LagThreshold = TimeSpan.FromMinutes(5), Severity = AlertSeverity.Critical },
		};

		var sut = new ConsistencyAlertConfiguration
		{
			MaxAcceptableLag = TimeSpan.FromSeconds(15),
			RequiredSLAPercentage = 99.9,
			MetricsWindow = TimeSpan.FromMinutes(10),
			ProjectionSpecificThresholds = thresholds,
			AlertOnIndividualEvents = true,
			AlertCooldownPeriod = TimeSpan.FromMinutes(30),
			SeverityThresholds = severities,
		};

		sut.RequiredSLAPercentage.ShouldBe(99.9);
		sut.MetricsWindow.ShouldBe(TimeSpan.FromMinutes(10));
		sut.ProjectionSpecificThresholds.ShouldBeSameAs(thresholds);
		sut.AlertOnIndividualEvents.ShouldBeTrue();
		sut.AlertCooldownPeriod.ShouldBe(TimeSpan.FromMinutes(30));
		sut.SeverityThresholds.ShouldBeSameAs(severities);
	}
}
