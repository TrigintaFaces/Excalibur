// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Abstractions.Tests.Inbox;

/// <summary>
/// Unit tests for <see cref="DeduplicationStatistics"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class DeduplicationStatisticsShould
{
	[Fact]
	public void DefaultValues_AreZero()
	{
		// Act
		var stats = new DeduplicationStatistics();

		// Assert
		stats.TrackedMessageCount.ShouldBe(0);
		stats.TotalChecks.ShouldBe(0);
		stats.DuplicatesDetected.ShouldBe(0);
		stats.EstimatedMemoryUsageBytes.ShouldBe(0);
	}

	[Fact]
	public void DuplicateHitRatio_ReturnsZero_WhenNoChecks()
	{
		// Act
		var stats = new DeduplicationStatistics();

		// Assert
		stats.DuplicateHitRatio.ShouldBe(0.0);
	}

	[Fact]
	public void DuplicateHitRatio_CalculatesCorrectPercentage()
	{
		// Act
		var stats = new DeduplicationStatistics
		{
			TotalChecks = 100,
			DuplicatesDetected = 25,
		};

		// Assert
		stats.DuplicateHitRatio.ShouldBe(25.0);
	}

	[Fact]
	public void DuplicateHitRatio_Handles100Percent()
	{
		// Act
		var stats = new DeduplicationStatistics
		{
			TotalChecks = 50,
			DuplicatesDetected = 50,
		};

		// Assert
		stats.DuplicateHitRatio.ShouldBe(100.0);
	}

	[Fact]
	public void CapturedAt_HasDefaultValue()
	{
		// Arrange
		var before = DateTimeOffset.UtcNow;

		// Act
		var stats = new DeduplicationStatistics();

		// Assert
		stats.CapturedAt.ShouldBeGreaterThanOrEqualTo(before);
	}

	[Fact]
	public void AllProperties_CanBeSet()
	{
		// Act
		var stats = new DeduplicationStatistics
		{
			TrackedMessageCount = 1000,
			TotalChecks = 5000,
			DuplicatesDetected = 200,
			EstimatedMemoryUsageBytes = 65536,
		};

		// Assert
		stats.TrackedMessageCount.ShouldBe(1000);
		stats.TotalChecks.ShouldBe(5000);
		stats.DuplicatesDetected.ShouldBe(200);
		stats.EstimatedMemoryUsageBytes.ShouldBe(65536);
	}

	[Fact]
	public void ToString_ContainsRelevantInfo()
	{
		// Arrange
		var stats = new DeduplicationStatistics
		{
			TrackedMessageCount = 42,
			TotalChecks = 100,
			DuplicatesDetected = 10,
			EstimatedMemoryUsageBytes = 1024,
		};

		// Act
		var str = stats.ToString();

		// Assert
		str.ShouldContain("42");
		str.ShouldContain("10");
		str.ShouldContain("100");
		str.ShouldContain("1,024");
	}
}
