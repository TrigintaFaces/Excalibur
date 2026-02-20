// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.Projections;

namespace Excalibur.Data.Tests.ElasticSearch.Projections;

[Trait("Category", "Unit")]
[Trait("Component", "Data")]
public sealed class ConsistencyMetricsShould
{
	[Fact]
	public void SetRequiredProperties()
	{
		var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
		var end = new DateTime(2026, 1, 1, 1, 0, 0, DateTimeKind.Utc);
		var sut = new ConsistencyMetrics
		{
			ProjectionType = "OrderProjection",
			PeriodStart = start,
			PeriodEnd = end,
			TotalEventsProcessed = 10000,
			AverageProcessingTimeMs = 2.5,
			EventsPerSecond = 400.0,
			SLACompliancePercentage = 99.9,
		};

		sut.ProjectionType.ShouldBe("OrderProjection");
		sut.PeriodStart.ShouldBe(start);
		sut.PeriodEnd.ShouldBe(end);
		sut.TotalEventsProcessed.ShouldBe(10000);
		sut.AverageProcessingTimeMs.ShouldBe(2.5);
		sut.EventsPerSecond.ShouldBe(400.0);
		sut.SLACompliancePercentage.ShouldBe(99.9);
	}

	[Fact]
	public void HaveZeroDefaultsForOptionalProperties()
	{
		var sut = new ConsistencyMetrics
		{
			ProjectionType = "Test",
			PeriodStart = DateTime.UtcNow,
			PeriodEnd = DateTime.UtcNow,
			TotalEventsProcessed = 0,
			AverageProcessingTimeMs = 0,
			EventsPerSecond = 0,
			SLACompliancePercentage = 0,
		};

		sut.ConsistencyViolations.ShouldBe(0);
		sut.LagDistribution.ShouldBeNull();
	}

	[Fact]
	public void AllowSettingOptionalProperties()
	{
		var lagDist = new Dictionary<string, long>
		{
			["<100ms"] = 9000,
			["100-500ms"] = 900,
			[">500ms"] = 100,
		};

		var sut = new ConsistencyMetrics
		{
			ProjectionType = "Test",
			PeriodStart = DateTime.UtcNow,
			PeriodEnd = DateTime.UtcNow,
			TotalEventsProcessed = 10000,
			AverageProcessingTimeMs = 1.0,
			EventsPerSecond = 1000.0,
			SLACompliancePercentage = 99.0,
			ConsistencyViolations = 5,
			LagDistribution = lagDist,
		};

		sut.ConsistencyViolations.ShouldBe(5);
		sut.LagDistribution.ShouldBeSameAs(lagDist);
	}
}
