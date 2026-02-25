// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Google;

namespace Excalibur.Dispatch.Transport.Tests.GooglePubSub.DeadLetter;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class DeadLetterStatisticsShould
{
	[Fact]
	public void CreateRetryPolicyStatisticsWithDefaults()
	{
		// Arrange & Act
		var stats = new RetryPolicyStatistics();

		// Assert
		stats.TotalAttempts.ShouldBe(0);
		stats.SuccessfulAttempts.ShouldBe(0);
		stats.FailedAttempts.ShouldBe(0);
		stats.AverageRetryCount.ShouldBe(0);
		stats.LastUpdated.ShouldBe(default);
		stats.PolicyCount.ShouldBe(0);
		stats.TotalRetryAttempts.ShouldBe(0);
		stats.SuccessRate.ShouldBe(0);
		stats.PolicyStatistics.ShouldBeEmpty();
	}

	[Fact]
	public void CreateRetryPolicyStatisticsWithValues()
	{
		// Arrange
		var now = DateTime.UtcNow;

		// Act
		var stats = new RetryPolicyStatistics
		{
			TotalAttempts = 100,
			SuccessfulAttempts = 85,
			FailedAttempts = 15,
			AverageRetryCount = 2.3,
			LastUpdated = now,
			PolicyCount = 4,
			TotalRetryAttempts = 100,
			SuccessRate = 0.85,
			PolicyStatistics =
			[
				new PolicyStatistic
				{
					PolicyKey = "default",
					TotalAttempts = 50,
					SuccessfulAttempts = 45,
					FailedAttempts = 5,
					AverageDuration = TimeSpan.FromMilliseconds(200),
					SuccessRate = 0.9,
				},
			],
		};

		// Assert
		stats.TotalAttempts.ShouldBe(100);
		stats.SuccessfulAttempts.ShouldBe(85);
		stats.FailedAttempts.ShouldBe(15);
		stats.AverageRetryCount.ShouldBe(2.3);
		stats.LastUpdated.ShouldBe(now);
		stats.PolicyCount.ShouldBe(4);
		stats.TotalRetryAttempts.ShouldBe(100);
		stats.SuccessRate.ShouldBe(0.85);
		stats.PolicyStatistics.Count.ShouldBe(1);
	}

	[Fact]
	public void CreatePolicyStatisticWithDefaults()
	{
		// Arrange & Act
		var stat = new PolicyStatistic();

		// Assert
		stat.PolicyKey.ShouldBe(string.Empty);
		stat.TotalAttempts.ShouldBe(0);
		stat.SuccessfulAttempts.ShouldBe(0);
		stat.FailedAttempts.ShouldBe(0);
		stat.AverageDuration.ShouldBe(TimeSpan.Zero);
		stat.SuccessRate.ShouldBe(0);
	}

	[Fact]
	public void CreatePolicyStatisticWithValues()
	{
		// Arrange & Act
		var stat = new PolicyStatistic
		{
			PolicyKey = "timeout-policy",
			TotalAttempts = 200,
			SuccessfulAttempts = 180,
			FailedAttempts = 20,
			AverageDuration = TimeSpan.FromMilliseconds(350),
			SuccessRate = 0.9,
		};

		// Assert
		stat.PolicyKey.ShouldBe("timeout-policy");
		stat.TotalAttempts.ShouldBe(200);
		stat.SuccessfulAttempts.ShouldBe(180);
		stat.FailedAttempts.ShouldBe(20);
		stat.AverageDuration.ShouldBe(TimeSpan.FromMilliseconds(350));
		stat.SuccessRate.ShouldBe(0.9);
	}

	[Fact]
	public void CreatePoisonDetectionStatisticsWithDefaults()
	{
		// Arrange & Act
		var stats = new PoisonDetectionStatistics();

		// Assert
		stats.TotalTrackedMessages.ShouldBe(0);
		stats.MultipleFailuresCount.ShouldBe(0);
		stats.TotalFailures.ShouldBe(0);
		stats.RecentFailures.ShouldBe(0);
		stats.DetectedPatterns.ShouldBeEmpty();
	}

	[Fact]
	public void CreatePoisonDetectionStatisticsWithValues()
	{
		// Arrange
		var now = DateTimeOffset.UtcNow;

		// Act
		var stats = new PoisonDetectionStatistics
		{
			TotalTrackedMessages = 500,
			MultipleFailuresCount = 12,
			TotalFailures = 45,
			RecentFailures = 8,
			DetectedPatterns =
			[
				new PatternInfo { Pattern = "TimeoutException", Occurrences = 20, LastSeen = now },
				new PatternInfo { Pattern = "SerializationError", Occurrences = 5, LastSeen = now },
			],
		};

		// Assert
		stats.TotalTrackedMessages.ShouldBe(500);
		stats.MultipleFailuresCount.ShouldBe(12);
		stats.TotalFailures.ShouldBe(45);
		stats.RecentFailures.ShouldBe(8);
		stats.DetectedPatterns.Count.ShouldBe(2);
	}
}
