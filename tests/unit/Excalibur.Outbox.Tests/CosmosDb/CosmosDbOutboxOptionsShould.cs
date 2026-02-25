// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Outbox.Tests.CosmosDb;

/// <summary>
/// Unit tests for <see cref="CosmosDbOutboxOptions" />.
/// </summary>
[Trait("Category", "Unit")]
public sealed class CosmosDbOutboxOptionsShould : UnitTestBase
{
	[Fact]
	public void DefaultValues_AreCorrect()
	{
		// Act
		var options = new CosmosDbOutboxOptions();

		// Assert
		options.ConnectionString.ShouldBeNull();
		options.AccountEndpoint.ShouldBeNull();
		options.AccountKey.ShouldBeNull();
		options.DatabaseName.ShouldBeNull();
		options.ContainerName.ShouldBe("outbox");
		options.DefaultTimeToLiveSeconds.ShouldBe(604800); // 7 days
		options.MaxRetryAttempts.ShouldBe(9);
		options.MaxRetryWaitTimeInSeconds.ShouldBe(30);
		options.CreateContainerIfNotExists.ShouldBeTrue();
		options.ContainerThroughput.ShouldBe(400);
		options.UseDirectMode.ShouldBeTrue();
	}

	[Fact]
	public void ConnectionString_CanBeSet()
	{
		// Arrange
		var options = new CosmosDbOutboxOptions();
		const string connectionString = "AccountEndpoint=https://test.documents.azure.com:443/;AccountKey=testkey==";

		// Act
		options.ConnectionString = connectionString;

		// Assert
		options.ConnectionString.ShouldBe(connectionString);
	}

	[Fact]
	public void AccountEndpoint_CanBeSet()
	{
		// Arrange
		var options = new CosmosDbOutboxOptions();
		const string endpoint = "https://test.documents.azure.com:443/";

		// Act
		options.AccountEndpoint = endpoint;

		// Assert
		options.AccountEndpoint.ShouldBe(endpoint);
	}

	[Fact]
	public void AccountKey_CanBeSet()
	{
		// Arrange
		var options = new CosmosDbOutboxOptions();
		const string key = "testkey==";

		// Act
		options.AccountKey = key;

		// Assert
		options.AccountKey.ShouldBe(key);
	}

	[Fact]
	public void DatabaseName_CanBeSet()
	{
		// Arrange
		var options = new CosmosDbOutboxOptions();
		const string databaseName = "TestDatabase";

		// Act
		options.DatabaseName = databaseName;

		// Assert
		options.DatabaseName.ShouldBe(databaseName);
	}

	[Fact]
	public void ContainerName_CanBeSet()
	{
		// Arrange
		var options = new CosmosDbOutboxOptions();
		const string containerName = "custom-outbox";

		// Act
		options.ContainerName = containerName;

		// Assert
		options.ContainerName.ShouldBe(containerName);
	}

	[Fact]
	public void DefaultTimeToLiveSeconds_CanBeSet()
	{
		// Arrange
		var options = new CosmosDbOutboxOptions();
		const int ttl = -1; // Disable TTL

		// Act
		options.DefaultTimeToLiveSeconds = ttl;

		// Assert
		options.DefaultTimeToLiveSeconds.ShouldBe(ttl);
	}

	[Fact]
	public void Validate_Succeeds_WhenConnectionStringProvided()
	{
		// Arrange
		var options = new CosmosDbOutboxOptions
		{
			ConnectionString = "AccountEndpoint=https://test.documents.azure.com:443/;AccountKey=testkey==",
			DatabaseName = "TestDatabase"
		};

		// Act & Assert - Should not throw
		options.Validate();
	}

	[Fact]
	public void Validate_Succeeds_WhenAccountEndpointAndKeyProvided()
	{
		// Arrange
		var options = new CosmosDbOutboxOptions
		{
			AccountEndpoint = "https://test.documents.azure.com:443/",
			AccountKey = "testkey==",
			DatabaseName = "TestDatabase"
		};

		// Act & Assert - Should not throw
		options.Validate();
	}

	[Fact]
	public void Validate_ThrowsInvalidOperationException_WhenNoConnectionInfoProvided()
	{
		// Arrange
		var options = new CosmosDbOutboxOptions
		{
			DatabaseName = "TestDatabase"
		};

		// Act & Assert
		var exception = Should.Throw<InvalidOperationException>(() => options.Validate());
		exception.Message.ShouldContain("ConnectionString");
	}

	[Fact]
	public void Validate_ThrowsInvalidOperationException_WhenOnlyAccountEndpointProvided()
	{
		// Arrange
		var options = new CosmosDbOutboxOptions
		{
			AccountEndpoint = "https://test.documents.azure.com:443/",
			DatabaseName = "TestDatabase"
		};

		// Act & Assert
		var exception = Should.Throw<InvalidOperationException>(() => options.Validate());
		exception.Message.ShouldContain("AccountEndpoint");
	}

	[Fact]
	public void Validate_ThrowsInvalidOperationException_WhenOnlyAccountKeyProvided()
	{
		// Arrange
		var options = new CosmosDbOutboxOptions
		{
			AccountKey = "testkey==",
			DatabaseName = "TestDatabase"
		};

		// Act & Assert
		var exception = Should.Throw<InvalidOperationException>(() => options.Validate());
		exception.Message.ShouldContain("AccountKey");
	}

	[Fact]
	public void Validate_ThrowsInvalidOperationException_WhenDatabaseNameMissing()
	{
		// Arrange
		var options = new CosmosDbOutboxOptions
		{
			ConnectionString = "AccountEndpoint=https://test.documents.azure.com:443/;AccountKey=testkey=="
		};

		// Act & Assert
		var exception = Should.Throw<InvalidOperationException>(() => options.Validate());
		exception.Message.ShouldContain("DatabaseName");
	}
}
