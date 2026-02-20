// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Processing;

namespace Excalibur.Dispatch.Tests.Messaging.Processing;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ProcessorStatisticsShould
{
	[Fact]
	public void Constructor_SetAllFields()
	{
		// Act
		var stats = new ProcessorStatistics(
			totalMessagesProcessed: 500,
			totalErrors: 3,
			averageLatencyUs: 200.0,
			p99LatencyUs: 900.0,
			activeThreads: 4);

		// Assert
		stats.TotalMessagesProcessed.ShouldBe(500);
		stats.TotalErrors.ShouldBe(3);
		stats.AverageLatencyUs.ShouldBe(200.0);
		stats.P99LatencyUs.ShouldBe(900.0);
		stats.ActiveThreads.ShouldBe(4);
	}

	[Fact]
	public void Equality_SameValues_AreEqual()
	{
		// Arrange
		var stats1 = new ProcessorStatistics(500, 3, 200.0, 900.0, 4);
		var stats2 = new ProcessorStatistics(500, 3, 200.0, 900.0, 4);

		// Assert
		stats1.ShouldBe(stats2);
		(stats1 == stats2).ShouldBeTrue();
		(stats1 != stats2).ShouldBeFalse();
	}

	[Fact]
	public void Equality_DifferentActiveThreads_AreNotEqual()
	{
		// Arrange
		var stats1 = new ProcessorStatistics(500, 3, 200.0, 900.0, 4);
		var stats2 = new ProcessorStatistics(500, 3, 200.0, 900.0, 8);

		// Assert
		stats1.ShouldNotBe(stats2);
		(stats1 != stats2).ShouldBeTrue();
	}

	[Fact]
	public void GetHashCode_SameValues_ReturnSameHash()
	{
		// Arrange
		var stats1 = new ProcessorStatistics(500, 3, 200.0, 900.0, 4);
		var stats2 = new ProcessorStatistics(500, 3, 200.0, 900.0, 4);

		// Assert
		stats1.GetHashCode().ShouldBe(stats2.GetHashCode());
	}

	[Fact]
	public void Equals_WithObjectParam_ReturnFalseForDifferentType()
	{
		// Arrange
		var stats = new ProcessorStatistics(500, 3, 200.0, 900.0, 4);

		// Act & Assert
		stats.Equals(42).ShouldBeFalse();
	}

	[Fact]
	public void Default_AllZeros()
	{
		// Act
		var stats = default(ProcessorStatistics);

		// Assert
		stats.TotalMessagesProcessed.ShouldBe(0);
		stats.TotalErrors.ShouldBe(0);
		stats.ActiveThreads.ShouldBe(0);
	}
}
