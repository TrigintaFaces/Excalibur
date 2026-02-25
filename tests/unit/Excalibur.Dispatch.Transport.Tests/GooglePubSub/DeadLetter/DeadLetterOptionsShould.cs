// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using GoogleDeadLetterOptions = Excalibur.Dispatch.Transport.Google.DeadLetterOptions;

namespace Excalibur.Dispatch.Transport.Tests.GooglePubSub.DeadLetter;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class DeadLetterOptionsShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		// Arrange & Act
		var options = new GoogleDeadLetterOptions();

		// Assert
		options.DeadLetterTopicName.ShouldBeNull();
		options.DeadLetterSubscriptionName.ShouldBeNull();
		options.DefaultMaxDeliveryAttempts.ShouldBe(5);
		options.AutoCreateDeadLetterResources.ShouldBeTrue();
		options.DeadLetterRetentionDuration.ShouldBe(TimeSpan.FromDays(7));
		options.EnableAutomaticRetry.ShouldBeFalse();
		options.AutomaticRetryInterval.ShouldBe(TimeSpan.FromHours(1));
		options.AutomaticRetryBatchSize.ShouldBe(100);
		options.EnableMonitoring.ShouldBeTrue();
		options.MonitoringInterval.ShouldBe(TimeSpan.FromMinutes(5));
		options.AlertThresholdMessageCount.ShouldBe(1000);
		options.AlertThresholdMessageAge.ShouldBe(TimeSpan.FromHours(24));
		options.NonRetryableReasons.ShouldNotBeEmpty();
		options.NonRetryableReasons.ShouldContain("INVALID_MESSAGE_FORMAT");
		options.NonRetryableReasons.ShouldContain("UNAUTHORIZED");
		options.NonRetryableReasons.ShouldContain("MESSAGE_TOO_LARGE");
		options.NonRetryableReasons.ShouldContain("UNSUPPORTED_OPERATION");
		options.PreserveMessageOrdering.ShouldBeFalse();
		options.EnableCompression.ShouldBeTrue();
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		// Arrange & Act
		var options = new GoogleDeadLetterOptions
		{
			DefaultMaxDeliveryAttempts = 10,
			AutoCreateDeadLetterResources = false,
			DeadLetterRetentionDuration = TimeSpan.FromDays(14),
			EnableAutomaticRetry = true,
			AutomaticRetryInterval = TimeSpan.FromHours(2),
			AutomaticRetryBatchSize = 50,
			EnableMonitoring = false,
			MonitoringInterval = TimeSpan.FromMinutes(10),
			AlertThresholdMessageCount = 500,
			AlertThresholdMessageAge = TimeSpan.FromHours(12),
			PreserveMessageOrdering = true,
			EnableCompression = false,
		};

		// Assert
		options.DefaultMaxDeliveryAttempts.ShouldBe(10);
		options.AutoCreateDeadLetterResources.ShouldBeFalse();
		options.DeadLetterRetentionDuration.ShouldBe(TimeSpan.FromDays(14));
		options.EnableAutomaticRetry.ShouldBeTrue();
		options.AutomaticRetryInterval.ShouldBe(TimeSpan.FromHours(2));
		options.AutomaticRetryBatchSize.ShouldBe(50);
		options.EnableMonitoring.ShouldBeFalse();
		options.MonitoringInterval.ShouldBe(TimeSpan.FromMinutes(10));
		options.AlertThresholdMessageCount.ShouldBe(500);
		options.AlertThresholdMessageAge.ShouldBe(TimeSpan.FromHours(12));
		options.PreserveMessageOrdering.ShouldBeTrue();
		options.EnableCompression.ShouldBeFalse();
	}

	[Fact]
	public void ValidateThrowWhenMaxDeliveryAttemptsLessThanOne()
	{
		// Arrange
		var options = new GoogleDeadLetterOptions { DefaultMaxDeliveryAttempts = 0 };

		// Act & Assert
		Should.Throw<InvalidOperationException>(() => options.Validate())
			.Message.ShouldContain("DefaultMaxDeliveryAttempts");
	}

	[Fact]
	public void ValidateThrowWhenRetentionDurationTooShort()
	{
		// Arrange
		var options = new GoogleDeadLetterOptions { DeadLetterRetentionDuration = TimeSpan.FromMinutes(5) };

		// Act & Assert
		Should.Throw<InvalidOperationException>(() => options.Validate())
			.Message.ShouldContain("DeadLetterRetentionDuration");
	}

	[Fact]
	public void ValidateThrowWhenRetryIntervalTooShort()
	{
		// Arrange
		var options = new GoogleDeadLetterOptions { AutomaticRetryInterval = TimeSpan.FromSeconds(30) };

		// Act & Assert
		Should.Throw<InvalidOperationException>(() => options.Validate())
			.Message.ShouldContain("AutomaticRetryInterval");
	}

	[Fact]
	public void ValidateThrowWhenRetryBatchSizeTooLarge()
	{
		// Arrange
		var options = new GoogleDeadLetterOptions { AutomaticRetryBatchSize = 1001 };

		// Act & Assert
		Should.Throw<InvalidOperationException>(() => options.Validate())
			.Message.ShouldContain("AutomaticRetryBatchSize");
	}

	[Fact]
	public void ValidateThrowWhenMonitoringIntervalTooShort()
	{
		// Arrange
		var options = new GoogleDeadLetterOptions { MonitoringInterval = TimeSpan.FromSeconds(10) };

		// Act & Assert
		Should.Throw<InvalidOperationException>(() => options.Validate())
			.Message.ShouldContain("MonitoringInterval");
	}

	[Fact]
	public void ValidateThrowWhenAlertThresholdNegative()
	{
		// Arrange
		var options = new GoogleDeadLetterOptions { AlertThresholdMessageCount = -1 };

		// Act & Assert
		Should.Throw<InvalidOperationException>(() => options.Validate())
			.Message.ShouldContain("AlertThresholdMessageCount");
	}

	[Fact]
	public void ValidateSucceedWithValidConfig()
	{
		// Arrange
		var options = new GoogleDeadLetterOptions();

		// Act & Assert — should not throw
		options.Validate();
	}
}
