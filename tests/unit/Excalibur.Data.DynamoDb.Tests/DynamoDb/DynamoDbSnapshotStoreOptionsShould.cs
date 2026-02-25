// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.DynamoDb.Snapshots;

namespace Excalibur.Data.Tests.DynamoDb;

/// <summary>
/// Unit tests for <see cref="DynamoDbSnapshotStoreOptions"/>.
/// </summary>
/// <remarks>
/// Sprint 514 (S514.4): DynamoDB unit tests.
/// Tests verify snapshot store options defaults and validation.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "DynamoDb")]
[Trait("Feature", "Snapshots")]
public sealed class DynamoDbSnapshotStoreOptionsShould
{
	#region Default Value Tests

	[Fact]
	public void ServiceUrl_DefaultsToNull()
	{
		// Arrange & Act
		var options = new DynamoDbSnapshotStoreOptions();

		// Assert
		options.ServiceUrl.ShouldBeNull();
	}

	[Fact]
	public void Region_DefaultsToNull()
	{
		// Arrange & Act
		var options = new DynamoDbSnapshotStoreOptions();

		// Assert
		options.Region.ShouldBeNull();
	}

	[Fact]
	public void AccessKey_DefaultsToNull()
	{
		// Arrange & Act
		var options = new DynamoDbSnapshotStoreOptions();

		// Assert
		options.AccessKey.ShouldBeNull();
	}

	[Fact]
	public void SecretKey_DefaultsToNull()
	{
		// Arrange & Act
		var options = new DynamoDbSnapshotStoreOptions();

		// Assert
		options.SecretKey.ShouldBeNull();
	}

	[Fact]
	public void TableName_DefaultsToSnapshots()
	{
		// Arrange & Act
		var options = new DynamoDbSnapshotStoreOptions();

		// Assert
		options.TableName.ShouldBe("snapshots");
	}

	[Fact]
	public void MaxRetryAttempts_DefaultsToThree()
	{
		// Arrange & Act
		var options = new DynamoDbSnapshotStoreOptions();

		// Assert
		options.MaxRetryAttempts.ShouldBe(3);
	}

	[Fact]
	public void TimeoutInSeconds_DefaultsToThirty()
	{
		// Arrange & Act
		var options = new DynamoDbSnapshotStoreOptions();

		// Assert
		options.TimeoutInSeconds.ShouldBe(30);
	}

	[Fact]
	public void UseConsistentReads_DefaultsToTrue()
	{
		// Arrange & Act
		var options = new DynamoDbSnapshotStoreOptions();

		// Assert
		options.UseConsistentReads.ShouldBeTrue();
	}

	[Fact]
	public void DefaultTtlSeconds_DefaultsToZero()
	{
		// Arrange & Act
		var options = new DynamoDbSnapshotStoreOptions();

		// Assert
		options.DefaultTtlSeconds.ShouldBe(0);
	}

	[Fact]
	public void TtlAttributeName_DefaultsToTtl()
	{
		// Arrange & Act
		var options = new DynamoDbSnapshotStoreOptions();

		// Assert
		options.TtlAttributeName.ShouldBe("ttl");
	}

	[Fact]
	public void CreateTableIfNotExists_DefaultsToTrue()
	{
		// Arrange & Act
		var options = new DynamoDbSnapshotStoreOptions();

		// Assert
		options.CreateTableIfNotExists.ShouldBeTrue();
	}

	#endregion

	#region Property Initialization Tests

	[Fact]
	public void AllProperties_CanBeInitialized()
	{
		// Act
		var options = new DynamoDbSnapshotStoreOptions
		{
			ServiceUrl = "http://localhost:8000",
			Region = "us-west-2",
			AccessKey = "snapshot-access-key",
			SecretKey = "snapshot-secret-key",
			TableName = "custom_snapshots",
			MaxRetryAttempts = 5,
			TimeoutInSeconds = 60,
			UseConsistentReads = false,
			DefaultTtlSeconds = 86400,
			TtlAttributeName = "expires_at",
			CreateTableIfNotExists = false
		};

		// Assert
		options.ServiceUrl.ShouldBe("http://localhost:8000");
		options.Region.ShouldBe("us-west-2");
		options.AccessKey.ShouldBe("snapshot-access-key");
		options.SecretKey.ShouldBe("snapshot-secret-key");
		options.TableName.ShouldBe("custom_snapshots");
		options.MaxRetryAttempts.ShouldBe(5);
		options.TimeoutInSeconds.ShouldBe(60);
		options.UseConsistentReads.ShouldBeFalse();
		options.DefaultTtlSeconds.ShouldBe(86400);
		options.TtlAttributeName.ShouldBe("expires_at");
		options.CreateTableIfNotExists.ShouldBeFalse();
	}

	#endregion

	#region Validation Tests

	[Fact]
	public void Validate_Succeeds_WithServiceUrl()
	{
		// Arrange
		var options = new DynamoDbSnapshotStoreOptions
		{
			ServiceUrl = "http://localhost:8000"
		};

		// Act & Assert - Should not throw
		options.Validate();
	}

	[Fact]
	public void Validate_Succeeds_WithRegion()
	{
		// Arrange
		var options = new DynamoDbSnapshotStoreOptions
		{
			Region = "ap-northeast-1"
		};

		// Act & Assert - Should not throw
		options.Validate();
	}

	[Fact]
	public void Validate_Throws_WhenNeitherServiceUrlNorRegionProvided()
	{
		// Arrange
		var options = new DynamoDbSnapshotStoreOptions();

		// Act & Assert
		var exception = Should.Throw<InvalidOperationException>(() => options.Validate());
		exception.Message.ShouldContain("ServiceUrl");
		exception.Message.ShouldContain("Region");
	}

	[Fact]
	public void Validate_Throws_WhenTableNameIsEmpty()
	{
		// Arrange
		var options = new DynamoDbSnapshotStoreOptions
		{
			ServiceUrl = "http://localhost:8000",
			TableName = ""
		};

		// Act & Assert
		var exception = Should.Throw<InvalidOperationException>(() => options.Validate());
		exception.Message.ShouldContain("TableName");
	}

	[Fact]
	public void Validate_Throws_WhenTableNameIsWhitespace()
	{
		// Arrange
		var options = new DynamoDbSnapshotStoreOptions
		{
			ServiceUrl = "http://localhost:8000",
			TableName = "   "
		};

		// Act & Assert
		var exception = Should.Throw<InvalidOperationException>(() => options.Validate());
		exception.Message.ShouldContain("TableName");
	}

	#endregion

	#region GetRegionEndpoint Tests

	[Fact]
	public void GetRegionEndpoint_ReturnsEndpoint_WhenRegionIsSet()
	{
		// Arrange
		var options = new DynamoDbSnapshotStoreOptions
		{
			Region = "sa-east-1"
		};

		// Act
		var endpoint = options.GetRegionEndpoint();

		// Assert
		endpoint.ShouldNotBeNull();
		endpoint.SystemName.ShouldBe("sa-east-1");
	}

	[Fact]
	public void GetRegionEndpoint_ReturnsNull_WhenRegionIsNull()
	{
		// Arrange
		var options = new DynamoDbSnapshotStoreOptions();

		// Act
		var endpoint = options.GetRegionEndpoint();

		// Assert
		endpoint.ShouldBeNull();
	}

	[Fact]
	public void GetRegionEndpoint_ReturnsNull_WhenRegionIsEmpty()
	{
		// Arrange
		var options = new DynamoDbSnapshotStoreOptions
		{
			Region = ""
		};

		// Act
		var endpoint = options.GetRegionEndpoint();

		// Assert
		endpoint.ShouldBeNull();
	}

	#endregion

	#region Type Tests

	[Fact]
	public void IsSealed()
	{
		// Assert
		typeof(DynamoDbSnapshotStoreOptions).IsSealed.ShouldBeTrue();
	}

	[Fact]
	public void IsPublic()
	{
		// Assert
		typeof(DynamoDbSnapshotStoreOptions).IsPublic.ShouldBeTrue();
	}

	#endregion
}
