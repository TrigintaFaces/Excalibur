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
		options.VisibilityTimeout.ShouldBe(TimeSpan.FromMinutes(5));
		options.PollingInterval.ShouldBe(TimeSpan.FromSeconds(1));
		options.MaxMessages.ShouldBe(10);
		options.EnableEncryption.ShouldBeFalse();
		options.EncryptionProviderName.ShouldBeNull();
		options.DeadLetterQueueName.ShouldBeNull();
		options.MaxDequeueCount.ShouldBe(5);
		options.EmptyQueueDelayMs.ShouldBe(1000);
		options.EnableVerboseLogging.ShouldBeFalse();
		options.CustomProperties.ShouldBeEmpty();
	}

	[Fact]
	public void ComputedPropertiesReturnCorrectValues()
	{
		// Arrange
		var options = new AzureStorageQueueOptions
		{
			PollingInterval = TimeSpan.FromMilliseconds(500),
			MaxMessages = 20,
			VisibilityTimeout = TimeSpan.FromMinutes(10),
		};

		// Assert
		options.PollingIntervalMs.ShouldBe(500);
		options.MaxMessagesPerRequest.ShouldBe(20);
		options.VisibilityTimeoutSeconds.ShouldBe(600);
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
			VisibilityTimeout = TimeSpan.FromDays(8),
		};

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
			MaxMessages = 33,
		};

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
