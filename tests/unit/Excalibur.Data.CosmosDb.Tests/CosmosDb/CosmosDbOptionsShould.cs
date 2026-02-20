// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Azure.Cosmos;

using Excalibur.Data.CosmosDb;
namespace Excalibur.Data.Tests.CosmosDb;

/// <summary>
/// Unit tests for <see cref="CosmosDbOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Data")]
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
		options.AccountEndpoint.ShouldBeNull();
		options.AccountKey.ShouldBeNull();
		options.ConnectionString.ShouldBeNull();
		options.DatabaseName.ShouldBeNull();
		options.DefaultContainerName.ShouldBeNull();
		options.DefaultPartitionKeyPath.ShouldBe("/id");
		options.ConsistencyLevel.ShouldBeNull();
		options.ApplicationName.ShouldBeNull();
		options.PreferredRegions.ShouldBeNull();
		options.EnableContentResponseOnWrite.ShouldBeTrue();
		options.MaxRetryAttempts.ShouldBe(9);
		options.MaxRetryWaitTimeInSeconds.ShouldBe(30);
		options.UseDirectMode.ShouldBeTrue();
		options.MaxConnectionsPerEndpoint.ShouldBe(50);
		options.RequestTimeoutInSeconds.ShouldBe(60);
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
			ConnectionString = "AccountEndpoint=https://test.documents.azure.com:443/;AccountKey=test==",
			DatabaseName = "TestDb"
		};

		// Act & Assert - should not throw
		Should.NotThrow(() => options.Validate());
	}

	[Fact]
	public void ValidateWithEndpointAndKey()
	{
		// Arrange
		var options = new CosmosDbOptions
		{
			AccountEndpoint = "https://test.documents.azure.com:443/",
			AccountKey = "test==",
			DatabaseName = "TestDb"
		};

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
		var options = new CosmosDbOptions
		{
			ConnectionString = "AccountEndpoint=https://test.documents.azure.com:443/;AccountKey=test=="
		};

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
			Name = "CustomCosmosDb",
			AccountEndpoint = "https://test.documents.azure.com:443/",
			AccountKey = "testKey==",
			DatabaseName = "CustomDb",
			DefaultContainerName = "Items",
			DefaultPartitionKeyPath = "/tenantId",
			ConsistencyLevel = Microsoft.Azure.Cosmos.ConsistencyLevel.Session,
			ApplicationName = "TestApp",
			PreferredRegions = ["East US", "West US"],
			EnableContentResponseOnWrite = false,
			MaxRetryAttempts = 5,
			MaxRetryWaitTimeInSeconds = 60,
			UseDirectMode = false,
			MaxConnectionsPerEndpoint = 100,
			RequestTimeoutInSeconds = 120,
			IdleConnectionTimeoutInSeconds = 300,
			EnableTcpConnectionEndpointRediscovery = false,
			BulkExecutionMaxDegreeOfParallelism = 50,
			AllowBulkExecution = true
		};

		// Assert
		options.Name.ShouldBe("CustomCosmosDb");
		options.DefaultPartitionKeyPath.ShouldBe("/tenantId");
		options.ConsistencyLevel.ShouldBe(Microsoft.Azure.Cosmos.ConsistencyLevel.Session);
		options.PreferredRegions.ShouldContain("East US");
		options.EnableContentResponseOnWrite.ShouldBeFalse();
		options.AllowBulkExecution.ShouldBeTrue();
	}
}
