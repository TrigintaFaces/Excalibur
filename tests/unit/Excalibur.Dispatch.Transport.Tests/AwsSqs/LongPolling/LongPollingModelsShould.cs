// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using AwsPollingStatus = Excalibur.Dispatch.Transport.Aws.PollingStatus;
using Excalibur.Dispatch.Transport.Aws;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.LongPolling;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class LongPollingModelsShould
{
	[Fact]
	public void LongPollingResultHaveCorrectDefaults()
	{
		// Arrange & Act
		var result = new LongPollingResult();

		// Assert
		result.MessageCount.ShouldBe(0);
		result.ElapsedTime.ShouldBe(TimeSpan.Zero);
		result.IsEmpty.ShouldBeTrue();
		result.Timestamp.ShouldNotBe(default);
	}

	[Fact]
	public void LongPollingResultNotEmptyWithMessages()
	{
		// Arrange & Act
		var result = new LongPollingResult
		{
			MessageCount = 5,
			ElapsedTime = TimeSpan.FromMilliseconds(200),
		};

		// Assert
		result.MessageCount.ShouldBe(5);
		result.ElapsedTime.ShouldBe(TimeSpan.FromMilliseconds(200));
		result.IsEmpty.ShouldBeFalse();
	}

	[Fact]
	public void ReceiveOptionsAllowSettingAllProperties()
	{
		// Arrange & Act
		var options = new ReceiveOptions
		{
			MaxNumberOfMessages = 5,
			WaitTime = TimeSpan.FromSeconds(10),
			VisibilityTimeout = TimeSpan.FromMinutes(2),
			MessageAttributeNames = ["attr1", "attr2"],
			AttributeNames = ["All"],
		};

		// Assert
		options.MaxNumberOfMessages.ShouldBe(5);
		options.WaitTime.ShouldBe(TimeSpan.FromSeconds(10));
		options.VisibilityTimeout.ShouldBe(TimeSpan.FromMinutes(2));
		options.MessageAttributeNames.ShouldNotBeNull();
		options.MessageAttributeNames!.Count.ShouldBe(2);
		options.AttributeNames.ShouldNotBeNull();
		options.AttributeNames!.Count.ShouldBe(1);
	}

	[Fact]
	public void ReceiveOptionsHaveNullDefaults()
	{
		// Arrange & Act
		var options = new ReceiveOptions();

		// Assert
		options.MaxNumberOfMessages.ShouldBeNull();
		options.WaitTime.ShouldBeNull();
		options.VisibilityTimeout.ShouldBeNull();
		options.MessageAttributeNames.ShouldBeNull();
		options.AttributeNames.ShouldBeNull();
	}

	[Fact]
	public void OptimizationStatisticsAllowSettingAllProperties()
	{
		// Arrange
		var queueUrl = new Uri("https://sqs.us-east-1.amazonaws.com/123456789/test-q");
		var now = DateTimeOffset.UtcNow;

		// Act
		var stats = new OptimizationStatistics
		{
			QueueUrl = queueUrl,
			TotalMessages = 10000,
			ApiCallsSaved = 500,
			EfficiencyScore = 0.85,
			AverageLatency = TimeSpan.FromMilliseconds(15),
			EmptyReceiveRate = 0.02,
			LastUpdated = now,
		};

		// Assert
		stats.QueueUrl.ShouldBe(queueUrl);
		stats.TotalMessages.ShouldBe(10000);
		stats.ApiCallsSaved.ShouldBe(500);
		stats.EfficiencyScore.ShouldBe(0.85);
		stats.AverageLatency.ShouldBe(TimeSpan.FromMilliseconds(15));
		stats.EmptyReceiveRate.ShouldBe(0.02);
		stats.LastUpdated.ShouldBe(now);
	}

	[Fact]
	public void ReceiverStatisticsAllowSettingAllProperties()
	{
		// Arrange
		var now = DateTimeOffset.UtcNow;

		// Act
		var stats = new ReceiverStatistics
		{
			TotalReceiveOperations = 5000,
			TotalMessagesReceived = 25000,
			TotalMessagesDeleted = 24900,
			VisibilityTimeoutOptimizations = 100,
			LastReceiveTime = now,
			PollingStatus = AwsPollingStatus.Active,
		};

		// Assert
		stats.TotalReceiveOperations.ShouldBe(5000);
		stats.TotalMessagesReceived.ShouldBe(25000);
		stats.TotalMessagesDeleted.ShouldBe(24900);
		stats.VisibilityTimeoutOptimizations.ShouldBe(100);
		stats.LastReceiveTime.ShouldBe(now);
		stats.PollingStatus.ShouldBe(AwsPollingStatus.Active);
	}

	[Fact]
	public void HealthStatusHaveCorrectDefaults()
	{
		// Arrange & Act
		var health = new HealthStatus();

		// Assert
		health.IsHealthy.ShouldBeFalse();
		health.Status.ShouldBe("Initialized");
		health.ActiveQueues.ShouldBe(0);
		health.TotalMessagesProcessed.ShouldBe(0);
		health.EfficiencyScore.ShouldBe(0.0);
		health.Details.ShouldBeEmpty();
	}

	[Fact]
	public void HealthStatusAllowSettingAllProperties()
	{
		// Arrange
		var now = DateTimeOffset.UtcNow;

		// Act
		var health = new HealthStatus
		{
			IsHealthy = true,
			Status = "Running",
			ActiveQueues = 5,
			TotalMessagesProcessed = 100000,
			EfficiencyScore = 0.95,
			LastActivityTime = now,
		};
		health.Details["uptime"] = "48h";

		// Assert
		health.IsHealthy.ShouldBeTrue();
		health.Status.ShouldBe("Running");
		health.ActiveQueues.ShouldBe(5);
		health.TotalMessagesProcessed.ShouldBe(100000);
		health.EfficiencyScore.ShouldBe(0.95);
		health.LastActivityTime.ShouldBe(now);
		health.Details.Count.ShouldBe(1);
	}
}
