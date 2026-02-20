// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.CosmosDb.Projections;

namespace Excalibur.Data.Tests.CosmosDb;

/// <summary>
/// Unit tests for the <see cref="CosmosDbProjectionStoreOptions"/> class.
/// </summary>
/// <remarks>
/// Sprint 514 (S514.3): CosmosDB unit tests.
/// Tests verify default values and validation.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "CosmosDb")]
[Trait("Feature", "Projections")]
public sealed class CosmosDbProjectionStoreOptionsShould
{
	#region Default Value Tests

	[Fact]
	public void AccountEndpoint_DefaultsToNull()
	{
		// Arrange & Act
		var options = new CosmosDbProjectionStoreOptions();

		// Assert
		options.AccountEndpoint.ShouldBeNull();
	}

	[Fact]
	public void AccountKey_DefaultsToNull()
	{
		// Arrange & Act
		var options = new CosmosDbProjectionStoreOptions();

		// Assert
		options.AccountKey.ShouldBeNull();
	}

	[Fact]
	public void ConnectionString_DefaultsToNull()
	{
		// Arrange & Act
		var options = new CosmosDbProjectionStoreOptions();

		// Assert
		options.ConnectionString.ShouldBeNull();
	}

	[Fact]
	public void DatabaseName_DefaultsToExcalibur()
	{
		// Arrange & Act
		var options = new CosmosDbProjectionStoreOptions();

		// Assert
		options.DatabaseName.ShouldBe("excalibur");
	}

	[Fact]
	public void ContainerName_DefaultsToProjections()
	{
		// Arrange & Act
		var options = new CosmosDbProjectionStoreOptions();

		// Assert
		options.ContainerName.ShouldBe("projections");
	}

	[Fact]
	public void PartitionKeyPath_DefaultsToProjectionType()
	{
		// Arrange & Act
		var options = new CosmosDbProjectionStoreOptions();

		// Assert
		options.PartitionKeyPath.ShouldBe("/projectionType");
	}

	[Fact]
	public void CreateContainerIfNotExists_DefaultsToTrue()
	{
		// Arrange & Act
		var options = new CosmosDbProjectionStoreOptions();

		// Assert
		options.CreateContainerIfNotExists.ShouldBeTrue();
	}

	[Fact]
	public void ContainerThroughput_DefaultsTo400()
	{
		// Arrange & Act
		var options = new CosmosDbProjectionStoreOptions();

		// Assert
		options.ContainerThroughput.ShouldBe(400);
	}

	[Fact]
	public void DefaultTtlSeconds_DefaultsToMinusOne()
	{
		// Arrange & Act
		var options = new CosmosDbProjectionStoreOptions();

		// Assert - -1 means no expiration
		options.DefaultTtlSeconds.ShouldBe(-1);
	}

	[Fact]
	public void ConsistencyLevel_DefaultsToNull()
	{
		// Arrange & Act
		var options = new CosmosDbProjectionStoreOptions();

		// Assert
		options.ConsistencyLevel.ShouldBeNull();
	}

	[Fact]
	public void MaxRetryAttempts_DefaultsToNine()
	{
		// Arrange & Act
		var options = new CosmosDbProjectionStoreOptions();

		// Assert
		options.MaxRetryAttempts.ShouldBe(9);
	}

	[Fact]
	public void MaxRetryWaitTimeInSeconds_DefaultsToThirty()
	{
		// Arrange & Act
		var options = new CosmosDbProjectionStoreOptions();

		// Assert
		options.MaxRetryWaitTimeInSeconds.ShouldBe(30);
	}

	[Fact]
	public void RequestTimeoutInSeconds_DefaultsToThirty()
	{
		// Arrange & Act
		var options = new CosmosDbProjectionStoreOptions();

		// Assert
		options.RequestTimeoutInSeconds.ShouldBe(30);
	}

	[Fact]
	public void UseDirectMode_DefaultsToTrue()
	{
		// Arrange & Act
		var options = new CosmosDbProjectionStoreOptions();

		// Assert
		options.UseDirectMode.ShouldBeTrue();
	}

	[Fact]
	public void EnableContentResponseOnWrite_DefaultsToFalse()
	{
		// Arrange & Act
		var options = new CosmosDbProjectionStoreOptions();

		// Assert
		options.EnableContentResponseOnWrite.ShouldBeFalse();
	}

	[Fact]
	public void PreferredRegions_DefaultsToNull()
	{
		// Arrange & Act
		var options = new CosmosDbProjectionStoreOptions();

		// Assert
		options.PreferredRegions.ShouldBeNull();
	}

	[Fact]
	public void HttpClientFactory_DefaultsToNull()
	{
		// Arrange & Act
		var options = new CosmosDbProjectionStoreOptions();

		// Assert
		options.HttpClientFactory.ShouldBeNull();
	}

	#endregion

	#region Validation Tests

	[Fact]
	public void Validate_Throws_WhenNoConnectionInfo()
	{
		// Arrange
		var options = new CosmosDbProjectionStoreOptions();

		// Act & Assert
		Should.Throw<InvalidOperationException>(() => options.Validate());
	}

	[Fact]
	public void Validate_Succeeds_WithConnectionString()
	{
		// Arrange
		var options = new CosmosDbProjectionStoreOptions
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
		var options = new CosmosDbProjectionStoreOptions
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
		var options = new CosmosDbProjectionStoreOptions
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
		var options = new CosmosDbProjectionStoreOptions
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
		var options = new CosmosDbProjectionStoreOptions
		{
			AccountEndpoint = "https://test.documents.azure.com:443/",
			AccountKey = "test-key",
			ConnectionString = "connection-string",
			DatabaseName = "my-db",
			ContainerName = "my-projections",
			PartitionKeyPath = "/customPath",
			CreateContainerIfNotExists = false,
			ContainerThroughput = 1000,
			DefaultTtlSeconds = 3600,
			MaxRetryAttempts = 5,
			MaxRetryWaitTimeInSeconds = 60,
			RequestTimeoutInSeconds = 120,
			UseDirectMode = false,
			EnableContentResponseOnWrite = true,
			PreferredRegions = new List<string> { "East US", "West US" },
			HttpClientFactory = () => new HttpClient()
		};

		// Assert
		options.AccountEndpoint.ShouldBe("https://test.documents.azure.com:443/");
		options.AccountKey.ShouldBe("test-key");
		options.ConnectionString.ShouldBe("connection-string");
		options.DatabaseName.ShouldBe("my-db");
		options.ContainerName.ShouldBe("my-projections");
		options.PartitionKeyPath.ShouldBe("/customPath");
		options.CreateContainerIfNotExists.ShouldBeFalse();
		options.ContainerThroughput.ShouldBe(1000);
		options.DefaultTtlSeconds.ShouldBe(3600);
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
