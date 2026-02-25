// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.MongoDB.MaterializedViews;

namespace Excalibur.EventSourcing.Tests.MaterializedViews.Providers;

/// <summary>
/// Unit tests for <see cref="MongoDbMaterializedViewStoreOptions"/>.
/// </summary>
/// <remarks>
/// Sprint 518: Materialized Views provider tests.
/// Tests verify options defaults, property behavior, and validation.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "MaterializedViews")]
[Trait("Feature", "MongoDB")]
public sealed class MongoDbMaterializedViewStoreOptionsShould
{
	#region Type Tests

	[Fact]
	public void BeSealed()
	{
		// Assert
		typeof(MongoDbMaterializedViewStoreOptions).IsSealed.ShouldBeTrue();
	}

	[Fact]
	public void BePublic()
	{
		// Assert
		typeof(MongoDbMaterializedViewStoreOptions).IsPublic.ShouldBeTrue();
	}

	#endregion

	#region Default Value Tests

	[Fact]
	public void HaveEmptyConnectionStringByDefault()
	{
		// Arrange & Act
		var options = new MongoDbMaterializedViewStoreOptions();

		// Assert
		options.ConnectionString.ShouldBe(string.Empty);
	}

	[Fact]
	public void HaveEmptyDatabaseNameByDefault()
	{
		// Arrange & Act
		var options = new MongoDbMaterializedViewStoreOptions();

		// Assert
		options.DatabaseName.ShouldBe(string.Empty);
	}

	[Fact]
	public void HaveCorrectDefaultViewsCollectionName()
	{
		// Arrange & Act
		var options = new MongoDbMaterializedViewStoreOptions();

		// Assert
		options.ViewsCollectionName.ShouldBe("materialized_views");
	}

	[Fact]
	public void HaveCorrectDefaultPositionsCollectionName()
	{
		// Arrange & Act
		var options = new MongoDbMaterializedViewStoreOptions();

		// Assert
		options.PositionsCollectionName.ShouldBe("materialized_view_positions");
	}

	[Fact]
	public void HaveDefaultServerSelectionTimeoutOf30Seconds()
	{
		// Arrange & Act
		var options = new MongoDbMaterializedViewStoreOptions();

		// Assert
		options.ServerSelectionTimeoutSeconds.ShouldBe(30);
	}

	[Fact]
	public void HaveDefaultConnectTimeoutOf10Seconds()
	{
		// Arrange & Act
		var options = new MongoDbMaterializedViewStoreOptions();

		// Assert
		options.ConnectTimeoutSeconds.ShouldBe(10);
	}

	[Fact]
	public void HaveDefaultMaxPoolSizeOf100()
	{
		// Arrange & Act
		var options = new MongoDbMaterializedViewStoreOptions();

		// Assert
		options.MaxPoolSize.ShouldBe(100);
	}

	[Fact]
	public void HaveUseSslDisabledByDefault()
	{
		// Arrange & Act
		var options = new MongoDbMaterializedViewStoreOptions();

		// Assert
		options.UseSsl.ShouldBeFalse();
	}

	#endregion

	#region Property Setter Tests

	[Fact]
	public void AllowSettingConnectionString()
	{
		// Arrange
		var options = new MongoDbMaterializedViewStoreOptions();
		var connectionString = "mongodb://localhost:27017";

		// Act
		options.ConnectionString = connectionString;

		// Assert
		options.ConnectionString.ShouldBe(connectionString);
	}

	[Fact]
	public void AllowSettingDatabaseName()
	{
		// Arrange
		var options = new MongoDbMaterializedViewStoreOptions();
		var databaseName = "my_database";

		// Act
		options.DatabaseName = databaseName;

		// Assert
		options.DatabaseName.ShouldBe(databaseName);
	}

	[Fact]
	public void AllowSettingViewsCollectionName()
	{
		// Arrange
		var options = new MongoDbMaterializedViewStoreOptions();
		var collectionName = "custom_views";

		// Act
		options.ViewsCollectionName = collectionName;

		// Assert
		options.ViewsCollectionName.ShouldBe(collectionName);
	}

	[Fact]
	public void AllowSettingPositionsCollectionName()
	{
		// Arrange
		var options = new MongoDbMaterializedViewStoreOptions();
		var collectionName = "custom_positions";

		// Act
		options.PositionsCollectionName = collectionName;

		// Assert
		options.PositionsCollectionName.ShouldBe(collectionName);
	}

	[Fact]
	public void AllowSettingServerSelectionTimeout()
	{
		// Arrange
		var options = new MongoDbMaterializedViewStoreOptions();

		// Act
		options.ServerSelectionTimeoutSeconds = 60;

		// Assert
		options.ServerSelectionTimeoutSeconds.ShouldBe(60);
	}

	[Fact]
	public void AllowSettingConnectTimeout()
	{
		// Arrange
		var options = new MongoDbMaterializedViewStoreOptions();

		// Act
		options.ConnectTimeoutSeconds = 30;

		// Assert
		options.ConnectTimeoutSeconds.ShouldBe(30);
	}

	[Fact]
	public void AllowSettingMaxPoolSize()
	{
		// Arrange
		var options = new MongoDbMaterializedViewStoreOptions();

		// Act
		options.MaxPoolSize = 200;

		// Assert
		options.MaxPoolSize.ShouldBe(200);
	}

	[Fact]
	public void AllowEnablingSsl()
	{
		// Arrange
		var options = new MongoDbMaterializedViewStoreOptions();

		// Act
		options.UseSsl = true;

		// Assert
		options.UseSsl.ShouldBeTrue();
	}

	#endregion

	#region Validate Tests

	[Fact]
	public void Validate_ThrowOnMissingConnectionString()
	{
		// Arrange
		var options = new MongoDbMaterializedViewStoreOptions
		{
			DatabaseName = "test"
		};

		// Act & Assert
		Should.Throw<InvalidOperationException>(() => options.Validate())
			.Message.ShouldContain("ConnectionString");
	}

	[Fact]
	public void Validate_ThrowOnEmptyConnectionString()
	{
		// Arrange
		var options = new MongoDbMaterializedViewStoreOptions
		{
			ConnectionString = "",
			DatabaseName = "test"
		};

		// Act & Assert
		Should.Throw<InvalidOperationException>(() => options.Validate())
			.Message.ShouldContain("ConnectionString");
	}

	[Fact]
	public void Validate_ThrowOnWhitespaceConnectionString()
	{
		// Arrange
		var options = new MongoDbMaterializedViewStoreOptions
		{
			ConnectionString = "   ",
			DatabaseName = "test"
		};

		// Act & Assert
		Should.Throw<InvalidOperationException>(() => options.Validate())
			.Message.ShouldContain("ConnectionString");
	}

	[Fact]
	public void Validate_ThrowOnMissingDatabaseName()
	{
		// Arrange
		var options = new MongoDbMaterializedViewStoreOptions
		{
			ConnectionString = "mongodb://localhost:27017"
		};

		// Act & Assert
		Should.Throw<InvalidOperationException>(() => options.Validate())
			.Message.ShouldContain("DatabaseName");
	}

	[Fact]
	public void Validate_ThrowOnEmptyDatabaseName()
	{
		// Arrange
		var options = new MongoDbMaterializedViewStoreOptions
		{
			ConnectionString = "mongodb://localhost:27017",
			DatabaseName = ""
		};

		// Act & Assert
		Should.Throw<InvalidOperationException>(() => options.Validate())
			.Message.ShouldContain("DatabaseName");
	}

	[Fact]
	public void Validate_ThrowOnWhitespaceDatabaseName()
	{
		// Arrange
		var options = new MongoDbMaterializedViewStoreOptions
		{
			ConnectionString = "mongodb://localhost:27017",
			DatabaseName = "   "
		};

		// Act & Assert
		Should.Throw<InvalidOperationException>(() => options.Validate())
			.Message.ShouldContain("DatabaseName");
	}

	[Fact]
	public void Validate_SucceedWithValidOptions()
	{
		// Arrange
		var options = new MongoDbMaterializedViewStoreOptions
		{
			ConnectionString = "mongodb://localhost:27017",
			DatabaseName = "test"
		};

		// Act & Assert - should not throw
		options.Validate();
	}

	#endregion

	#region Constant Tests

	[Fact]
	public void HaveDefaultViewsCollectionNameConstant()
	{
		// Assert
		MongoDbMaterializedViewStoreOptions.DefaultViewsCollectionName.ShouldBe("materialized_views");
	}

	[Fact]
	public void HaveDefaultPositionsCollectionNameConstant()
	{
		// Assert
		MongoDbMaterializedViewStoreOptions.DefaultPositionsCollectionName.ShouldBe("materialized_view_positions");
	}

	#endregion
}
