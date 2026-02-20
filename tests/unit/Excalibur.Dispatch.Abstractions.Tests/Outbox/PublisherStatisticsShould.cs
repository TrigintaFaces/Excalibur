// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Abstractions.Tests.Outbox;

/// <summary>
/// Unit tests for <see cref="PublisherStatistics"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class PublisherStatisticsShould
{
	[Fact]
	public void DefaultValues_AreCorrect()
	{
		// Act
		var stats = new PublisherStatistics();

		// Assert
		stats.TotalOperations.ShouldBe(0);
		stats.TotalMessagesPublished.ShouldBe(0);
		stats.TotalMessagesFailed.ShouldBe(0);
		stats.AverageMessagesPerSecond.ShouldBe(0);
		stats.CurrentSuccessRate.ShouldBe(0);
		stats.LastOperationAt.ShouldBeNull();
		stats.LastOperationDuration.ShouldBeNull();
	}

	[Fact]
	public void CapturedAt_HasDefaultValue()
	{
		// Arrange
		var before = DateTimeOffset.UtcNow;

		// Act
		var stats = new PublisherStatistics();

		// Assert
		stats.CapturedAt.ShouldBeGreaterThanOrEqualTo(before);
	}

	[Fact]
	public void AllProperties_CanBeInitialized()
	{
		// Arrange
		var now = DateTimeOffset.UtcNow;
		var duration = TimeSpan.FromSeconds(2.5);

		// Act
		var stats = new PublisherStatistics
		{
			TotalOperations = 100,
			TotalMessagesPublished = 950,
			TotalMessagesFailed = 50,
			AverageMessagesPerSecond = 42.5,
			CurrentSuccessRate = 95.0,
			LastOperationAt = now,
			LastOperationDuration = duration,
		};

		// Assert
		stats.TotalOperations.ShouldBe(100);
		stats.TotalMessagesPublished.ShouldBe(950);
		stats.TotalMessagesFailed.ShouldBe(50);
		stats.AverageMessagesPerSecond.ShouldBe(42.5);
		stats.CurrentSuccessRate.ShouldBe(95.0);
		stats.LastOperationAt.ShouldBe(now);
		stats.LastOperationDuration.ShouldBe(duration);
	}

	[Fact]
	public void ToString_ContainsRelevantInfo()
	{
		// Arrange
		var stats = new PublisherStatistics
		{
			TotalMessagesPublished = 100,
			TotalMessagesFailed = 5,
			CurrentSuccessRate = 95.2,
			AverageMessagesPerSecond = 10.5,
		};

		// Act
		var str = stats.ToString();

		// Assert
		str.ShouldContain("100");
		str.ShouldContain("5");
		str.ShouldContain("95.2");
		str.ShouldContain("10.5");
	}
}
