// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Google;

namespace Excalibur.Dispatch.Transport.Tests.GooglePubSub.OrderingKey;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class OrderingKeyOptionsShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		// Arrange & Act
		var options = new OrderingKeyOptions();

		// Assert
		options.MaxConcurrentOrderingKeys.ShouldBe(Environment.ProcessorCount);
		options.MaxMessagesPerOrderingKey.ShouldBe(1000);
		options.RemoveEmptyQueues.ShouldBeTrue();
		options.QueueCleanupTimeout.ShouldBe(TimeSpan.FromMinutes(5));
		options.EnableMetrics.ShouldBeTrue();
		options.MessageStaleTimeout.ShouldBe(TimeSpan.FromSeconds(30));
		options.EnforceStrictOrdering.ShouldBeFalse();
		options.MaxRetries.ShouldBe(3);
		options.RetryDelay.ShouldBe(TimeSpan.FromSeconds(1));
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		// Arrange & Act
		var options = new OrderingKeyOptions
		{
			MaxConcurrentOrderingKeys = 8,
			MaxMessagesPerOrderingKey = 500,
			RemoveEmptyQueues = false,
			QueueCleanupTimeout = TimeSpan.FromMinutes(10),
			EnableMetrics = false,
			MessageStaleTimeout = TimeSpan.FromSeconds(60),
			EnforceStrictOrdering = true,
			MaxRetries = 5,
			RetryDelay = TimeSpan.FromSeconds(2),
		};

		// Assert
		options.MaxConcurrentOrderingKeys.ShouldBe(8);
		options.MaxMessagesPerOrderingKey.ShouldBe(500);
		options.RemoveEmptyQueues.ShouldBeFalse();
		options.QueueCleanupTimeout.ShouldBe(TimeSpan.FromMinutes(10));
		options.EnableMetrics.ShouldBeFalse();
		options.MessageStaleTimeout.ShouldBe(TimeSpan.FromSeconds(60));
		options.EnforceStrictOrdering.ShouldBeTrue();
		options.MaxRetries.ShouldBe(5);
		options.RetryDelay.ShouldBe(TimeSpan.FromSeconds(2));
	}

	[Fact]
	public void ValidateThrowWhenMaxConcurrentOrderingKeysZero()
	{
		// Arrange
		var options = new OrderingKeyOptions { MaxConcurrentOrderingKeys = 0 };

		// Act & Assert
		Should.Throw<ArgumentException>(() => options.Validate())
			.Message.ShouldContain("MaxConcurrentOrderingKeys");
	}

	[Fact]
	public void ValidateThrowWhenMaxMessagesPerOrderingKeyZero()
	{
		// Arrange
		var options = new OrderingKeyOptions { MaxMessagesPerOrderingKey = 0 };

		// Act & Assert
		Should.Throw<ArgumentException>(() => options.Validate())
			.Message.ShouldContain("MaxMessagesPerOrderingKey");
	}

	[Fact]
	public void ValidateThrowWhenQueueCleanupTimeoutZero()
	{
		// Arrange
		var options = new OrderingKeyOptions { QueueCleanupTimeout = TimeSpan.Zero };

		// Act & Assert
		Should.Throw<ArgumentException>(() => options.Validate())
			.Message.ShouldContain("QueueCleanupTimeout");
	}

	[Fact]
	public void ValidateThrowWhenMessageStaleTimeoutZero()
	{
		// Arrange
		var options = new OrderingKeyOptions { MessageStaleTimeout = TimeSpan.Zero };

		// Act & Assert
		Should.Throw<ArgumentException>(() => options.Validate())
			.Message.ShouldContain("MessageStaleTimeout");
	}

	[Fact]
	public void ValidateThrowWhenMaxRetriesNegative()
	{
		// Arrange
		var options = new OrderingKeyOptions { MaxRetries = -1 };

		// Act & Assert
		Should.Throw<ArgumentException>(() => options.Validate())
			.Message.ShouldContain("MaxRetries");
	}

	[Fact]
	public void ValidateThrowWhenRetryDelayNegative()
	{
		// Arrange
		var options = new OrderingKeyOptions { RetryDelay = TimeSpan.FromSeconds(-1) };

		// Act & Assert
		Should.Throw<ArgumentException>(() => options.Validate())
			.Message.ShouldContain("RetryDelay");
	}

	[Fact]
	public void ValidateSucceedWithValidConfig()
	{
		// Arrange
		var options = new OrderingKeyOptions();

		// Act & Assert — should not throw
		options.Validate();
	}
}
