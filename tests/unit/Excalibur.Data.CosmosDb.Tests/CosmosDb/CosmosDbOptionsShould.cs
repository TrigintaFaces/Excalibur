// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Azure.Cosmos;

using Excalibur.Data.CosmosDb;
namespace Excalibur.Data.Tests.CosmosDb;

/// <summary>
/// Unit tests for <see cref="CosmosDbOptions"/>.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Data)]
[Trait("Feature", "CosmosDb")]
public sealed class CosmosDbOptionsShould : UnitTestBase
{
	[Fact]
	public void HaveDefaultValues()
	{
		// Act
		var options = new CosmosDbOptions();

		// Assert
		options.Name.ShouldBe("CosmosDb");
		options.Client.AccountEndpoint.ShouldBeNull();
		options.Client.AccountKey.ShouldBeNull();
		options.Client.ConnectionString.ShouldBeNull();
		options.DatabaseName.ShouldBeNull();
		options.DefaultContainerName.ShouldBeNull();
		options.DefaultPartitionKeyPath.ShouldBe("/id");
		options.Client.ConsistencyLevel.ShouldBeNull();
		options.Client.ApplicationName.ShouldBeNull();
		options.Client.PreferredRegions.ShouldBeNull();
		options.Client.Resilience.EnableContentResponseOnWrite.ShouldBeFalse();
		options.Client.Resilience.MaxRetryAttempts.ShouldBe(9);
		options.Client.Resilience.MaxRetryWaitTimeInSeconds.ShouldBe(30);
		options.Client.UseDirectMode.ShouldBeTrue();
		options.MaxConnectionsPerEndpoint.ShouldBe(50);
		options.Client.Resilience.RequestTimeoutInSeconds.ShouldBe(30);
		options.IdleConnectionTimeoutInSeconds.ShouldBe(600);
		options.EnableTcpConnectionEndpointRediscovery.ShouldBeTrue();
		options.BulkExecutionMaxDegreeOfParallelism.ShouldBe(25);
		options.AllowBulkExecution.ShouldBeFalse();
	}

	[Fact]
	public void ValidateWithConnectionString()
	{
		// Arrange
		var options = new CosmosDbOptions
		{
			DatabaseName = "TestDb"
		};
		options.Client.ConnectionString = "AccountEndpoint=https://test.documents.azure.com:443/;AccountKey=test==";

		// Act & Assert - should not throw
		Should.NotThrow(() => options.Validate());
	}

	[Fact]
	public void ValidateWithEndpointAndKey()
	{
		// Arrange
		var options = new CosmosDbOptions
		{
			DatabaseName = "TestDb"
		};
		options.Client.AccountEndpoint = "https://test.documents.azure.com:443/";
		options.Client.AccountKey = "test==";

		// Act & Assert - should not throw
		Should.NotThrow(() => options.Validate());
	}

	[Fact]
	public void ThrowWhenNeitherConnectionStringNorEndpointProvided()
	{
		// Arrange
		var options = new CosmosDbOptions
		{
			DatabaseName = "TestDb"
		};

		// Act & Assert
		var exception = Should.Throw<InvalidOperationException>(() => options.Validate());
		exception.Message.ShouldContain("ConnectionString");
	}

	[Fact]
	public void ThrowWhenDatabaseNameMissing()
	{
		// Arrange
		var options = new CosmosDbOptions();
		options.Client.ConnectionString = "AccountEndpoint=https://test.documents.azure.com:443/;AccountKey=test==";

		// Act & Assert
		var exception = Should.Throw<InvalidOperationException>(() => options.Validate());
		exception.Message.ShouldContain("database");
	}

	[Fact]
	public void AllowCustomConfiguration()
	{
		// Act
		var options = new CosmosDbOptions
		{
			Client =
			{
				AccountEndpoint = "https://test.documents.azure.com:443/",
				AccountKey = "testKey==",
				ConsistencyLevel = Microsoft.Azure.Cosmos.ConsistencyLevel.Session,
				ApplicationName = "TestApp",
				PreferredRegions = ["East US", "West US"],
				UseDirectMode = false,
				Resilience =
				{
					EnableContentResponseOnWrite = false,
					MaxRetryAttempts = 5,
					MaxRetryWaitTimeInSeconds = 60,
					RequestTimeoutInSeconds = 120
				}
			},
			Name = "CustomCosmosDb",
			DatabaseName = "CustomDb",
			DefaultContainerName = "Items",
			DefaultPartitionKeyPath = "/tenantId",
			MaxConnectionsPerEndpoint = 100,
			IdleConnectionTimeoutInSeconds = 300,
			EnableTcpConnectionEndpointRediscovery = false,
			BulkExecutionMaxDegreeOfParallelism = 50,
			AllowBulkExecution = true
		};

		// Assert
		options.Name.ShouldBe("CustomCosmosDb");
		options.DefaultPartitionKeyPath.ShouldBe("/tenantId");
		options.Client.ConsistencyLevel.ShouldBe(Microsoft.Azure.Cosmos.ConsistencyLevel.Session);
		options.Client.PreferredRegions.ShouldContain("East US");
		options.Client.Resilience.EnableContentResponseOnWrite.ShouldBeFalse();
		options.AllowBulkExecution.ShouldBeTrue();
	}
}
