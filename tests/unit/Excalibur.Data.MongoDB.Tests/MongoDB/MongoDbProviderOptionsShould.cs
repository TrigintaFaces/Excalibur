// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.MongoDB;

namespace Excalibur.Data.Tests.MongoDB;

/// <summary>
/// Unit tests for MongoDbProviderOptions configuration.
/// </summary>
[Trait("Category", "Unit")]
public sealed class MongoDbProviderOptionsShould : UnitTestBase
{
	#region Default Values

	[Fact]
	public void Create_WithDefaults_HasExpectedDefaultValues()
	{
		// Arrange & Act
		var options = new MongoDbProviderOptions();

		// Assert
		options.Name.ShouldBeNull();
		options.ConnectionString.ShouldBe(string.Empty);
		options.DatabaseName.ShouldBe(string.Empty);
		options.ServerSelectionTimeout.ShouldBe(30);
		options.ConnectTimeout.ShouldBe(30);
		options.UseSsl.ShouldBeTrue();
		options.MaxPoolSize.ShouldBe(100);
		options.MinPoolSize.ShouldBe(0);
		options.UseTransactions.ShouldBeFalse();
		options.RetryCount.ShouldBe(3);
		options.IsReadOnly.ShouldBeFalse();
	}

	#endregion Default Values

	#region Property Customization

	[Fact]
	public void AllowCustomName()
	{
		// Arrange & Act
		var options = new MongoDbProviderOptions
		{
			Name = "custom-mongo"
		};

		// Assert
		options.Name.ShouldBe("custom-mongo");
	}

	[Fact]
	public void AllowCustomConnectionString()
	{
		// Arrange & Act
		var options = new MongoDbProviderOptions
		{
			ConnectionString = "mongodb://localhost:27017"
		};

		// Assert
		options.ConnectionString.ShouldBe("mongodb://localhost:27017");
	}

	[Fact]
	public void AllowCustomDatabaseName()
	{
		// Arrange & Act
		var options = new MongoDbProviderOptions
		{
			DatabaseName = "myDatabase"
		};

		// Assert
		options.DatabaseName.ShouldBe("myDatabase");
	}

	[Fact]
	public void AllowCustomTimeouts()
	{
		// Arrange & Act
		var options = new MongoDbProviderOptions
		{
			ServerSelectionTimeout = 60,
			ConnectTimeout = 45
		};

		// Assert
		options.ServerSelectionTimeout.ShouldBe(60);
		options.ConnectTimeout.ShouldBe(45);
	}

	[Fact]
	public void AllowCustomPoolSize()
	{
		// Arrange & Act
		var options = new MongoDbProviderOptions
		{
			MaxPoolSize = 200,
			MinPoolSize = 10
		};

		// Assert
		options.MaxPoolSize.ShouldBe(200);
		options.MinPoolSize.ShouldBe(10);
	}

	[Fact]
	public void AllowSslConfiguration()
	{
		// Arrange & Act
		var options = new MongoDbProviderOptions
		{
			UseSsl = false
		};

		// Assert
		options.UseSsl.ShouldBeFalse();
	}

	[Fact]
	public void AllowTransactionConfiguration()
	{
		// Arrange & Act
		var options = new MongoDbProviderOptions
		{
			UseTransactions = true
		};

		// Assert
		options.UseTransactions.ShouldBeTrue();
	}

	[Fact]
	public void AllowCustomRetryCount()
	{
		// Arrange & Act
		var options = new MongoDbProviderOptions
		{
			RetryCount = 5
		};

		// Assert
		options.RetryCount.ShouldBe(5);
	}

	[Fact]
	public void AllowReadOnlyConfiguration()
	{
		// Arrange & Act
		var options = new MongoDbProviderOptions
		{
			IsReadOnly = true
		};

		// Assert
		options.IsReadOnly.ShouldBeTrue();
	}

	#endregion Property Customization

	#region Complex Configurations

	[Fact]
	public void SupportProductionConfiguration()
	{
		// Arrange & Act
		var options = new MongoDbProviderOptions
		{
			Name = "production-mongo",
			ConnectionString = "mongodb+srv://user:password@cluster.mongodb.net",
			DatabaseName = "production_db",
			ServerSelectionTimeout = 60,
			ConnectTimeout = 45,
			UseSsl = true,
			MaxPoolSize = 200,
			MinPoolSize = 20,
			UseTransactions = true,
			RetryCount = 5,
			IsReadOnly = false
		};

		// Assert
		options.Name.ShouldBe("production-mongo");
		options.ConnectionString.ShouldBe("mongodb+srv://user:password@cluster.mongodb.net");
		options.DatabaseName.ShouldBe("production_db");
		options.ServerSelectionTimeout.ShouldBe(60);
		options.ConnectTimeout.ShouldBe(45);
		options.UseSsl.ShouldBeTrue();
		options.MaxPoolSize.ShouldBe(200);
		options.MinPoolSize.ShouldBe(20);
		options.UseTransactions.ShouldBeTrue();
		options.RetryCount.ShouldBe(5);
		options.IsReadOnly.ShouldBeFalse();
	}

	[Fact]
	public void SupportReadReplicaConfiguration()
	{
		// Arrange & Act
		var options = new MongoDbProviderOptions
		{
			Name = "mongo-replica",
			ConnectionString = "mongodb://replica.mongodb.example.com:27017",
			DatabaseName = "replica_db",
			IsReadOnly = true,
			UseTransactions = false
		};

		// Assert
		options.Name.ShouldBe("mongo-replica");
		options.IsReadOnly.ShouldBeTrue();
		options.UseTransactions.ShouldBeFalse();
	}

	[Fact]
	public void SupportLocalDevelopmentConfiguration()
	{
		// Arrange & Act
		var options = new MongoDbProviderOptions
		{
			Name = "local-mongo",
			ConnectionString = "mongodb://localhost:27017",
			DatabaseName = "dev_db",
			UseSsl = false,
			MaxPoolSize = 10,
			MinPoolSize = 1
		};

		// Assert
		options.Name.ShouldBe("local-mongo");
		options.ConnectionString.ShouldBe("mongodb://localhost:27017");
		options.UseSsl.ShouldBeFalse();
		options.MaxPoolSize.ShouldBe(10);
		options.MinPoolSize.ShouldBe(1);
	}

	#endregion Complex Configurations
}
