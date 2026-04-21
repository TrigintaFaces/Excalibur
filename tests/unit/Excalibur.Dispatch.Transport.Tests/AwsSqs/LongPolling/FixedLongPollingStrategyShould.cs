// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Aws;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.LongPolling;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Transport)]
public sealed class FixedLongPollingStrategyShould
{
	[Fact]
	public void ThrowWhenConfigurationIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new FixedLongPollingStrategy((LongPollingOptions)null!));
	}

	[Fact]
	public void ThrowWhenWaitTimeIsNegative()
	{
		Should.Throw<ArgumentOutOfRangeException>(() =>
			new FixedLongPollingStrategy(TimeSpan.FromSeconds(-1)));
	}

	[Fact]
	public void ThrowWhenWaitTimeExceedsTwentySeconds()
	{
		Should.Throw<ArgumentOutOfRangeException>(() =>
			new FixedLongPollingStrategy(TimeSpan.FromSeconds(21)));
	}

	[Fact]
	public void CreateWithValidTimeSpan()
	{
		var strategy = new FixedLongPollingStrategy(TimeSpan.FromSeconds(10));
		strategy.Name.ShouldBe("Fixed");
	}

	[Fact]
	public void CreateWithConfiguration()
	{
		var config = new LongPollingOptions
		{
			QueueUrl = new Uri("https://sqs.us-east-1.amazonaws.com/123456789/my-queue"),
		};
		config.Polling.MaxWaitTimeSeconds = 15;
		var strategy = new FixedLongPollingStrategy(config);
		strategy.Name.ShouldBe("Fixed");
	}

	[Fact]
	public async Task ReturnFixedWaitTime()
	{
		// Arrange
		var strategy = new FixedLongPollingStrategy(TimeSpan.FromSeconds(10));

		// Act
		var waitTime = await strategy.CalculateOptimalWaitTimeAsync();

		// Assert
		waitTime.ShouldBe(TimeSpan.FromSeconds(10));
	}

	[Fact]
	public async Task TrackReceiveResults()
	{
		// Arrange
		var strategy = new FixedLongPollingStrategy(TimeSpan.FromSeconds(10));

		// Act
		await strategy.RecordReceiveResultAsync(5, TimeSpan.FromSeconds(3));
		await strategy.RecordReceiveResultAsync(0, TimeSpan.FromSeconds(10));
		await strategy.RecordReceiveResultAsync(3, TimeSpan.FromSeconds(5));

		// Assert
		var stats = await ((ILongPollingStrategyAdmin)strategy).GetStatisticsAsync();
		stats.TotalReceives.ShouldBe(3);
		stats.TotalMessages.ShouldBe(8);
		stats.EmptyReceives.ShouldBe(1);
		stats.CurrentWaitTime.ShouldBe(TimeSpan.FromSeconds(10));
	}

	[Fact]
	public async Task CalculateLoadFactor()
	{
		// Arrange
		var strategy = new FixedLongPollingStrategy(TimeSpan.FromSeconds(10));
		await strategy.RecordReceiveResultAsync(10, TimeSpan.FromSeconds(5));

		// Act
		var loadFactor = await strategy.GetCurrentLoadFactorAsync();

		// Assert — 10 messages / 10.0 max = 1.0, capped at 1.0
		loadFactor.ShouldBe(1.0);
	}

	[Fact]
	public async Task ReturnZeroLoadFactorWithNoReceives()
	{
		var strategy = new FixedLongPollingStrategy(TimeSpan.FromSeconds(10));

		var loadFactor = await strategy.GetCurrentLoadFactorAsync();

		loadFactor.ShouldBe(0.0);
	}

	[Fact]
	public async Task ResetStatistics()
	{
		// Arrange
		var strategy = new FixedLongPollingStrategy(TimeSpan.FromSeconds(10));
		await strategy.RecordReceiveResultAsync(5, TimeSpan.FromSeconds(3));

		// Act
		var admin = (ILongPollingStrategyAdmin)strategy;
		await admin.ResetAsync();

		// Assert
		var stats = await admin.GetStatisticsAsync();
		stats.TotalReceives.ShouldBe(0);
		stats.TotalMessages.ShouldBe(0);
		stats.EmptyReceives.ShouldBe(0);
		stats.ApiCallsSaved.ShouldBe(0);
	}

	[Fact]
	public async Task ThrowOnPollAsync()
	{
		var strategy = new FixedLongPollingStrategy(TimeSpan.FromSeconds(10));

		await Should.ThrowAsync<InvalidOperationException>(
			() => strategy.PollAsync<object>("https://sqs.example.com/queue", CancellationToken.None));
	}

	[Fact]
	public async Task TrackApiCallsSaved()
	{
		// Arrange
		var strategy = new FixedLongPollingStrategy(TimeSpan.FromSeconds(10));

		// Act — record empty receive with wait time > 1s
		await strategy.RecordReceiveResultAsync(0, TimeSpan.FromSeconds(5));

		// Assert — API calls saved should be > 0 (5/1 - 1 = 4 potential polls saved)
		var stats = await ((ILongPollingStrategyAdmin)strategy).GetStatisticsAsync();
		stats.ApiCallsSaved.ShouldBeGreaterThan(0);
	}

	[Fact]
	public void ImplementILongPollingStrategyAdmin()
	{
		var strategy = new FixedLongPollingStrategy(TimeSpan.FromSeconds(10));

		strategy.ShouldBeAssignableTo<ILongPollingStrategyAdmin>();
	}

	[Fact]
	public async Task AdminGetStatisticsAsync_ReturnsValidStats()
	{
		// Arrange
		var strategy = new FixedLongPollingStrategy(TimeSpan.FromSeconds(10));
		await strategy.RecordReceiveResultAsync(5, TimeSpan.FromSeconds(3));

		// Act -- call via admin interface
		ILongPollingStrategyAdmin admin = strategy;
		var stats = await admin.GetStatisticsAsync();

		// Assert
		stats.TotalReceives.ShouldBe(1);
		stats.TotalMessages.ShouldBe(5);
	}

	[Fact]
	public async Task AdminResetAsync_ClearsStats()
	{
		// Arrange
		var strategy = new FixedLongPollingStrategy(TimeSpan.FromSeconds(10));
		await strategy.RecordReceiveResultAsync(5, TimeSpan.FromSeconds(3));

		// Act -- reset via admin interface
		ILongPollingStrategyAdmin admin = strategy;
		await admin.ResetAsync();

		// Assert
		var stats = await admin.GetStatisticsAsync();
		stats.TotalReceives.ShouldBe(0);
		stats.TotalMessages.ShouldBe(0);
	}
}
