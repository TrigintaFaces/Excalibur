// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.CosmosDb.Inbox;

namespace Excalibur.Data.Tests.CosmosDb;

/// <summary>
/// Unit tests for the <see cref="CosmosDbInboxOptions"/> class.
/// </summary>
/// <remarks>
/// Sprint 514 (S514.3): CosmosDB unit tests.
/// Tests verify default values and validation.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "CosmosDb")]
[Trait("Feature", "Inbox")]
public sealed class CosmosDbInboxOptionsShould
{
	#region Default Value Tests

	[Fact]
	public void AccountEndpoint_DefaultsToNull()
	{
		// Arrange & Act
		var options = new CosmosDbInboxOptions();

		// Assert
		options.AccountEndpoint.ShouldBeNull();
	}

	[Fact]
	public void AccountKey_DefaultsToNull()
	{
		// Arrange & Act
		var options = new CosmosDbInboxOptions();

		// Assert
		options.AccountKey.ShouldBeNull();
	}

	[Fact]
	public void ConnectionString_DefaultsToNull()
	{
		// Arrange & Act
		var options = new CosmosDbInboxOptions();

		// Assert
		options.ConnectionString.ShouldBeNull();
	}

	[Fact]
	public void DatabaseName_DefaultsToExcalibur()
	{
		// Arrange & Act
		var options = new CosmosDbInboxOptions();

		// Assert
		options.DatabaseName.ShouldBe("excalibur");
	}

	[Fact]
	public void ContainerName_DefaultsToInboxMessages()
	{
		// Arrange & Act
		var options = new CosmosDbInboxOptions();

		// Assert
		options.ContainerName.ShouldBe("inbox-messages");
	}

	[Fact]
	public void PartitionKeyPath_DefaultsToHandlerType()
	{
		// Arrange & Act
		var options = new CosmosDbInboxOptions();

		// Assert
		options.PartitionKeyPath.ShouldBe("/handler_type");
	}

	[Fact]
	public void ConsistencyLevel_DefaultsToNull()
	{
		// Arrange & Act
		var options = new CosmosDbInboxOptions();

		// Assert
		options.ConsistencyLevel.ShouldBeNull();
	}

	[Fact]
	public void MaxRetryAttempts_DefaultsToNine()
	{
		// Arrange & Act
		var options = new CosmosDbInboxOptions();

		// Assert
		options.MaxRetryAttempts.ShouldBe(9);
	}

	[Fact]
	public void MaxRetryWaitTimeInSeconds_DefaultsToThirty()
	{
		// Arrange & Act
		var options = new CosmosDbInboxOptions();

		// Assert
		options.MaxRetryWaitTimeInSeconds.ShouldBe(30);
	}

	[Fact]
	public void RequestTimeoutInSeconds_DefaultsToSixty()
	{
		// Arrange & Act
		var options = new CosmosDbInboxOptions();

		// Assert
		options.RequestTimeoutInSeconds.ShouldBe(60);
	}

	[Fact]
	public void DefaultTimeToLiveSeconds_DefaultsToSevenDays()
	{
		// Arrange & Act
		var options = new CosmosDbInboxOptions();

		// Assert - 7 days = 604800 seconds
		options.DefaultTimeToLiveSeconds.ShouldBe(604800);
	}

	[Fact]
	public void UseDirectMode_DefaultsToTrue()
	{
		// Arrange & Act
		var options = new CosmosDbInboxOptions();

		// Assert
		options.UseDirectMode.ShouldBeTrue();
	}

	[Fact]
	public void PreferredRegions_DefaultsToNull()
	{
		// Arrange & Act
		var options = new CosmosDbInboxOptions();

		// Assert
		options.PreferredRegions.ShouldBeNull();
	}

	[Fact]
	public void EnableContentResponseOnWrite_DefaultsToFalse()
	{
		// Arrange & Act
		var options = new CosmosDbInboxOptions();

		// Assert
		options.EnableContentResponseOnWrite.ShouldBeFalse();
	}

	#endregion

	#region Validation Tests

	[Fact]
	public void Validate_Throws_WhenNoConnectionInfo()
	{
		// Arrange
		var options = new CosmosDbInboxOptions();

		// Act & Assert
		Should.Throw<InvalidOperationException>(() => options.Validate());
	}

	[Fact]
	public void Validate_Succeeds_WithConnectionString()
	{
		// Arrange
		var options = new CosmosDbInboxOptions
		{
			ConnectionString = "AccountEndpoint=https://test.documents.azure.com:443/;AccountKey=xyz"
		};

		// Act & Assert - Should not throw
		options.Validate();
	}

	[Fact]
	public void Validate_Succeeds_WithEndpointAndKey()
	{
		// Arrange
		var options = new CosmosDbInboxOptions
		{
			AccountEndpoint = "https://test.documents.azure.com:443/",
			AccountKey = "xyz"
		};

		// Act & Assert - Should not throw
		options.Validate();
	}

	[Fact]
	public void Validate_Throws_WhenDatabaseNameEmpty()
	{
		// Arrange
		var options = new CosmosDbInboxOptions
		{
			ConnectionString = "AccountEndpoint=https://test.documents.azure.com:443/;AccountKey=xyz",
			DatabaseName = ""
		};

		// Act & Assert
		Should.Throw<InvalidOperationException>(() => options.Validate());
	}

	[Fact]
	public void Validate_Throws_WhenContainerNameEmpty()
	{
		// Arrange
		var options = new CosmosDbInboxOptions
		{
			ConnectionString = "AccountEndpoint=https://test.documents.azure.com:443/;AccountKey=xyz",
			ContainerName = ""
		};

		// Act & Assert
		Should.Throw<InvalidOperationException>(() => options.Validate());
	}

	#endregion

	#region Property Initialization Tests

	[Fact]
	public void AllProperties_CanBeInitialized()
	{
		// Arrange & Act
		var options = new CosmosDbInboxOptions
		{
			AccountEndpoint = "https://test.documents.azure.com:443/",
			AccountKey = "test-key",
			ConnectionString = "connection-string",
			DatabaseName = "my-db",
			ContainerName = "my-container",
			PartitionKeyPath = "/custom_key",
			MaxRetryAttempts = 5,
			MaxRetryWaitTimeInSeconds = 60,
			RequestTimeoutInSeconds = 120,
			DefaultTimeToLiveSeconds = 3600,
			UseDirectMode = false,
			PreferredRegions = new List<string> { "East US", "West US" },
			EnableContentResponseOnWrite = true
		};

		// Assert
		options.AccountEndpoint.ShouldBe("https://test.documents.azure.com:443/");
		options.AccountKey.ShouldBe("test-key");
		options.ConnectionString.ShouldBe("connection-string");
		options.DatabaseName.ShouldBe("my-db");
		options.ContainerName.ShouldBe("my-container");
		options.PartitionKeyPath.ShouldBe("/custom_key");
		options.MaxRetryAttempts.ShouldBe(5);
		options.MaxRetryWaitTimeInSeconds.ShouldBe(60);
		options.RequestTimeoutInSeconds.ShouldBe(120);
		options.DefaultTimeToLiveSeconds.ShouldBe(3600);
		options.UseDirectMode.ShouldBeFalse();
		options.PreferredRegions.Count.ShouldBe(2);
		options.EnableContentResponseOnWrite.ShouldBeTrue();
	}

	#endregion
}
