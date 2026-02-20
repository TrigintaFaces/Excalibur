// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Processing;

namespace Excalibur.Dispatch.Tests.Messaging.Processing;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class DispatcherStatisticsShould
{
	[Fact]
	public void Constructor_SetAllFields()
	{
		// Act
		var stats = new DispatcherStatistics(
			totalMessagesDispatched: 1000,
			totalErrors: 5,
			averageLatencyUs: 150.0,
			p50LatencyUs: 100.0,
			p95LatencyUs: 500.0,
			p99LatencyUs: 1000.0,
			lastLatencyUs: 120.0);

		// Assert
		stats.TotalMessagesDispatched.ShouldBe(1000);
		stats.TotalErrors.ShouldBe(5);
		stats.AverageLatencyUs.ShouldBe(150.0);
		stats.P50LatencyUs.ShouldBe(100.0);
		stats.P95LatencyUs.ShouldBe(500.0);
		stats.P99LatencyUs.ShouldBe(1000.0);
		stats.LastLatencyUs.ShouldBe(120.0);
	}

	[Fact]
	public void Equality_SameValues_AreEqual()
	{
		// Arrange
		var stats1 = new DispatcherStatistics(100, 5, 10.0, 8.0, 20.0, 50.0, 9.0);
		var stats2 = new DispatcherStatistics(100, 5, 10.0, 8.0, 20.0, 50.0, 9.0);

		// Assert
		stats1.ShouldBe(stats2);
		(stats1 == stats2).ShouldBeTrue();
		(stats1 != stats2).ShouldBeFalse();
	}

	[Fact]
	public void Equality_DifferentTotals_AreNotEqual()
	{
		// Arrange
		var stats1 = new DispatcherStatistics(100, 5, 10.0, 8.0, 20.0, 50.0, 9.0);
		var stats2 = new DispatcherStatistics(200, 5, 10.0, 8.0, 20.0, 50.0, 9.0);

		// Assert
		stats1.ShouldNotBe(stats2);
		(stats1 != stats2).ShouldBeTrue();
	}

	[Fact]
	public void GetHashCode_SameValues_ReturnSameHash()
	{
		// Arrange
		var stats1 = new DispatcherStatistics(100, 5, 10.0, 8.0, 20.0, 50.0, 9.0);
		var stats2 = new DispatcherStatistics(100, 5, 10.0, 8.0, 20.0, 50.0, 9.0);

		// Assert
		stats1.GetHashCode().ShouldBe(stats2.GetHashCode());
	}

	[Fact]
	public void Equals_WithObjectParam_ReturnFalseForDifferentType()
	{
		// Arrange
		var stats = new DispatcherStatistics(100, 5, 10.0, 8.0, 20.0, 50.0, 9.0);

		// Act & Assert
		stats.Equals("not a stats").ShouldBeFalse();
	}

	[Fact]
	public void Default_AllZeros()
	{
		// Act
		var stats = default(DispatcherStatistics);

		// Assert
		stats.TotalMessagesDispatched.ShouldBe(0);
		stats.TotalErrors.ShouldBe(0);
		stats.AverageLatencyUs.ShouldBe(0.0);
	}
}
