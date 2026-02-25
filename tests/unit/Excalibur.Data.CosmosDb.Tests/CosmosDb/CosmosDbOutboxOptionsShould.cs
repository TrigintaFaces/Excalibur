// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.CosmosDb.Outbox;

namespace Excalibur.Data.Tests.CosmosDb;

/// <summary>
/// Unit tests for the <see cref="CosmosDbOutboxOptions"/> class.
/// </summary>
/// <remarks>
/// Sprint 514 (S514.3): CosmosDB unit tests.
/// Tests verify default values and validation.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "CosmosDb")]
[Trait("Feature", "Outbox")]
public sealed class CosmosDbOutboxOptionsShould
{
	#region Default Value Tests

	[Fact]
	public void ConnectionString_DefaultsToNull()
	{
		// Arrange & Act
		var options = new CosmosDbOutboxOptions();

		// Assert
		options.ConnectionString.ShouldBeNull();
	}

	[Fact]
	public void AccountEndpoint_DefaultsToNull()
	{
		// Arrange & Act
		var options = new CosmosDbOutboxOptions();

		// Assert
		options.AccountEndpoint.ShouldBeNull();
	}

	[Fact]
	public void AccountKey_DefaultsToNull()
	{
		// Arrange & Act
		var options = new CosmosDbOutboxOptions();

		// Assert
		options.AccountKey.ShouldBeNull();
	}

	[Fact]
	public void DatabaseName_DefaultsToNull()
	{
		// Arrange & Act
		var options = new CosmosDbOutboxOptions();

		// Assert
		options.DatabaseName.ShouldBeNull();
	}

	[Fact]
	public void ContainerName_DefaultsToOutbox()
	{
		// Arrange & Act
		var options = new CosmosDbOutboxOptions();

		// Assert
		options.ContainerName.ShouldBe("outbox");
	}

	[Fact]
	public void CreateContainerIfNotExists_DefaultsToTrue()
	{
		// Arrange & Act
		var options = new CosmosDbOutboxOptions();

		// Assert
		options.CreateContainerIfNotExists.ShouldBeTrue();
	}

	[Fact]
	public void ContainerThroughput_DefaultsTo400()
	{
		// Arrange & Act
		var options = new CosmosDbOutboxOptions();

		// Assert
		options.ContainerThroughput.ShouldBe(400);
	}

	[Fact]
	public void SentMessageTtlSeconds_DefaultsToSevenDays()
	{
		// Arrange & Act
		var options = new CosmosDbOutboxOptions();

		// Assert - 7 days = 604800 seconds
		options.SentMessageTtlSeconds.ShouldBe(604800);
	}

	[Fact]
	public void MaxRetryAttempts_DefaultsToNine()
	{
		// Arrange & Act
		var options = new CosmosDbOutboxOptions();

		// Assert
		options.MaxRetryAttempts.ShouldBe(9);
	}

	[Fact]
	public void MaxRetryWaitTimeInSeconds_DefaultsToThirty()
	{
		// Arrange & Act
		var options = new CosmosDbOutboxOptions();

		// Assert
		options.MaxRetryWaitTimeInSeconds.ShouldBe(30);
	}

	[Fact]
	public void RequestTimeoutInSeconds_DefaultsToThirty()
	{
		// Arrange & Act
		var options = new CosmosDbOutboxOptions();

		// Assert
		options.RequestTimeoutInSeconds.ShouldBe(30);
	}

	[Fact]
	public void UseDirectMode_DefaultsToTrue()
	{
		// Arrange & Act
		var options = new CosmosDbOutboxOptions();

		// Assert
		options.UseDirectMode.ShouldBeTrue();
	}

	[Fact]
	public void EnableContentResponseOnWrite_DefaultsToFalse()
	{
		// Arrange & Act
		var options = new CosmosDbOutboxOptions();

		// Assert
		options.EnableContentResponseOnWrite.ShouldBeFalse();
	}

	[Fact]
	public void ConsistencyLevel_DefaultsToNull()
	{
		// Arrange & Act
		var options = new CosmosDbOutboxOptions();

		// Assert
		options.ConsistencyLevel.ShouldBeNull();
	}

	[Fact]
	public void PreferredRegions_DefaultsToNull()
	{
		// Arrange & Act
		var options = new CosmosDbOutboxOptions();

		// Assert
		options.PreferredRegions.ShouldBeNull();
	}

	[Fact]
	public void HttpClientFactory_DefaultsToNull()
	{
		// Arrange & Act
		var options = new CosmosDbOutboxOptions();

		// Assert
		options.HttpClientFactory.ShouldBeNull();
	}

	#endregion

	#region Validation Tests

	[Fact]
	public void Validate_Throws_WhenNoConnectionInfo()
	{
		// Arrange
		var options = new CosmosDbOutboxOptions
		{
			DatabaseName = "test-db"
		};

		// Act & Assert
		Should.Throw<InvalidOperationException>(() => options.Validate());
	}

	[Fact]
	public void Validate_Succeeds_WithConnectionString()
	{
		// Arrange
		var options = new CosmosDbOutboxOptions
		{
			ConnectionString = "AccountEndpoint=https://test.documents.azure.com:443/;AccountKey=xyz",
			DatabaseName = "test-db"
		};

		// Act & Assert - Should not throw
		options.Validate();
	}

	[Fact]
	public void Validate_Succeeds_WithEndpointAndKey()
	{
		// Arrange
		var options = new CosmosDbOutboxOptions
		{
			AccountEndpoint = "https://test.documents.azure.com:443/",
			AccountKey = "xyz",
			DatabaseName = "test-db"
		};

		// Act & Assert - Should not throw
		options.Validate();
	}

	[Fact]
	public void Validate_Throws_WhenDatabaseNameNull()
	{
		// Arrange
		var options = new CosmosDbOutboxOptions
		{
			ConnectionString = "AccountEndpoint=https://test.documents.azure.com:443/;AccountKey=xyz",
			DatabaseName = null
		};

		// Act & Assert
		Should.Throw<InvalidOperationException>(() => options.Validate());
	}

	[Fact]
	public void Validate_Throws_WhenContainerNameEmpty()
	{
		// Arrange
		var options = new CosmosDbOutboxOptions
		{
			ConnectionString = "AccountEndpoint=https://test.documents.azure.com:443/;AccountKey=xyz",
			DatabaseName = "test-db",
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
		var options = new CosmosDbOutboxOptions
		{
			ConnectionString = "connection-string",
			AccountEndpoint = "https://test.documents.azure.com:443/",
			AccountKey = "test-key",
			DatabaseName = "my-db",
			ContainerName = "my-outbox",
			CreateContainerIfNotExists = false,
			ContainerThroughput = 1000,
			SentMessageTtlSeconds = 3600,
			MaxRetryAttempts = 5,
			MaxRetryWaitTimeInSeconds = 60,
			RequestTimeoutInSeconds = 120,
			UseDirectMode = false,
			EnableContentResponseOnWrite = true,
			PreferredRegions = new List<string> { "East US", "West US" },
			HttpClientFactory = () => new HttpClient()
		};

		// Assert
		options.ConnectionString.ShouldBe("connection-string");
		options.AccountEndpoint.ShouldBe("https://test.documents.azure.com:443/");
		options.AccountKey.ShouldBe("test-key");
		options.DatabaseName.ShouldBe("my-db");
		options.ContainerName.ShouldBe("my-outbox");
		options.CreateContainerIfNotExists.ShouldBeFalse();
		options.ContainerThroughput.ShouldBe(1000);
		options.SentMessageTtlSeconds.ShouldBe(3600);
		options.MaxRetryAttempts.ShouldBe(5);
		options.MaxRetryWaitTimeInSeconds.ShouldBe(60);
		options.RequestTimeoutInSeconds.ShouldBe(120);
		options.UseDirectMode.ShouldBeFalse();
		options.EnableContentResponseOnWrite.ShouldBeTrue();
		options.PreferredRegions.Count.ShouldBe(2);
		options.HttpClientFactory.ShouldNotBeNull();
	}

	#endregion
}
