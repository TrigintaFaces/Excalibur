// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Abstractions.Tests.Outbox;

/// <summary>
/// Depth coverage tests for <see cref="OutboxStatistics"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class OutboxStatisticsDepthShould
{
	[Fact]
	public void TotalMessageCount_SumsAllCategories()
	{
		// Arrange
		var stats = new OutboxStatistics
		{
			StagedMessageCount = 10,
			SendingMessageCount = 5,
			SentMessageCount = 100,
			FailedMessageCount = 3,
			ScheduledMessageCount = 7,
		};

		// Assert
		stats.TotalMessageCount.ShouldBe(125);
	}

	[Fact]
	public void TotalMessageCount_IsZero_WhenAllCountsAreZero()
	{
		// Arrange
		var stats = new OutboxStatistics();

		// Assert
		stats.TotalMessageCount.ShouldBe(0);
	}

	[Fact]
	public void ToString_ContainsTotalCount()
	{
		// Arrange
		var stats = new OutboxStatistics
		{
			StagedMessageCount = 5,
			SentMessageCount = 20,
			FailedMessageCount = 1,
		};

		// Act
		var result = stats.ToString();

		// Assert
		result.ShouldContain("26 total");
	}

	[Fact]
	public void ToString_ContainsStagedCount()
	{
		// Arrange
		var stats = new OutboxStatistics { StagedMessageCount = 15 };

		// Act
		var result = stats.ToString();

		// Assert
		result.ShouldContain("15 staged");
	}

	[Fact]
	public void ToString_ContainsSentCount()
	{
		// Arrange
		var stats = new OutboxStatistics { SentMessageCount = 42 };

		// Act
		var result = stats.ToString();

		// Assert
		result.ShouldContain("42 sent");
	}

	[Fact]
	public void ToString_ContainsFailedCount()
	{
		// Arrange
		var stats = new OutboxStatistics { FailedMessageCount = 7 };

		// Act
		var result = stats.ToString();

		// Assert
		result.ShouldContain("7 failed");
	}

	[Fact]
	public void CapturedAt_DefaultsToUtcNow()
	{
		// Arrange
		var before = DateTimeOffset.UtcNow;

		// Act
		var stats = new OutboxStatistics();

		// Assert
		var after = DateTimeOffset.UtcNow;
		stats.CapturedAt.ShouldBeGreaterThanOrEqualTo(before);
		stats.CapturedAt.ShouldBeLessThanOrEqualTo(after);
	}

	[Fact]
	public void OldestUnsentMessageAge_DefaultsToNull()
	{
		// Assert
		new OutboxStatistics().OldestUnsentMessageAge.ShouldBeNull();
	}

	[Fact]
	public void OldestFailedMessageAge_DefaultsToNull()
	{
		// Assert
		new OutboxStatistics().OldestFailedMessageAge.ShouldBeNull();
	}

	[Fact]
	public void OldestUnsentMessageAge_CanBeSet()
	{
		// Arrange
		var age = TimeSpan.FromMinutes(15);
		var stats = new OutboxStatistics { OldestUnsentMessageAge = age };

		// Assert
		stats.OldestUnsentMessageAge.ShouldBe(age);
	}

	[Fact]
	public void OldestFailedMessageAge_CanBeSet()
	{
		// Arrange
		var age = TimeSpan.FromHours(2);
		var stats = new OutboxStatistics { OldestFailedMessageAge = age };

		// Assert
		stats.OldestFailedMessageAge.ShouldBe(age);
	}

	[Fact]
	public void AllDefaults_AreZeroOrNull()
	{
		// Arrange
		var stats = new OutboxStatistics();

		// Assert
		stats.StagedMessageCount.ShouldBe(0);
		stats.SendingMessageCount.ShouldBe(0);
		stats.SentMessageCount.ShouldBe(0);
		stats.FailedMessageCount.ShouldBe(0);
		stats.ScheduledMessageCount.ShouldBe(0);
		stats.OldestUnsentMessageAge.ShouldBeNull();
		stats.OldestFailedMessageAge.ShouldBeNull();
	}
}
