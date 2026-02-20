// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.DynamoDb.Inbox;

namespace Excalibur.Data.Tests.DynamoDb;

/// <summary>
/// Unit tests for <see cref="DynamoDbInboxOptions"/>.
/// </summary>
/// <remarks>
/// Sprint 514 (S514.4): DynamoDB unit tests.
/// Tests verify inbox options defaults and validation.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "DynamoDb")]
[Trait("Feature", "Inbox")]
public sealed class DynamoDbInboxOptionsShould
{
	#region Default Value Tests

	[Fact]
	public void ServiceUrl_DefaultsToNull()
	{
		// Arrange & Act
		var options = new DynamoDbInboxOptions();

		// Assert
		options.ServiceUrl.ShouldBeNull();
	}

	[Fact]
	public void Region_DefaultsToNull()
	{
		// Arrange & Act
		var options = new DynamoDbInboxOptions();

		// Assert
		options.Region.ShouldBeNull();
	}

	[Fact]
	public void AccessKey_DefaultsToNull()
	{
		// Arrange & Act
		var options = new DynamoDbInboxOptions();

		// Assert
		options.AccessKey.ShouldBeNull();
	}

	[Fact]
	public void SecretKey_DefaultsToNull()
	{
		// Arrange & Act
		var options = new DynamoDbInboxOptions();

		// Assert
		options.SecretKey.ShouldBeNull();
	}

	[Fact]
	public void TableName_DefaultsToInboxMessages()
	{
		// Arrange & Act
		var options = new DynamoDbInboxOptions();

		// Assert
		options.TableName.ShouldBe("inbox_messages");
	}

	[Fact]
	public void PartitionKeyAttribute_DefaultsToHandlerType()
	{
		// Arrange & Act
		var options = new DynamoDbInboxOptions();

		// Assert
		options.PartitionKeyAttribute.ShouldBe("handler_type");
	}

	[Fact]
	public void SortKeyAttribute_DefaultsToMessageId()
	{
		// Arrange & Act
		var options = new DynamoDbInboxOptions();

		// Assert
		options.SortKeyAttribute.ShouldBe("message_id");
	}

	[Fact]
	public void MaxRetryAttempts_DefaultsToThree()
	{
		// Arrange & Act
		var options = new DynamoDbInboxOptions();

		// Assert
		options.MaxRetryAttempts.ShouldBe(3);
	}

	[Fact]
	public void TimeoutInSeconds_DefaultsToThirty()
	{
		// Arrange & Act
		var options = new DynamoDbInboxOptions();

		// Assert
		options.TimeoutInSeconds.ShouldBe(30);
	}

	[Fact]
	public void UseConsistentReads_DefaultsToTrue()
	{
		// Arrange & Act
		var options = new DynamoDbInboxOptions();

		// Assert
		options.UseConsistentReads.ShouldBeTrue();
	}

	[Fact]
	public void DefaultTtlSeconds_DefaultsToSevenDays()
	{
		// Arrange & Act
		var options = new DynamoDbInboxOptions();

		// Assert
		options.DefaultTtlSeconds.ShouldBe(604800); // 7 days in seconds
	}

	[Fact]
	public void TtlAttributeName_DefaultsToTtl()
	{
		// Arrange & Act
		var options = new DynamoDbInboxOptions();

		// Assert
		options.TtlAttributeName.ShouldBe("ttl");
	}

	#endregion

	#region Property Initialization Tests

	[Fact]
	public void AllProperties_CanBeInitialized()
	{
		// Act
		var options = new DynamoDbInboxOptions
		{
			ServiceUrl = "http://localhost:8000",
			Region = "us-east-1",
			AccessKey = "inbox-access-key",
			SecretKey = "inbox-secret-key",
			TableName = "custom_inbox",
			PartitionKeyAttribute = "custom_handler",
			SortKeyAttribute = "custom_message_id",
			MaxRetryAttempts = 5,
			TimeoutInSeconds = 60,
			UseConsistentReads = false,
			DefaultTtlSeconds = 86400,
			TtlAttributeName = "expires_at"
		};

		// Assert
		options.ServiceUrl.ShouldBe("http://localhost:8000");
		options.Region.ShouldBe("us-east-1");
		options.AccessKey.ShouldBe("inbox-access-key");
		options.SecretKey.ShouldBe("inbox-secret-key");
		options.TableName.ShouldBe("custom_inbox");
		options.PartitionKeyAttribute.ShouldBe("custom_handler");
		options.SortKeyAttribute.ShouldBe("custom_message_id");
		options.MaxRetryAttempts.ShouldBe(5);
		options.TimeoutInSeconds.ShouldBe(60);
		options.UseConsistentReads.ShouldBeFalse();
		options.DefaultTtlSeconds.ShouldBe(86400);
		options.TtlAttributeName.ShouldBe("expires_at");
	}

	#endregion

	#region Validation Tests

	[Fact]
	public void Validate_Succeeds_WithServiceUrl()
	{
		// Arrange
		var options = new DynamoDbInboxOptions
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
		var options = new DynamoDbInboxOptions
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
		var options = new DynamoDbInboxOptions();

		// Act & Assert
		var exception = Should.Throw<InvalidOperationException>(() => options.Validate());
		exception.Message.ShouldContain("ServiceUrl");
		exception.Message.ShouldContain("Region");
	}

	[Fact]
	public void Validate_Throws_WhenTableNameIsEmpty()
	{
		// Arrange
		var options = new DynamoDbInboxOptions
		{
			ServiceUrl = "http://localhost:8000",
			TableName = ""
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
		var options = new DynamoDbInboxOptions
		{
			Region = "eu-central-1"
		};

		// Act
		var endpoint = options.GetRegionEndpoint();

		// Assert
		endpoint.ShouldNotBeNull();
		endpoint.SystemName.ShouldBe("eu-central-1");
	}

	[Fact]
	public void GetRegionEndpoint_ReturnsNull_WhenRegionIsNull()
	{
		// Arrange
		var options = new DynamoDbInboxOptions();

		// Act
		var endpoint = options.GetRegionEndpoint();

		// Assert
		endpoint.ShouldBeNull();
	}

	[Fact]
	public void GetRegionEndpoint_ReturnsNull_WhenRegionIsWhitespace()
	{
		// Arrange
		var options = new DynamoDbInboxOptions
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
		typeof(DynamoDbInboxOptions).IsSealed.ShouldBeTrue();
	}

	[Fact]
	public void IsPublic()
	{
		// Assert
		typeof(DynamoDbInboxOptions).IsPublic.ShouldBeTrue();
	}

	#endregion
}
