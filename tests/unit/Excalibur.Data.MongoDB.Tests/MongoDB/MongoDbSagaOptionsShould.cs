// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.MongoDB.Saga;

using Excalibur.Data.MongoDB;

namespace Excalibur.Data.Tests.MongoDB.Saga;

/// <summary>
/// Unit tests for <see cref="MongoDbSagaOptions"/> configuration and validation.
/// </summary>
[Trait("Category", "Unit")]
public sealed class MongoDbSagaOptionsShould : UnitTestBase
{
	#region Default Values Tests

	[Fact]
	public void HaveDefaultConnectionString()
	{
		// Arrange & Act
		var options = new MongoDbSagaOptions();

		// Assert
		options.ConnectionString.ShouldBe("mongodb://localhost:27017");
	}

	[Fact]
	public void HaveDefaultDatabaseName()
	{
		// Arrange & Act
		var options = new MongoDbSagaOptions();

		// Assert
		options.DatabaseName.ShouldBe("excalibur");
	}

	[Fact]
	public void HaveDefaultCollectionName()
	{
		// Arrange & Act
		var options = new MongoDbSagaOptions();

		// Assert
		options.CollectionName.ShouldBe("sagas");
	}

	[Fact]
	public void HaveDefaultServerSelectionTimeout()
	{
		// Arrange & Act
		var options = new MongoDbSagaOptions();

		// Assert
		options.ServerSelectionTimeoutSeconds.ShouldBe(30);
	}

	[Fact]
	public void HaveDefaultConnectTimeout()
	{
		// Arrange & Act
		var options = new MongoDbSagaOptions();

		// Assert
		options.ConnectTimeoutSeconds.ShouldBe(30);
	}

	[Fact]
	public void HaveDefaultUseSslFalse()
	{
		// Arrange & Act
		var options = new MongoDbSagaOptions();

		// Assert
		options.UseSsl.ShouldBeFalse();
	}

	[Fact]
	public void HaveDefaultMaxPoolSize()
	{
		// Arrange & Act
		var options = new MongoDbSagaOptions();

		// Assert
		options.MaxPoolSize.ShouldBe(100);
	}

	#endregion Default Values Tests

	#region Property Setters Tests

	[Fact]
	public void AllowCustomConnectionString()
	{
		// Arrange & Act
		var options = new MongoDbSagaOptions
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
		var options = new MongoDbSagaOptions
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
		var options = new MongoDbSagaOptions
		{
			CollectionName = "custom_sagas"
		};

		// Assert
		options.CollectionName.ShouldBe("custom_sagas");
	}

	[Fact]
	public void AllowCustomTimeouts()
	{
		// Arrange & Act
		var options = new MongoDbSagaOptions
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
		var options = new MongoDbSagaOptions
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
		var options = new MongoDbSagaOptions
		{
			MaxPoolSize = 200
		};

		// Assert
		options.MaxPoolSize.ShouldBe(200);
	}

	#endregion Property Setters Tests

	#region Validation Tests

	[Fact]
	public void Validate_WithValidOptions_DoesNotThrow()
	{
		// Arrange
		var options = new MongoDbSagaOptions();

		// Act & Assert - Should not throw
		options.Validate();
	}

	[Fact]
	public void Validate_WithNullConnectionString_ThrowsInvalidOperationException()
	{
		// Arrange
		var options = new MongoDbSagaOptions
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
		var options = new MongoDbSagaOptions
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
		var options = new MongoDbSagaOptions
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
		var options = new MongoDbSagaOptions
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
		var options = new MongoDbSagaOptions
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
		var options = new MongoDbSagaOptions
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
		var options = new MongoDbSagaOptions
		{
			CollectionName = string.Empty
		};

		// Act & Assert
		var exception = Should.Throw<InvalidOperationException>(() => options.Validate());
		exception.Message.ShouldContain("CollectionName");
	}

	#endregion Validation Tests
}
