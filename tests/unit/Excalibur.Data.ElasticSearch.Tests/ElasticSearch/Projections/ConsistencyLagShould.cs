// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.Projections;

namespace Excalibur.Data.Tests.ElasticSearch.Projections;

[Trait("Category", "Unit")]
[Trait("Component", "Data")]
public sealed class ConsistencyLagShould
{
	[Fact]
	public void SetRequiredProperties()
	{
		var sut = new ConsistencyLag
		{
			ProjectionType = "OrderProjection",
			CurrentLagMs = 150.5,
			AverageLagMs = 100.0,
			MaxLagMs = 500.0,
			MinLagMs = 10.0,
		};

		sut.ProjectionType.ShouldBe("OrderProjection");
		sut.CurrentLagMs.ShouldBe(150.5);
		sut.AverageLagMs.ShouldBe(100.0);
		sut.MaxLagMs.ShouldBe(500.0);
		sut.MinLagMs.ShouldBe(10.0);
	}

	[Fact]
	public void HaveDefaultsForOptionalProperties()
	{
		var sut = new ConsistencyLag
		{
			ProjectionType = "Test",
			CurrentLagMs = 0,
			AverageLagMs = 0,
			MaxLagMs = 0,
			MinLagMs = 0,
		};

		sut.P95LagMs.ShouldBe(0);
		sut.P99LagMs.ShouldBe(0);
		sut.PendingEvents.ShouldBe(0);
		sut.LastProcessedEventTime.ShouldBeNull();
		sut.IsWithinSLA.ShouldBeFalse();
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		var lastProcessed = DateTimeOffset.UtcNow;
		var sut = new ConsistencyLag
		{
			ProjectionType = "OrderProjection",
			CurrentLagMs = 50.0,
			AverageLagMs = 75.0,
			MaxLagMs = 300.0,
			MinLagMs = 5.0,
			P95LagMs = 200.0,
			P99LagMs = 280.0,
			PendingEvents = 42,
			LastProcessedEventTime = lastProcessed,
			IsWithinSLA = true,
		};

		sut.P95LagMs.ShouldBe(200.0);
		sut.P99LagMs.ShouldBe(280.0);
		sut.PendingEvents.ShouldBe(42);
		sut.LastProcessedEventTime.ShouldBe(lastProcessed);
		sut.IsWithinSLA.ShouldBeTrue();
	}
}
