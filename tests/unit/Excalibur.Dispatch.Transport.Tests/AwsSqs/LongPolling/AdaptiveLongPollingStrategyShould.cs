// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Aws;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.LongPolling;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Transport)]
public sealed class AdaptiveLongPollingStrategyShould : IDisposable
{
	private readonly LongPollingOptions _config;
	private readonly AdaptiveLongPollingStrategy _strategy;

	public AdaptiveLongPollingStrategyShould()
	{
		_config = new LongPollingOptions
		{
			QueueUrl = new Uri("https://sqs.us-east-1.amazonaws.com/123456789/test-queue"),
		};
		_config.Polling.MaxWaitTimeSeconds = 20;
		_config.Polling.MinWaitTimeSeconds = 1;
		_config.Polling.MaxNumberOfMessages = 10;
		_config.Visibility.VisibilityTimeoutSeconds = 30;
		_config.Adaptive.Enabled = true;

		_strategy = new AdaptiveLongPollingStrategy(_config);
	}

	[Fact]
	public void HaveAdaptiveName()
	{
		_strategy.Name.ShouldBe("Adaptive");
	}

	[Fact]
	public async Task ReturnMaxWaitTimeWhenAdaptivePollingDisabled()
	{
		// Arrange
		var config = new LongPollingOptions
		{
			QueueUrl = new Uri("https://sqs.us-east-1.amazonaws.com/123456789/test-queue"),
		};
		config.Adaptive.Enabled = false;
		using var strategy = new AdaptiveLongPollingStrategy(config);

		// Act
		var waitTime = await strategy.CalculateOptimalWaitTimeAsync();

		// Assert
		waitTime.ShouldBe(config.Polling.MaxWaitTime);
	}

	[Fact]
	public async Task ReturnModerateWaitTimeInitially()
	{
		// Act
		var waitTime = await _strategy.CalculateOptimalWaitTimeAsync();

		// Assert — initial load factor is 0.5 (medium), should be interpolated
		waitTime.TotalSeconds.ShouldBeGreaterThan(0);
		waitTime.TotalSeconds.ShouldBeLessThanOrEqualTo(20);
	}

	[Fact]
	public async Task RecordReceiveResultAndUpdateStatistics()
	{
		// Act
		await _strategy.RecordReceiveResultAsync(5, TimeSpan.FromSeconds(10));
		var stats = await ((ILongPollingStrategyAdmin)_strategy).GetStatisticsAsync();

		// Assert
		stats.TotalReceives.ShouldBe(1);
		stats.TotalMessages.ShouldBe(5);
		stats.EmptyReceives.ShouldBe(0);
	}

	[Fact]
	public async Task TrackEmptyReceives()
	{
		// Act
		await _strategy.RecordReceiveResultAsync(0, TimeSpan.FromSeconds(5));
		var stats = await ((ILongPollingStrategyAdmin)_strategy).GetStatisticsAsync();

		// Assert
		stats.TotalReceives.ShouldBe(1);
		stats.TotalMessages.ShouldBe(0);
		stats.EmptyReceives.ShouldBe(1);
	}

	[Fact]
	public async Task ReturnLoadFactorBetween0And1()
	{
		// Act
		var loadFactor = await _strategy.GetCurrentLoadFactorAsync();

		// Assert
		loadFactor.ShouldBeGreaterThanOrEqualTo(0.0);
		loadFactor.ShouldBeLessThanOrEqualTo(1.0);
	}

	[Fact]
	public async Task AdjustLoadFactorWithHighMessageVolume()
	{
		// Arrange — simulate high load (10 messages per receive on max 10)
		for (var i = 0; i < 10; i++)
		{
			await _strategy.RecordReceiveResultAsync(10, TimeSpan.FromSeconds(1));
		}

		// Act
		var loadFactor = await _strategy.GetCurrentLoadFactorAsync();

		// Assert — load factor should be high
		loadFactor.ShouldBeGreaterThan(0.5);
	}

	[Fact]
	public async Task AdjustLoadFactorWithLowMessageVolume()
	{
		// Arrange — simulate low load (0-1 messages per receive)
		for (var i = 0; i < 10; i++)
		{
			await _strategy.RecordReceiveResultAsync(0, TimeSpan.FromSeconds(20));
		}

		// Act
		var loadFactor = await _strategy.GetCurrentLoadFactorAsync();

		// Assert — load factor should be low
		loadFactor.ShouldBeLessThan(0.5);
	}

	[Fact]
	public async Task ResetClearsAllStatistics()
	{
		// Arrange
		await _strategy.RecordReceiveResultAsync(5, TimeSpan.FromSeconds(10));
		await _strategy.RecordReceiveResultAsync(3, TimeSpan.FromSeconds(5));

		// Act
		var admin = (ILongPollingStrategyAdmin)_strategy;
		await admin.ResetAsync();
		var stats = await admin.GetStatisticsAsync();

		// Assert
		stats.TotalReceives.ShouldBe(0);
		stats.TotalMessages.ShouldBe(0);
		stats.EmptyReceives.ShouldBe(0);
		stats.ApiCallsSaved.ShouldBe(0);
	}

	[Fact]
	public async Task ResetResetsLoadFactor()
	{
		// Arrange
		for (var i = 0; i < 5; i++)
		{
			await _strategy.RecordReceiveResultAsync(10, TimeSpan.FromSeconds(1));
		}

		// Act
		await ((ILongPollingStrategyAdmin)_strategy).ResetAsync();
		var loadFactor = await _strategy.GetCurrentLoadFactorAsync();

		// Assert
		loadFactor.ShouldBe(0.5); // Default after reset
	}

	[Fact]
	public void ThrowInvalidOperationOnPollAsync()
	{
		// Act & Assert
		Should.Throw<InvalidOperationException>(
			() => _strategy.PollAsync<object>("https://example.com/queue", CancellationToken.None));
	}

	[Fact]
	public void ThrowWhenConfigurationIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => new AdaptiveLongPollingStrategy(null!));
	}

	[Fact]
	public async Task TrackApiCallsSaved()
	{
		// Arrange — simulate receives with wait times > 1s and empty results
		for (var i = 0; i < 5; i++)
		{
			await _strategy.RecordReceiveResultAsync(0, TimeSpan.FromSeconds(10));
		}

		// Act
		var stats = await ((ILongPollingStrategyAdmin)_strategy).GetStatisticsAsync();

		// Assert — should have saved some API calls
		stats.ApiCallsSaved.ShouldBeGreaterThan(0);
	}

	[Fact]
	public async Task ReportCorrectCurrentWaitTime()
	{
		// Act
		var stats = await ((ILongPollingStrategyAdmin)_strategy).GetStatisticsAsync();

		// Assert
		stats.CurrentWaitTime.TotalSeconds.ShouldBeGreaterThan(0);
	}

	[Fact]
	public void DisposeWithoutThrowing()
	{
		// Act & Assert — should not throw
		_strategy.Dispose();
		_strategy.Dispose(); // Double dispose should be safe
	}

	public void Dispose()
	{
		_strategy.Dispose();
	}
}
