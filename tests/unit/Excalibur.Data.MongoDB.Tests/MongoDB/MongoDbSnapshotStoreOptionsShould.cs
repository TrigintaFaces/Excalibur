// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.MongoDB.Snapshots;

using Excalibur.Data.MongoDB;

namespace Excalibur.Data.Tests.MongoDB.Snapshots;

/// <summary>
/// Unit tests for <see cref="MongoDbSnapshotStoreOptions"/> configuration.
/// </summary>
[Trait("Category", "Unit")]
public sealed class MongoDbSnapshotStoreOptionsShould : UnitTestBase
{
	[Fact]
	public void Create_WithDefaults_HasExpectedDefaultValues()
	{
		// Arrange & Act
		var options = new MongoDbSnapshotStoreOptions();

		// Assert
		options.ConnectionString.ShouldBe("mongodb://localhost:27017");
		options.DatabaseName.ShouldBe("excalibur");
		options.CollectionName.ShouldBe("snapshots");
		options.ServerSelectionTimeoutSeconds.ShouldBe(30);
		options.ConnectTimeoutSeconds.ShouldBe(30);
		options.UseSsl.ShouldBeFalse();
		options.MaxPoolSize.ShouldBe(100);
	}

	[Fact]
	public void ConnectionString_CanBeCustomized()
	{
		// Arrange & Act
		var options = new MongoDbSnapshotStoreOptions
		{
			ConnectionString = "mongodb://myhost:27017"
		};

		// Assert
		options.ConnectionString.ShouldBe("mongodb://myhost:27017");
	}

	[Fact]
	public void DatabaseName_CanBeCustomized()
	{
		// Arrange & Act
		var options = new MongoDbSnapshotStoreOptions
		{
			DatabaseName = "my_database"
		};

		// Assert
		options.DatabaseName.ShouldBe("my_database");
	}

	[Fact]
	public void CollectionName_CanBeCustomized()
	{
		// Arrange & Act
		var options = new MongoDbSnapshotStoreOptions
		{
			CollectionName = "aggregate_snapshots"
		};

		// Assert
		options.CollectionName.ShouldBe("aggregate_snapshots");
	}

	[Fact]
	public void Validate_WithValidOptions_DoesNotThrow()
	{
		// Arrange
		var options = new MongoDbSnapshotStoreOptions();

		// Act & Assert
		Should.NotThrow(() => options.Validate());
	}

	[Fact]
	public void Validate_WithNullConnectionString_ThrowsInvalidOperationException()
	{
		// Arrange
		var options = new MongoDbSnapshotStoreOptions { ConnectionString = null! };

		// Act & Assert
		var exception = Should.Throw<InvalidOperationException>(() => options.Validate());
		exception.Message.ShouldContain("ConnectionString");
	}

	[Fact]
	public void Validate_WithEmptyConnectionString_ThrowsInvalidOperationException()
	{
		// Arrange
		var options = new MongoDbSnapshotStoreOptions { ConnectionString = string.Empty };

		// Act & Assert
		var exception = Should.Throw<InvalidOperationException>(() => options.Validate());
		exception.Message.ShouldContain("ConnectionString");
	}

	[Fact]
	public void Validate_WithNullDatabaseName_ThrowsInvalidOperationException()
	{
		// Arrange
		var options = new MongoDbSnapshotStoreOptions { DatabaseName = null! };

		// Act & Assert
		var exception = Should.Throw<InvalidOperationException>(() => options.Validate());
		exception.Message.ShouldContain("DatabaseName");
	}

	[Fact]
	public void Validate_WithEmptyDatabaseName_ThrowsInvalidOperationException()
	{
		// Arrange
		var options = new MongoDbSnapshotStoreOptions { DatabaseName = string.Empty };

		// Act & Assert
		var exception = Should.Throw<InvalidOperationException>(() => options.Validate());
		exception.Message.ShouldContain("DatabaseName");
	}

	[Fact]
	public void Validate_WithNullCollectionName_ThrowsInvalidOperationException()
	{
		// Arrange
		var options = new MongoDbSnapshotStoreOptions { CollectionName = null! };

		// Act & Assert
		var exception = Should.Throw<InvalidOperationException>(() => options.Validate());
		exception.Message.ShouldContain("CollectionName");
	}

	[Fact]
	public void Validate_WithEmptyCollectionName_ThrowsInvalidOperationException()
	{
		// Arrange
		var options = new MongoDbSnapshotStoreOptions { CollectionName = string.Empty };

		// Act & Assert
		var exception = Should.Throw<InvalidOperationException>(() => options.Validate());
		exception.Message.ShouldContain("CollectionName");
	}

	[Fact]
	public void AllProperties_CanBeSetViaInitializer()
	{
		// Arrange & Act
		var options = new MongoDbSnapshotStoreOptions
		{
			ConnectionString = "mongodb://custom:27017",
			DatabaseName = "custom_db",
			CollectionName = "custom_snapshots",
			ServerSelectionTimeoutSeconds = 60,
			ConnectTimeoutSeconds = 45,
			UseSsl = true,
			MaxPoolSize = 200
		};

		// Assert
		options.ConnectionString.ShouldBe("mongodb://custom:27017");
		options.DatabaseName.ShouldBe("custom_db");
		options.CollectionName.ShouldBe("custom_snapshots");
		options.ServerSelectionTimeoutSeconds.ShouldBe(60);
		options.ConnectTimeoutSeconds.ShouldBe(45);
		options.UseSsl.ShouldBeTrue();
		options.MaxPoolSize.ShouldBe(200);
	}
}
