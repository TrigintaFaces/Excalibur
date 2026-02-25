// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.MongoDB.Projections;

using Excalibur.Data.MongoDB;

namespace Excalibur.Data.Tests.MongoDB.Projections;

/// <summary>
/// Unit tests for <see cref="MongoDbProjectionStoreOptions"/> configuration and validation.
/// </summary>
[Trait("Category", "Unit")]
public sealed class MongoDbProjectionStoreOptionsShould : UnitTestBase
{
	#region Default Values Tests

	[Fact]
	public void HaveDefaultConnectionString()
	{
		// Arrange & Act
		var options = new MongoDbProjectionStoreOptions();

		// Assert
		options.ConnectionString.ShouldBe("mongodb://localhost:27017");
	}

	[Fact]
	public void HaveDefaultDatabaseName()
	{
		// Arrange & Act
		var options = new MongoDbProjectionStoreOptions();

		// Assert
		options.DatabaseName.ShouldBe("excalibur");
	}

	[Fact]
	public void HaveDefaultCollectionName()
	{
		// Arrange & Act
		var options = new MongoDbProjectionStoreOptions();

		// Assert
		options.CollectionName.ShouldBe("projections");
	}

	[Fact]
	public void HaveDefaultServerSelectionTimeout()
	{
		// Arrange & Act
		var options = new MongoDbProjectionStoreOptions();

		// Assert
		options.ServerSelectionTimeoutSeconds.ShouldBe(30);
	}

	[Fact]
	public void HaveDefaultConnectTimeout()
	{
		// Arrange & Act
		var options = new MongoDbProjectionStoreOptions();

		// Assert
		options.ConnectTimeoutSeconds.ShouldBe(30);
	}

	[Fact]
	public void HaveDefaultUseSslFalse()
	{
		// Arrange & Act
		var options = new MongoDbProjectionStoreOptions();

		// Assert
		options.UseSsl.ShouldBeFalse();
	}

	[Fact]
	public void HaveDefaultMaxPoolSize()
	{
		// Arrange & Act
		var options = new MongoDbProjectionStoreOptions();

		// Assert
		options.MaxPoolSize.ShouldBe(100);
	}

	[Fact]
	public void HaveDefaultCreateIndexesOnInitializeTrue()
	{
		// Arrange & Act
		var options = new MongoDbProjectionStoreOptions();

		// Assert
		options.CreateIndexesOnInitialize.ShouldBeTrue();
	}

	#endregion Default Values Tests

	#region Property Setters Tests

	[Fact]
	public void AllowCustomConnectionString()
	{
		// Arrange & Act
		var options = new MongoDbProjectionStoreOptions
		{
			ConnectionString = "mongodb://custom:27017"
		};

		// Assert
		options.ConnectionString.ShouldBe("mongodb://custom:27017");
	}

	[Fact]
	public void AllowCustomDatabaseName()
	{
		// Arrange & Act
		var options = new MongoDbProjectionStoreOptions
		{
			DatabaseName = "custom_db"
		};

		// Assert
		options.DatabaseName.ShouldBe("custom_db");
	}

	[Fact]
	public void AllowCustomCollectionName()
	{
		// Arrange & Act
		var options = new MongoDbProjectionStoreOptions
		{
			CollectionName = "custom_projections"
		};

		// Assert
		options.CollectionName.ShouldBe("custom_projections");
	}

	[Fact]
	public void AllowCustomTimeouts()
	{
		// Arrange & Act
		var options = new MongoDbProjectionStoreOptions
		{
			ServerSelectionTimeoutSeconds = 60,
			ConnectTimeoutSeconds = 45
		};

		// Assert
		options.ServerSelectionTimeoutSeconds.ShouldBe(60);
		options.ConnectTimeoutSeconds.ShouldBe(45);
	}

	[Fact]
	public void AllowEnablingSsl()
	{
		// Arrange & Act
		var options = new MongoDbProjectionStoreOptions
		{
			UseSsl = true
		};

		// Assert
		options.UseSsl.ShouldBeTrue();
	}

	[Fact]
	public void AllowCustomPoolSize()
	{
		// Arrange & Act
		var options = new MongoDbProjectionStoreOptions
		{
			MaxPoolSize = 200
		};

		// Assert
		options.MaxPoolSize.ShouldBe(200);
	}

	[Fact]
	public void AllowDisablingIndexCreation()
	{
		// Arrange & Act
		var options = new MongoDbProjectionStoreOptions
		{
			CreateIndexesOnInitialize = false
		};

		// Assert
		options.CreateIndexesOnInitialize.ShouldBeFalse();
	}

	#endregion Property Setters Tests

	#region Validation Tests

	[Fact]
	public void Validate_WithValidOptions_DoesNotThrow()
	{
		// Arrange
		var options = new MongoDbProjectionStoreOptions();

		// Act & Assert - Should not throw
		options.Validate();
	}

	[Fact]
	public void Validate_WithNullConnectionString_ThrowsInvalidOperationException()
	{
		// Arrange
		var options = new MongoDbProjectionStoreOptions
		{
			ConnectionString = null!
		};

		// Act & Assert
		var exception = Should.Throw<InvalidOperationException>(() => options.Validate());
		exception.Message.ShouldContain("ConnectionString");
	}

	[Fact]
	public void Validate_WithEmptyConnectionString_ThrowsInvalidOperationException()
	{
		// Arrange
		var options = new MongoDbProjectionStoreOptions
		{
			ConnectionString = string.Empty
		};

		// Act & Assert
		var exception = Should.Throw<InvalidOperationException>(() => options.Validate());
		exception.Message.ShouldContain("ConnectionString");
	}

	[Fact]
	public void Validate_WithWhitespaceConnectionString_ThrowsInvalidOperationException()
	{
		// Arrange
		var options = new MongoDbProjectionStoreOptions
		{
			ConnectionString = "   "
		};

		// Act & Assert
		var exception = Should.Throw<InvalidOperationException>(() => options.Validate());
		exception.Message.ShouldContain("ConnectionString");
	}

	[Fact]
	public void Validate_WithNullDatabaseName_ThrowsInvalidOperationException()
	{
		// Arrange
		var options = new MongoDbProjectionStoreOptions
		{
			DatabaseName = null!
		};

		// Act & Assert
		var exception = Should.Throw<InvalidOperationException>(() => options.Validate());
		exception.Message.ShouldContain("DatabaseName");
	}

	[Fact]
	public void Validate_WithEmptyDatabaseName_ThrowsInvalidOperationException()
	{
		// Arrange
		var options = new MongoDbProjectionStoreOptions
		{
			DatabaseName = string.Empty
		};

		// Act & Assert
		var exception = Should.Throw<InvalidOperationException>(() => options.Validate());
		exception.Message.ShouldContain("DatabaseName");
	}

	[Fact]
	public void Validate_WithNullCollectionName_ThrowsInvalidOperationException()
	{
		// Arrange
		var options = new MongoDbProjectionStoreOptions
		{
			CollectionName = null!
		};

		// Act & Assert
		var exception = Should.Throw<InvalidOperationException>(() => options.Validate());
		exception.Message.ShouldContain("CollectionName");
	}

	[Fact]
	public void Validate_WithEmptyCollectionName_ThrowsInvalidOperationException()
	{
		// Arrange
		var options = new MongoDbProjectionStoreOptions
		{
			CollectionName = string.Empty
		};

		// Act & Assert
		var exception = Should.Throw<InvalidOperationException>(() => options.Validate());
		exception.Message.ShouldContain("CollectionName");
	}

	#endregion Validation Tests
}
