// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Inbox.CosmosDb;

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
		options.Client.AccountEndpoint.ShouldBeNull();
	}

	[Fact]
	public void AccountKey_DefaultsToNull()
	{
		// Arrange & Act
		var options = new CosmosDbInboxOptions();

		// Assert
		options.Client.AccountKey.ShouldBeNull();
	}

	[Fact]
	public void ConnectionString_DefaultsToNull()
	{
		// Arrange & Act
		var options = new CosmosDbInboxOptions();

		// Assert
		options.Client.ConnectionString.ShouldBeNull();
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
		options.Client.ConsistencyLevel.ShouldBeNull();
	}

	[Fact]
	public void MaxRetryAttempts_DefaultsToNine()
	{
		// Arrange & Act
		var options = new CosmosDbInboxOptions();

		// Assert
		options.Client.Resilience.MaxRetryAttempts.ShouldBe(9);
	}

	[Fact]
	public void MaxRetryWaitTimeInSeconds_DefaultsToThirty()
	{
		// Arrange & Act
		var options = new CosmosDbInboxOptions();

		// Assert
		options.Client.Resilience.MaxRetryWaitTimeInSeconds.ShouldBe(30);
	}

	[Fact]
	public void RequestTimeoutInSeconds_DefaultsToThirty()
	{
		// Arrange & Act
		var options = new CosmosDbInboxOptions();

		// Assert
		options.Client.Resilience.RequestTimeoutInSeconds.ShouldBe(30);
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
		options.Client.UseDirectMode.ShouldBeTrue();
	}

	[Fact]
	public void PreferredRegions_DefaultsToNull()
	{
		// Arrange & Act
		var options = new CosmosDbInboxOptions();

		// Assert
		options.Client.PreferredRegions.ShouldBeNull();
	}

	[Fact]
	public void EnableContentResponseOnWrite_DefaultsToFalse()
	{
		// Arrange & Act
		var options = new CosmosDbInboxOptions();

		// Assert
		options.Client.Resilience.EnableContentResponseOnWrite.ShouldBeFalse();
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
		var options = new CosmosDbInboxOptions();
		options.Client.ConnectionString = "AccountEndpoint=https://test.documents.azure.com:443/;AccountKey=xyz";

		// Act & Assert - Should not throw
		options.Validate();
	}

	[Fact]
	public void Validate_Succeeds_WithEndpointAndKey()
	{
		// Arrange
		var options = new CosmosDbInboxOptions();
		options.Client.AccountEndpoint = "https://test.documents.azure.com:443/";
		options.Client.AccountKey = "xyz";

		// Act & Assert - Should not throw
		options.Validate();
	}

	[Fact]
	public void Validate_Throws_WhenDatabaseNameEmpty()
	{
		// Arrange
		var options = new CosmosDbInboxOptions
		{
			DatabaseName = ""
		};
		options.Client.ConnectionString = "AccountEndpoint=https://test.documents.azure.com:443/;AccountKey=xyz";

		// Act & Assert
		Should.Throw<InvalidOperationException>(() => options.Validate());
	}

	[Fact]
	public void Validate_Throws_WhenContainerNameEmpty()
	{
		// Arrange
		var options = new CosmosDbInboxOptions
		{
			ContainerName = ""
		};
		options.Client.ConnectionString = "AccountEndpoint=https://test.documents.azure.com:443/;AccountKey=xyz";

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
			Client =
			{
				AccountEndpoint = "https://test.documents.azure.com:443/",
				AccountKey = "test-key",
				ConnectionString = "connection-string",
				UseDirectMode = false,
				PreferredRegions = new List<string> { "East US", "West US" },
				Resilience =
				{
					MaxRetryAttempts = 5,
					MaxRetryWaitTimeInSeconds = 60,
					RequestTimeoutInSeconds = 120,
					EnableContentResponseOnWrite = true
				}
			},
			DatabaseName = "my-db",
			ContainerName = "my-container",
			PartitionKeyPath = "/custom_key",
			DefaultTimeToLiveSeconds = 3600
		};

		// Assert
		options.Client.AccountEndpoint.ShouldBe("https://test.documents.azure.com:443/");
		options.Client.AccountKey.ShouldBe("test-key");
		options.Client.ConnectionString.ShouldBe("connection-string");
		options.DatabaseName.ShouldBe("my-db");
		options.ContainerName.ShouldBe("my-container");
		options.PartitionKeyPath.ShouldBe("/custom_key");
		options.Client.Resilience.MaxRetryAttempts.ShouldBe(5);
		options.Client.Resilience.MaxRetryWaitTimeInSeconds.ShouldBe(60);
		options.Client.Resilience.RequestTimeoutInSeconds.ShouldBe(120);
		options.DefaultTimeToLiveSeconds.ShouldBe(3600);
		options.Client.UseDirectMode.ShouldBeFalse();
		options.Client.PreferredRegions.Count.ShouldBe(2);
		options.Client.Resilience.EnableContentResponseOnWrite.ShouldBeTrue();
	}

	#endregion
}
