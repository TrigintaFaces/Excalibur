// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.DynamoDb.Authorization;

namespace Excalibur.Data.Tests.DynamoDb;

/// <summary>
/// Unit tests for <see cref="DynamoDbAuthorizationOptions"/>.
/// </summary>
/// <remarks>
/// Sprint 514 (S514.4): DynamoDB unit tests.
/// Tests verify authorization options defaults and validation.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "DynamoDb")]
[Trait("Feature", "Authorization")]
public sealed class DynamoDbAuthorizationOptionsShould
{
	#region Default Value Tests

	[Fact]
	public void ServiceUrl_DefaultsToNull()
	{
		// Arrange & Act
		var options = new DynamoDbAuthorizationOptions();

		// Assert
		options.ServiceUrl.ShouldBeNull();
	}

	[Fact]
	public void Region_DefaultsToNull()
	{
		// Arrange & Act
		var options = new DynamoDbAuthorizationOptions();

		// Assert
		options.Region.ShouldBeNull();
	}

	[Fact]
	public void AccessKey_DefaultsToNull()
	{
		// Arrange & Act
		var options = new DynamoDbAuthorizationOptions();

		// Assert
		options.AccessKey.ShouldBeNull();
	}

	[Fact]
	public void SecretKey_DefaultsToNull()
	{
		// Arrange & Act
		var options = new DynamoDbAuthorizationOptions();

		// Assert
		options.SecretKey.ShouldBeNull();
	}

	[Fact]
	public void GrantsTableName_DefaultsToAuthorizationGrants()
	{
		// Arrange & Act
		var options = new DynamoDbAuthorizationOptions();

		// Assert
		options.GrantsTableName.ShouldBe("authorization_grants");
	}

	[Fact]
	public void ActivityGroupsTableName_DefaultsToAuthorizationActivityGroups()
	{
		// Arrange & Act
		var options = new DynamoDbAuthorizationOptions();

		// Assert
		options.ActivityGroupsTableName.ShouldBe("authorization_activity_groups");
	}

	[Fact]
	public void UserIndexName_DefaultsToUserIndex()
	{
		// Arrange & Act
		var options = new DynamoDbAuthorizationOptions();

		// Assert
		options.UserIndexName.ShouldBe("UserIndex");
	}

	[Fact]
	public void MaxRetryAttempts_DefaultsToThree()
	{
		// Arrange & Act
		var options = new DynamoDbAuthorizationOptions();

		// Assert
		options.MaxRetryAttempts.ShouldBe(3);
	}

	[Fact]
	public void TimeoutInSeconds_DefaultsToThirty()
	{
		// Arrange & Act
		var options = new DynamoDbAuthorizationOptions();

		// Assert
		options.TimeoutInSeconds.ShouldBe(30);
	}

	[Fact]
	public void UseConsistentReads_DefaultsToTrue()
	{
		// Arrange & Act
		var options = new DynamoDbAuthorizationOptions();

		// Assert
		options.UseConsistentReads.ShouldBeTrue();
	}

	[Fact]
	public void CreateTableIfNotExists_DefaultsToFalse()
	{
		// Arrange & Act
		var options = new DynamoDbAuthorizationOptions();

		// Assert
		options.CreateTableIfNotExists.ShouldBeFalse();
	}

	#endregion

	#region Property Initialization Tests

	[Fact]
	public void AllProperties_CanBeInitialized()
	{
		// Act
		var options = new DynamoDbAuthorizationOptions
		{
			ServiceUrl = "http://localhost:8000",
			Region = "us-east-1",
			AccessKey = "auth-access-key",
			SecretKey = "auth-secret-key",
			GrantsTableName = "custom_grants",
			ActivityGroupsTableName = "custom_activity_groups",
			UserIndexName = "CustomUserIndex",
			MaxRetryAttempts = 5,
			TimeoutInSeconds = 60,
			UseConsistentReads = false,
			CreateTableIfNotExists = true
		};

		// Assert
		options.ServiceUrl.ShouldBe("http://localhost:8000");
		options.Region.ShouldBe("us-east-1");
		options.AccessKey.ShouldBe("auth-access-key");
		options.SecretKey.ShouldBe("auth-secret-key");
		options.GrantsTableName.ShouldBe("custom_grants");
		options.ActivityGroupsTableName.ShouldBe("custom_activity_groups");
		options.UserIndexName.ShouldBe("CustomUserIndex");
		options.MaxRetryAttempts.ShouldBe(5);
		options.TimeoutInSeconds.ShouldBe(60);
		options.UseConsistentReads.ShouldBeFalse();
		options.CreateTableIfNotExists.ShouldBeTrue();
	}

	#endregion

	#region Validation Tests

	[Fact]
	public void Validate_Succeeds_WithServiceUrl()
	{
		// Arrange
		var options = new DynamoDbAuthorizationOptions
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
		var options = new DynamoDbAuthorizationOptions
		{
			Region = "us-east-1"
		};

		// Act & Assert - Should not throw
		options.Validate();
	}

	[Fact]
	public void Validate_Throws_WhenNeitherServiceUrlNorRegionProvided()
	{
		// Arrange
		var options = new DynamoDbAuthorizationOptions();

		// Act & Assert
		var exception = Should.Throw<InvalidOperationException>(() => options.Validate());
		exception.Message.ShouldContain("ServiceUrl");
		exception.Message.ShouldContain("Region");
	}

	[Fact]
	public void Validate_Throws_WhenGrantsTableNameIsEmpty()
	{
		// Arrange
		var options = new DynamoDbAuthorizationOptions
		{
			ServiceUrl = "http://localhost:8000",
			GrantsTableName = ""
		};

		// Act & Assert
		var exception = Should.Throw<InvalidOperationException>(() => options.Validate());
		exception.Message.ShouldContain("GrantsTableName");
	}

	[Fact]
	public void Validate_Throws_WhenActivityGroupsTableNameIsEmpty()
	{
		// Arrange
		var options = new DynamoDbAuthorizationOptions
		{
			ServiceUrl = "http://localhost:8000",
			ActivityGroupsTableName = ""
		};

		// Act & Assert
		var exception = Should.Throw<InvalidOperationException>(() => options.Validate());
		exception.Message.ShouldContain("ActivityGroupsTableName");
	}

	[Fact]
	public void Validate_Throws_WhenUserIndexNameIsEmpty()
	{
		// Arrange
		var options = new DynamoDbAuthorizationOptions
		{
			ServiceUrl = "http://localhost:8000",
			UserIndexName = ""
		};

		// Act & Assert
		var exception = Should.Throw<InvalidOperationException>(() => options.Validate());
		exception.Message.ShouldContain("UserIndexName");
	}

	#endregion

	#region GetRegionEndpoint Tests

	[Fact]
	public void GetRegionEndpoint_ReturnsEndpoint_WhenRegionIsSet()
	{
		// Arrange
		var options = new DynamoDbAuthorizationOptions
		{
			Region = "eu-west-1"
		};

		// Act
		var endpoint = options.GetRegionEndpoint();

		// Assert
		endpoint.ShouldNotBeNull();
		endpoint.SystemName.ShouldBe("eu-west-1");
	}

	[Fact]
	public void GetRegionEndpoint_ReturnsNull_WhenRegionIsNull()
	{
		// Arrange
		var options = new DynamoDbAuthorizationOptions();

		// Act
		var endpoint = options.GetRegionEndpoint();

		// Assert
		endpoint.ShouldBeNull();
	}

	[Fact]
	public void GetRegionEndpoint_ReturnsNull_WhenRegionIsWhitespace()
	{
		// Arrange
		var options = new DynamoDbAuthorizationOptions
		{
			Region = "   "
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
		typeof(DynamoDbAuthorizationOptions).IsSealed.ShouldBeTrue();
	}

	[Fact]
	public void IsPublic()
	{
		// Assert
		typeof(DynamoDbAuthorizationOptions).IsPublic.ShouldBeTrue();
	}

	#endregion
}
