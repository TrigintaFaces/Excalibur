// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Azure;

namespace Excalibur.Dispatch.Transport.Tests.AzureServiceBus.EventHubs;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class AzureEventHubOptionsShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		// Arrange & Act
		var options = new AzureEventHubOptions();

		// Assert
		options.ConnectionString.ShouldBeNull();
		options.FullyQualifiedNamespace.ShouldBeNull();
		options.EventHubName.ShouldBe(string.Empty);
		options.ConsumerGroup.ShouldBe("$Default");
		options.PrefetchCount.ShouldBe(300);
		options.MaxBatchSize.ShouldBe(100);
		options.EnableEncryption.ShouldBeFalse();
		options.EncryptionProviderName.ShouldBeNull();
		options.StartingPosition.ShouldBe(EventHubStartingPosition.Latest);
		options.EnableVerboseLogging.ShouldBeFalse();
		options.CustomProperties.ShouldBeEmpty();
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		// Arrange & Act
		var options = new AzureEventHubOptions
		{
			ConnectionString = "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=...",
			FullyQualifiedNamespace = "test.servicebus.windows.net",
			EventHubName = "my-hub",
			ConsumerGroup = "my-group",
			PrefetchCount = 500,
			MaxBatchSize = 200,
			EnableEncryption = true,
			EncryptionProviderName = "aes-provider",
			StartingPosition = EventHubStartingPosition.Earliest,
			EnableVerboseLogging = true,
		};
		options.CustomProperties["env"] = "test";

		// Assert
		options.ConnectionString.ShouldNotBeNull();
		options.EventHubName.ShouldBe("my-hub");
		options.ConsumerGroup.ShouldBe("my-group");
		options.PrefetchCount.ShouldBe(500);
		options.MaxBatchSize.ShouldBe(200);
		options.EnableEncryption.ShouldBeTrue();
		options.StartingPosition.ShouldBe(EventHubStartingPosition.Earliest);
		options.CustomProperties.Count.ShouldBe(1);
	}

	[Fact]
	public void ValidateThrowWhenNoConnectionConfigured()
	{
		// Arrange
		var options = new AzureEventHubOptions { EventHubName = "test" };

		// Act & Assert
		Should.Throw<InvalidOperationException>(() => options.Validate())
			.Message.ShouldContain("ConnectionString or FullyQualifiedNamespace");
	}

	[Fact]
	public void ValidateThrowWhenEventHubNameEmpty()
	{
		// Arrange
		var options = new AzureEventHubOptions { ConnectionString = "conn" };

		// Act & Assert
		Should.Throw<InvalidOperationException>(() => options.Validate())
			.Message.ShouldContain("EventHubName");
	}

	[Fact]
	public void ValidateThrowWhenPrefetchCountNegative()
	{
		// Arrange
		var options = new AzureEventHubOptions
		{
			ConnectionString = "conn",
			EventHubName = "hub",
			PrefetchCount = -1,
		};

		// Act & Assert
		Should.Throw<InvalidOperationException>(() => options.Validate())
			.Message.ShouldContain("PrefetchCount");
	}

	[Fact]
	public void ValidateThrowWhenMaxBatchSizeOutOfRange()
	{
		// Arrange
		var options = new AzureEventHubOptions
		{
			ConnectionString = "conn",
			EventHubName = "hub",
			MaxBatchSize = 0,
		};

		// Act & Assert
		Should.Throw<InvalidOperationException>(() => options.Validate())
			.Message.ShouldContain("MaxBatchSize");
	}

	[Fact]
	public void ValidateSucceedWithValidConfig()
	{
		// Arrange
		var options = new AzureEventHubOptions
		{
			ConnectionString = "conn",
			EventHubName = "hub",
		};

		// Act & Assert — should not throw
		options.Validate();
	}

	[Fact]
	public void EventHubStartingPositionEnumHaveCorrectValues()
	{
		// Assert
		((int)EventHubStartingPosition.Earliest).ShouldBe(0);
		((int)EventHubStartingPosition.Latest).ShouldBe(1);
		((int)EventHubStartingPosition.FromTimestamp).ShouldBe(2);
	}
}
