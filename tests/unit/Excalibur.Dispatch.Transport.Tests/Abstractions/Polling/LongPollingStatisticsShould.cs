// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Transport.Tests.Abstractions.Polling;

/// <summary>
/// Unit tests for <see cref="LongPollingStatistics"/> struct.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Transport.Abstractions")]
public sealed class LongPollingStatisticsShould
{
	[Fact]
	public void HaveZeroTotalReceives_ByDefault()
	{
		// Arrange & Act
		var stats = new LongPollingStatistics();

		// Assert
		stats.TotalReceives.ShouldBe(0);
	}

	[Fact]
	public void HaveZeroTotalMessages_ByDefault()
	{
		// Arrange & Act
		var stats = new LongPollingStatistics();

		// Assert
		stats.TotalMessages.ShouldBe(0);
	}

	[Fact]
	public void HaveZeroEmptyReceives_ByDefault()
	{
		// Arrange & Act
		var stats = new LongPollingStatistics();

		// Assert
		stats.EmptyReceives.ShouldBe(0);
	}

	[Fact]
	public void HaveZeroCurrentLoadFactor_ByDefault()
	{
		// Arrange & Act
		var stats = new LongPollingStatistics();

		// Assert
		stats.CurrentLoadFactor.ShouldBe(0.0);
	}

	[Fact]
	public void HaveZeroCurrentWaitTime_ByDefault()
	{
		// Arrange & Act
		var stats = new LongPollingStatistics();

		// Assert
		stats.CurrentWaitTime.ShouldBe(TimeSpan.Zero);
	}

	[Fact]
	public void HaveZeroApiCallsSaved_ByDefault()
	{
		// Arrange & Act
		var stats = new LongPollingStatistics();

		// Assert
		stats.ApiCallsSaved.ShouldBe(0);
	}

	[Fact]
	public void HaveDefaultLastReceiveTime_ByDefault()
	{
		// Arrange & Act
		var stats = new LongPollingStatistics();

		// Assert
		stats.LastReceiveTime.ShouldBe(default(DateTimeOffset));
	}

	[Fact]
	public void AverageMessagesPerReceive_ReturnsZeroWhenNoReceives()
	{
		// Arrange & Act
		var stats = new LongPollingStatistics();

		// Assert
		stats.AverageMessagesPerReceive.ShouldBe(0.0);
	}

	[Fact]
	public void AverageMessagesPerReceive_CalculatesCorrectly()
	{
		// Arrange
		var stats = new LongPollingStatistics { TotalReceives = 10, TotalMessages = 50 };

		// Assert
		stats.AverageMessagesPerReceive.ShouldBe(5.0);
	}

	[Fact]
	public void EmptyReceiveRate_ReturnsZeroWhenNoReceives()
	{
		// Arrange & Act
		var stats = new LongPollingStatistics();

		// Assert
		stats.EmptyReceiveRate.ShouldBe(0.0);
	}

	[Fact]
	public void EmptyReceiveRate_CalculatesCorrectly()
	{
		// Arrange
		var stats = new LongPollingStatistics { TotalReceives = 100, EmptyReceives = 25 };

		// Assert
		stats.EmptyReceiveRate.ShouldBe(0.25);
	}

	[Fact]
	public void AllowSettingTotalReceives()
	{
		// Arrange & Act
		var stats = new LongPollingStatistics { TotalReceives = 1000 };

		// Assert
		stats.TotalReceives.ShouldBe(1000);
	}

	[Fact]
	public void AllowSettingTotalMessages()
	{
		// Arrange & Act
		var stats = new LongPollingStatistics { TotalMessages = 5000 };

		// Assert
		stats.TotalMessages.ShouldBe(5000);
	}

	[Fact]
	public void AllowSettingEmptyReceives()
	{
		// Arrange & Act
		var stats = new LongPollingStatistics { EmptyReceives = 100 };

		// Assert
		stats.EmptyReceives.ShouldBe(100);
	}

	[Fact]
	public void AllowSettingCurrentLoadFactor()
	{
		// Arrange & Act
		var stats = new LongPollingStatistics { CurrentLoadFactor = 0.75 };

		// Assert
		stats.CurrentLoadFactor.ShouldBe(0.75);
	}

	[Fact]
	public void AllowSettingCurrentWaitTime()
	{
		// Arrange & Act
		var stats = new LongPollingStatistics { CurrentWaitTime = TimeSpan.FromSeconds(20) };

		// Assert
		stats.CurrentWaitTime.ShouldBe(TimeSpan.FromSeconds(20));
	}

	[Fact]
	public void AllowSettingApiCallsSaved()
	{
		// Arrange & Act
		var stats = new LongPollingStatistics { ApiCallsSaved = 10000 };

		// Assert
		stats.ApiCallsSaved.ShouldBe(10000);
	}

	[Fact]
	public void AllowSettingLastReceiveTime()
	{
		// Arrange
		var lastReceive = DateTimeOffset.UtcNow;

		// Act
		var stats = new LongPollingStatistics { LastReceiveTime = lastReceive };

		// Assert
		stats.LastReceiveTime.ShouldBe(lastReceive);
	}

	[Fact]
	public void BeValueType()
	{
		// Assert
		typeof(LongPollingStatistics).IsValueType.ShouldBeTrue();
	}

	[Fact]
	public void BeRecordStruct()
	{
		// Assert - record structs have compiler-generated Equals
		var stats1 = new LongPollingStatistics { TotalReceives = 100, TotalMessages = 500 };
		var stats2 = new LongPollingStatistics { TotalReceives = 100, TotalMessages = 500 };

		stats1.Equals(stats2).ShouldBeTrue();
	}

	[Fact]
	public void HaveEqualityByValue()
	{
		// Arrange
		var stats1 = new LongPollingStatistics
		{
			TotalReceives = 100,
			TotalMessages = 500,
			EmptyReceives = 10,
			CurrentLoadFactor = 0.5,
			CurrentWaitTime = TimeSpan.FromSeconds(5),
			ApiCallsSaved = 1000,
		};
		var stats2 = new LongPollingStatistics
		{
			TotalReceives = 100,
			TotalMessages = 500,
			EmptyReceives = 10,
			CurrentLoadFactor = 0.5,
			CurrentWaitTime = TimeSpan.FromSeconds(5),
			ApiCallsSaved = 1000,
		};

		// Assert
		(stats1 == stats2).ShouldBeTrue();
		(stats1 != stats2).ShouldBeFalse();
	}
}
