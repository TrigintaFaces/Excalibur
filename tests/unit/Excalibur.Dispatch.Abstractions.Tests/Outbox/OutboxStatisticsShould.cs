// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// Licensed under the Excalibur License 1.0 - see LICENSE files for details.

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Abstractions.Tests.Outbox;

/// <summary>
/// Unit tests for the <see cref="OutboxStatistics"/> class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Abstractions")]
public sealed class OutboxStatisticsShould
{
	[Fact]
	public void Have_DefaultValues_WhenCreated()
	{
		// Act
		var stats = new OutboxStatistics();

		// Assert
		stats.StagedMessageCount.ShouldBe(0);
		stats.SendingMessageCount.ShouldBe(0);
		stats.SentMessageCount.ShouldBe(0);
		stats.FailedMessageCount.ShouldBe(0);
		stats.ScheduledMessageCount.ShouldBe(0);
		stats.OldestUnsentMessageAge.ShouldBeNull();
		stats.OldestFailedMessageAge.ShouldBeNull();
		stats.CapturedAt.ShouldNotBe(default);
	}

	[Fact]
	public void TotalMessageCount_Should_SumAllCounts()
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

		// Act & Assert
		stats.TotalMessageCount.ShouldBe(125);
	}

	[Fact]
	public void TotalMessageCount_Should_BeZero_WhenAllCountsAreZero()
	{
		// Arrange
		var stats = new OutboxStatistics();

		// Act & Assert
		stats.TotalMessageCount.ShouldBe(0);
	}

	[Fact]
	public void ToString_Should_IncludeRelevantCounts()
	{
		// Arrange
		var stats = new OutboxStatistics
		{
			StagedMessageCount = 5,
			SentMessageCount = 20,
			FailedMessageCount = 2,
		};

		// Act
		var result = stats.ToString();

		// Assert
		result.ShouldContain("27 total");
		result.ShouldContain("5 staged");
		result.ShouldContain("20 sent");
		result.ShouldContain("2 failed");
	}

	[Fact]
	public void Support_InitSyntax_ForAges()
	{
		// Arrange & Act
		var stats = new OutboxStatistics
		{
			OldestUnsentMessageAge = TimeSpan.FromMinutes(30),
			OldestFailedMessageAge = TimeSpan.FromHours(2),
		};

		// Assert
		stats.OldestUnsentMessageAge.ShouldBe(TimeSpan.FromMinutes(30));
		stats.OldestFailedMessageAge.ShouldBe(TimeSpan.FromHours(2));
	}
}
