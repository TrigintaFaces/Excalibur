// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Azure;

namespace Excalibur.Dispatch.Transport.Tests.AzureServiceBus.StorageQueues;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class AzureStorageQueueOptionsShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		// Arrange & Act
		var options = new AzureStorageQueueOptions();

		// Assert
		options.ConnectionString.ShouldBeNull();
		options.StorageAccountUri.ShouldBeNull();
		options.QueueName.ShouldBe(string.Empty);
		options.MaxConcurrentMessages.ShouldBe(10);
		options.Polling.VisibilityTimeout.ShouldBe(TimeSpan.FromMinutes(5));
		options.Polling.PollingInterval.ShouldBe(TimeSpan.FromSeconds(1));
		options.Polling.MaxMessages.ShouldBe(10);
		options.EnableEncryption.ShouldBeFalse();
		options.EncryptionProviderName.ShouldBeNull();
		options.DeadLetterQueueName.ShouldBeNull();
		options.MaxDequeueCount.ShouldBe(5);
		options.Polling.EmptyQueueDelayMs.ShouldBe(1000);
		options.Polling.EnableVerboseLogging.ShouldBeFalse();
		options.Polling.CustomProperties.ShouldBeEmpty();
	}

	[Fact]
	public void PollingSubOptionReturnCorrectValues()
	{
		// Arrange
		var options = new AzureStorageQueueOptions();
		options.Polling.PollingInterval = TimeSpan.FromMilliseconds(500);
		options.Polling.MaxMessages = 20;
		options.Polling.VisibilityTimeout = TimeSpan.FromMinutes(10);

		// Assert
		options.Polling.PollingInterval.ShouldBe(TimeSpan.FromMilliseconds(500));
		options.Polling.MaxMessages.ShouldBe(20);
		options.Polling.VisibilityTimeout.ShouldBe(TimeSpan.FromMinutes(10));
	}

	[Fact]
	public void ValidateThrowWhenNoConnectionConfigured()
	{
		// Arrange
		var options = new AzureStorageQueueOptions { QueueName = "test" };

		// Act & Assert
		Should.Throw<InvalidOperationException>(() => options.Validate())
			.Message.ShouldContain("ConnectionString or StorageAccountUri");
	}

	[Fact]
	public void ValidateThrowWhenQueueNameEmpty()
	{
		// Arrange
		var options = new AzureStorageQueueOptions { ConnectionString = "conn" };

		// Act & Assert
		Should.Throw<InvalidOperationException>(() => options.Validate())
			.Message.ShouldContain("QueueName");
	}

	[Fact]
	public void ValidateThrowWhenMaxConcurrentMessagesInvalid()
	{
		// Arrange
		var options = new AzureStorageQueueOptions
		{
			ConnectionString = "conn",
			QueueName = "queue",
			MaxConcurrentMessages = 0,
		};

		// Act & Assert
		Should.Throw<InvalidOperationException>(() => options.Validate())
			.Message.ShouldContain("MaxConcurrentMessages");
	}

	[Fact]
	public void ValidateThrowWhenVisibilityTimeoutOutOfRange()
	{
		// Arrange
		var options = new AzureStorageQueueOptions
		{
			ConnectionString = "conn",
			QueueName = "queue",
		};
		options.Polling.VisibilityTimeout = TimeSpan.FromDays(8);

		// Act & Assert
		Should.Throw<InvalidOperationException>(() => options.Validate())
			.Message.ShouldContain("VisibilityTimeout");
	}

	[Fact]
	public void ValidateThrowWhenMaxMessagesOutOfRange()
	{
		// Arrange
		var options = new AzureStorageQueueOptions
		{
			ConnectionString = "conn",
			QueueName = "queue",
		};
		options.Polling.MaxMessages = 33;

		// Act & Assert
		Should.Throw<InvalidOperationException>(() => options.Validate())
			.Message.ShouldContain("MaxMessages");
	}

	[Fact]
	public void ValidateSucceedWithValidConfig()
	{
		// Arrange
		var options = new AzureStorageQueueOptions
		{
			ConnectionString = "conn",
			QueueName = "queue",
		};

		// Act & Assert — should not throw
		options.Validate();
	}
}
