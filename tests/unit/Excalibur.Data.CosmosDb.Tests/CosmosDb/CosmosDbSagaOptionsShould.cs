// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga.CosmosDb;

namespace Excalibur.Data.Tests.CosmosDb;

/// <summary>
/// Unit tests for the <see cref="CosmosDbSagaOptions"/> class.
/// </summary>
/// <remarks>
/// Sprint 514 (S514.3): CosmosDB unit tests.
/// Tests verify default values and validation.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "CosmosDb")]
[Trait("Feature", "Saga")]
public sealed class CosmosDbSagaOptionsShould
{
	#region Default Value Tests

	[Fact]
	public void AccountEndpoint_DefaultsToNull()
	{
		// Arrange & Act
		var options = new CosmosDbSagaOptions();

		// Assert
		options.Client.AccountEndpoint.ShouldBeNull();
	}

	[Fact]
	public void AccountKey_DefaultsToNull()
	{
		// Arrange & Act
		var options = new CosmosDbSagaOptions();

		// Assert
		options.Client.AccountKey.ShouldBeNull();
	}

	[Fact]
	public void ConnectionString_DefaultsToNull()
	{
		// Arrange & Act
		var options = new CosmosDbSagaOptions();

		// Assert
		options.Client.ConnectionString.ShouldBeNull();
	}

	[Fact]
	public void DatabaseName_DefaultsToExcalibur()
	{
		// Arrange & Act
		var options = new CosmosDbSagaOptions();

		// Assert
		options.DatabaseName.ShouldBe("excalibur");
	}

	[Fact]
	public void ContainerName_DefaultsToSagas()
	{
		// Arrange & Act
		var options = new CosmosDbSagaOptions();

		// Assert
		options.ContainerName.ShouldBe("sagas");
	}

	[Fact]
	public void PartitionKeyPath_DefaultsToSagaType()
	{
		// Arrange & Act
		var options = new CosmosDbSagaOptions();

		// Assert
		options.PartitionKeyPath.ShouldBe("/sagaType");
	}

	[Fact]
	public void CreateContainerIfNotExists_DefaultsToTrue()
	{
		// Arrange & Act
		var options = new CosmosDbSagaOptions();

		// Assert
		options.CreateContainerIfNotExists.ShouldBeTrue();
	}

	[Fact]
	public void ContainerThroughput_DefaultsTo400()
	{
		// Arrange & Act
		var options = new CosmosDbSagaOptions();

		// Assert
		options.ContainerThroughput.ShouldBe(400);
	}

	[Fact]
	public void DefaultTtlSeconds_DefaultsToMinusOne()
	{
		// Arrange & Act
		var options = new CosmosDbSagaOptions();

		// Assert - -1 means no expiration
		options.DefaultTtlSeconds.ShouldBe(-1);
	}

	[Fact]
	public void ConsistencyLevel_DefaultsToNull()
	{
		// Arrange & Act
		var options = new CosmosDbSagaOptions();

		// Assert
		options.Client.ConsistencyLevel.ShouldBeNull();
	}

	[Fact]
	public void MaxRetryAttempts_DefaultsToNine()
	{
		// Arrange & Act
		var options = new CosmosDbSagaOptions();

		// Assert
		options.Client.Resilience.MaxRetryAttempts.ShouldBe(9);
	}

	[Fact]
	public void MaxRetryWaitTimeInSeconds_DefaultsToThirty()
	{
		// Arrange & Act
		var options = new CosmosDbSagaOptions();

		// Assert
		options.Client.Resilience.MaxRetryWaitTimeInSeconds.ShouldBe(30);
	}

	[Fact]
	public void RequestTimeoutInSeconds_DefaultsToThirty()
	{
		// Arrange & Act
		var options = new CosmosDbSagaOptions();

		// Assert
		options.Client.Resilience.RequestTimeoutInSeconds.ShouldBe(30);
	}

	[Fact]
	public void UseDirectMode_DefaultsToTrue()
	{
		// Arrange & Act
		var options = new CosmosDbSagaOptions();

		// Assert
		options.Client.UseDirectMode.ShouldBeTrue();
	}

	[Fact]
	public void EnableContentResponseOnWrite_DefaultsToFalse()
	{
		// Arrange & Act
		var options = new CosmosDbSagaOptions();

		// Assert
		options.Client.Resilience.EnableContentResponseOnWrite.ShouldBeFalse();
	}

	[Fact]
	public void PreferredRegions_DefaultsToNull()
	{
		// Arrange & Act
		var options = new CosmosDbSagaOptions();

		// Assert
		options.Client.PreferredRegions.ShouldBeNull();
	}

	[Fact]
	public void HttpClientFactory_DefaultsToNull()
	{
		// Arrange & Act
		var options = new CosmosDbSagaOptions();

		// Assert
		options.Client.HttpClientFactory.ShouldBeNull();
	}

	#endregion

	#region Validation Tests

	[Fact]
	public void Validate_Throws_WhenNoConnectionInfo()
	{
		// Arrange
		var options = new CosmosDbSagaOptions();

		// Act & Assert
		Should.Throw<InvalidOperationException>(() => options.Validate());
	}

	[Fact]
	public void Validate_Succeeds_WithConnectionString()
	{
		// Arrange
		var options = new CosmosDbSagaOptions();
		options.Client.ConnectionString = "AccountEndpoint=https://test.documents.azure.com:443/;AccountKey=xyz";

		// Act & Assert - Should not throw
		options.Validate();
	}

	[Fact]
	public void Validate_Succeeds_WithEndpointAndKey()
	{
		// Arrange
		var options = new CosmosDbSagaOptions();
		options.Client.AccountEndpoint = "https://test.documents.azure.com:443/";
		options.Client.AccountKey = "xyz";

		// Act & Assert - Should not throw
		options.Validate();
	}

	[Fact]
	public void Validate_Throws_WhenDatabaseNameEmpty()
	{
		// Arrange
		var options = new CosmosDbSagaOptions
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
		var options = new CosmosDbSagaOptions
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
		var options = new CosmosDbSagaOptions
		{
			Client =
			{
				AccountEndpoint = "https://test.documents.azure.com:443/",
				AccountKey = "test-key",
				ConnectionString = "connection-string",
				UseDirectMode = false,
				PreferredRegions = new List<string> { "East US" },
				HttpClientFactory = () => new HttpClient(),
				Resilience =
				{
					MaxRetryAttempts = 5,
					MaxRetryWaitTimeInSeconds = 60,
					RequestTimeoutInSeconds = 120,
					EnableContentResponseOnWrite = true
				}
			},
			DatabaseName = "my-db",
			ContainerName = "my-sagas",
			PartitionKeyPath = "/customPath",
			CreateContainerIfNotExists = false,
			ContainerThroughput = 1000,
			DefaultTtlSeconds = 3600
		};

		// Assert
		options.Client.AccountEndpoint.ShouldBe("https://test.documents.azure.com:443/");
		options.Client.AccountKey.ShouldBe("test-key");
		options.Client.ConnectionString.ShouldBe("connection-string");
		options.DatabaseName.ShouldBe("my-db");
		options.ContainerName.ShouldBe("my-sagas");
		options.PartitionKeyPath.ShouldBe("/customPath");
		options.CreateContainerIfNotExists.ShouldBeFalse();
		options.ContainerThroughput.ShouldBe(1000);
		options.DefaultTtlSeconds.ShouldBe(3600);
		options.Client.Resilience.MaxRetryAttempts.ShouldBe(5);
		options.Client.Resilience.MaxRetryWaitTimeInSeconds.ShouldBe(60);
		options.Client.Resilience.RequestTimeoutInSeconds.ShouldBe(120);
		options.Client.UseDirectMode.ShouldBeFalse();
		options.Client.Resilience.EnableContentResponseOnWrite.ShouldBeTrue();
		options.Client.PreferredRegions.Count.ShouldBe(1);
		options.Client.HttpClientFactory.ShouldNotBeNull();
	}

	#endregion
}
