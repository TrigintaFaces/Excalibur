// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Options.Transport;

namespace Excalibur.Dispatch.Tests.Options.Transport;

/// <summary>
/// Unit tests for <see cref="AzureStorageQueueOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Options")]
[Trait("Priority", "0")]
public sealed class AzureStorageQueueOptionsShould
{
	#region Default Value Tests

	[Fact]
	public void Default_ConnectionString_IsEmpty()
	{
		// Arrange & Act
		var options = new AzureStorageQueueOptions();

		// Assert
		options.ConnectionString.ShouldBe(string.Empty);
	}

	[Fact]
	public void Default_MaxMessages_Is32()
	{
		// Arrange & Act
		var options = new AzureStorageQueueOptions();

		// Assert
		options.MaxMessages.ShouldBe(32);
	}

	[Fact]
	public void Default_VisibilityTimeout_Is10Minutes()
	{
		// Arrange & Act
		var options = new AzureStorageQueueOptions();

		// Assert
		options.VisibilityTimeout.ShouldBe(TimeSpan.FromMinutes(10));
	}

	[Fact]
	public void Default_PollingInterval_Is1Second()
	{
		// Arrange & Act
		var options = new AzureStorageQueueOptions();

		// Assert
		options.PollingInterval.ShouldBe(TimeSpan.FromSeconds(1));
	}

	#endregion

	#region Property Setter Tests

	[Fact]
	public void ConnectionString_CanBeSet()
	{
		// Arrange
		var options = new AzureStorageQueueOptions();

		// Act
		options.ConnectionString = "DefaultEndpointsProtocol=https;AccountName=myaccount;AccountKey=mykey;EndpointSuffix=core.windows.net";

		// Assert
		options.ConnectionString.ShouldContain("AccountName=myaccount");
	}

	[Fact]
	public void MaxMessages_CanBeSet()
	{
		// Arrange
		var options = new AzureStorageQueueOptions();

		// Act
		options.MaxMessages = 16;

		// Assert
		options.MaxMessages.ShouldBe(16);
	}

	[Fact]
	public void VisibilityTimeout_CanBeSet()
	{
		// Arrange
		var options = new AzureStorageQueueOptions();

		// Act
		options.VisibilityTimeout = TimeSpan.FromMinutes(5);

		// Assert
		options.VisibilityTimeout.ShouldBe(TimeSpan.FromMinutes(5));
	}

	[Fact]
	public void PollingInterval_CanBeSet()
	{
		// Arrange
		var options = new AzureStorageQueueOptions();

		// Act
		options.PollingInterval = TimeSpan.FromSeconds(5);

		// Assert
		options.PollingInterval.ShouldBe(TimeSpan.FromSeconds(5));
	}

	#endregion

	#region Object Initializer Tests

	[Fact]
	public void ObjectInitializer_SetsAllProperties()
	{
		// Act
		var options = new AzureStorageQueueOptions
		{
			ConnectionString = "UseDevelopmentStorage=true",
			MaxMessages = 10,
			VisibilityTimeout = TimeSpan.FromMinutes(2),
			PollingInterval = TimeSpan.FromMilliseconds(500),
		};

		// Assert
		options.ConnectionString.ShouldBe("UseDevelopmentStorage=true");
		options.MaxMessages.ShouldBe(10);
		options.VisibilityTimeout.ShouldBe(TimeSpan.FromMinutes(2));
		options.PollingInterval.ShouldBe(TimeSpan.FromMilliseconds(500));
	}

	#endregion

	#region Real-World Scenario Tests

	[Fact]
	public void Options_ForHighThroughput_UsesMaxMessages()
	{
		// Act
		var options = new AzureStorageQueueOptions
		{
			MaxMessages = 32,
			PollingInterval = TimeSpan.FromMilliseconds(100),
		};

		// Assert
		options.MaxMessages.ShouldBe(32);
		options.PollingInterval.ShouldBeLessThan(TimeSpan.FromSeconds(1));
	}

	[Fact]
	public void Options_ForDevelopment_UsesEmulator()
	{
		// Act
		var options = new AzureStorageQueueOptions
		{
			ConnectionString = "UseDevelopmentStorage=true",
		};

		// Assert
		options.ConnectionString.ShouldContain("Development");
	}

	[Fact]
	public void Options_ForLongRunningTasks_HasHigherVisibilityTimeout()
	{
		// Act
		var options = new AzureStorageQueueOptions
		{
			VisibilityTimeout = TimeSpan.FromMinutes(30),
		};

		// Assert
		options.VisibilityTimeout.ShouldBeGreaterThan(TimeSpan.FromMinutes(10));
	}

	#endregion
}
