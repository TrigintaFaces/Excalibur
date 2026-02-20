// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.Projections;

namespace Excalibur.Data.Tests.ElasticSearch.Projections;

[Trait("Category", "Unit")]
[Trait("Component", "Data")]
public sealed class AlertSeverityThresholdShould
{
	[Fact]
	public void SetRequiredProperties()
	{
		var sut = new AlertSeverityThreshold
		{
			LagThreshold = TimeSpan.FromSeconds(30),
			Severity = AlertSeverity.Warning,
		};

		sut.LagThreshold.ShouldBe(TimeSpan.FromSeconds(30));
		sut.Severity.ShouldBe(AlertSeverity.Warning);
	}

	[Fact]
	public void HaveNullDefaultForMessageTemplate()
	{
		var sut = new AlertSeverityThreshold
		{
			LagThreshold = TimeSpan.FromMinutes(5),
			Severity = AlertSeverity.Critical,
		};

		sut.MessageTemplate.ShouldBeNull();
	}

	[Fact]
	public void AllowSettingMessageTemplate()
	{
		var sut = new AlertSeverityThreshold
		{
			LagThreshold = TimeSpan.FromMinutes(10),
			Severity = AlertSeverity.Critical,
			MessageTemplate = "Projection {ProjectionType} lag exceeded {Threshold}",
		};

		sut.MessageTemplate.ShouldBe("Projection {ProjectionType} lag exceeded {Threshold}");
	}
}
