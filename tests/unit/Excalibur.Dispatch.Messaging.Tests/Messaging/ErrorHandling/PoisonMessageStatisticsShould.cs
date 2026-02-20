// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.ErrorHandling;

namespace Excalibur.Dispatch.Tests.Messaging.ErrorHandling;

/// <summary>
///     Tests for the <see cref="PoisonMessageStatistics" /> class.
/// </summary>
[Trait("Category", "Unit")]
public sealed class PoisonMessageStatisticsShould
{
	[Fact]
	public void HaveDefaultValues()
	{
		var sut = new PoisonMessageStatistics();

		sut.TotalCount.ShouldBe(0);
		sut.RecentCount.ShouldBe(0);
		sut.TimeWindow.ShouldBe(TimeSpan.Zero);
		sut.MessagesByType.ShouldNotBeNull();
		sut.MessagesByType.ShouldBeEmpty();
		sut.MessagesByReason.ShouldNotBeNull();
		sut.MessagesByReason.ShouldBeEmpty();
		sut.OldestMessageDate.ShouldBeNull();
		sut.NewestMessageDate.ShouldBeNull();
	}

	[Fact]
	public void SetTotalCount()
	{
		var sut = new PoisonMessageStatistics { TotalCount = 42 };

		sut.TotalCount.ShouldBe(42);
	}

	[Fact]
	public void SetRecentCount()
	{
		var sut = new PoisonMessageStatistics { RecentCount = 10 };

		sut.RecentCount.ShouldBe(10);
	}

	[Fact]
	public void SetTimeWindow()
	{
		var sut = new PoisonMessageStatistics { TimeWindow = TimeSpan.FromHours(1) };

		sut.TimeWindow.ShouldBe(TimeSpan.FromHours(1));
	}

	[Fact]
	public void SetMessagesByType()
	{
		var sut = new PoisonMessageStatistics
		{
			MessagesByType = new Dictionary<string, int>
			{
				["OrderCommand"] = 5,
				["PaymentEvent"] = 3,
			},
		};

		sut.MessagesByType.Count.ShouldBe(2);
		sut.MessagesByType["OrderCommand"].ShouldBe(5);
		sut.MessagesByType["PaymentEvent"].ShouldBe(3);
	}

	[Fact]
	public void SetMessagesByReason()
	{
		var sut = new PoisonMessageStatistics
		{
			MessagesByReason = new Dictionary<string, int>
			{
				["Timeout"] = 10,
				["Validation"] = 2,
			},
		};

		sut.MessagesByReason.Count.ShouldBe(2);
		sut.MessagesByReason["Timeout"].ShouldBe(10);
	}

	[Fact]
	public void SetOldestMessageDate()
	{
		var date = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
		var sut = new PoisonMessageStatistics { OldestMessageDate = date };

		sut.OldestMessageDate.ShouldBe(date);
	}

	[Fact]
	public void SetNewestMessageDate()
	{
		var date = DateTimeOffset.UtcNow;
		var sut = new PoisonMessageStatistics { NewestMessageDate = date };

		sut.NewestMessageDate.ShouldBe(date);
	}
}
