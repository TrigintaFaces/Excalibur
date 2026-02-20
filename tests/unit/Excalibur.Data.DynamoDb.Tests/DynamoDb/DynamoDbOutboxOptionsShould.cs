// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.DynamoDb.Outbox;

namespace Excalibur.Data.Tests.DynamoDb;

/// <summary>
/// Unit tests for <see cref="DynamoDbOutboxOptions"/>.
/// </summary>
/// <remarks>
/// Sprint 514 (S514.4): DynamoDB unit tests.
/// Tests verify outbox options defaults and validation.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "DynamoDb")]
[Trait("Feature", "Outbox")]
public sealed class DynamoDbOutboxOptionsShould
{
	#region Default Value Tests

	[Fact]
	public void ServiceUrl_DefaultsToNull()
	{
		// Arrange & Act
		var options = new DynamoDbOutboxOptions();

		// Assert
		options.ServiceUrl.ShouldBeNull();
	}

	[Fact]
	public void Region_DefaultsToNull()
	{
		// Arrange & Act
		var options = new DynamoDbOutboxOptions();

		// Assert
		options.Region.ShouldBeNull();
	}

	[Fact]
	public void AccessKey_DefaultsToNull()
	{
		// Arrange & Act
		var options = new DynamoDbOutboxOptions();

		// Assert
		options.AccessKey.ShouldBeNull();
	}

	[Fact]
	public void SecretKey_DefaultsToNull()
	{
		// Arrange & Act
		var options = new DynamoDbOutboxOptions();

		// Assert
		options.SecretKey.ShouldBeNull();
	}

	[Fact]
	public void TableName_DefaultsToOutboxMessages()
	{
		// Arrange & Act
		var options = new DynamoDbOutboxOptions();

		// Assert
		options.TableName.ShouldBe("outbox_messages");
	}

	[Fact]
	public void GSI1IndexName_DefaultsToGSI1()
	{
		// Arrange & Act
		var options = new DynamoDbOutboxOptions();

		// Assert
		options.GSI1IndexName.ShouldBe("GSI1");
	}

	[Fact]
	public void GSI2IndexName_DefaultsToGSI2()
	{
		// Arrange & Act
		var options = new DynamoDbOutboxOptions();

		// Assert
		options.GSI2IndexName.ShouldBe("GSI2");
	}

	[Fact]
	public void MaxRetryAttempts_DefaultsToThree()
	{
		// Arrange & Act
		var options = new DynamoDbOutboxOptions();

		// Assert
		options.MaxRetryAttempts.ShouldBe(3);
	}

	[Fact]
	public void TimeoutInSeconds_DefaultsToThirty()
	{
		// Arrange & Act
		var options = new DynamoDbOutboxOptions();

		// Assert
		options.TimeoutInSeconds.ShouldBe(30);
	}

	[Fact]
	public void UseConsistentReads_DefaultsToTrue()
	{
		// Arrange & Act
		var options = new DynamoDbOutboxOptions();

		// Assert
		options.UseConsistentReads.ShouldBeTrue();
	}

	[Fact]
	public void SentMessageTtlSeconds_DefaultsToSevenDays()
	{
		// Arrange & Act
		var options = new DynamoDbOutboxOptions();

		// Assert
		options.SentMessageTtlSeconds.ShouldBe(604800); // 7 days in seconds
	}

	[Fact]
	public void TtlAttributeName_DefaultsToTtl()
	{
		// Arrange & Act
		var options = new DynamoDbOutboxOptions();

		// Assert
		options.TtlAttributeName.ShouldBe("ttl");
	}

	[Fact]
	public void CreateTableIfNotExists_DefaultsToTrue()
	{
		// Arrange & Act
		var options = new DynamoDbOutboxOptions();

		// Assert
		options.CreateTableIfNotExists.ShouldBeTrue();
	}

	#endregion

	#region Property Initialization Tests

	[Fact]
	public void AllProperties_CanBeInitialized()
	{
		// Act
		var options = new DynamoDbOutboxOptions
		{
			ServiceUrl = "http://localhost:8000",
			Region = "us-east-1",
			AccessKey = "test-access-key",
			SecretKey = "test-secret-key",
			TableName = "custom_outbox",
			GSI1IndexName = "CustomGSI1",
			GSI2IndexName = "CustomGSI2",
			MaxRetryAttempts = 5,
			TimeoutInSeconds = 60,
			UseConsistentReads = false,
			SentMessageTtlSeconds = 86400,
			TtlAttributeName = "expires_at",
			CreateTableIfNotExists = false
		};

		// Assert
		options.ServiceUrl.ShouldBe("http://localhost:8000");
		options.Region.ShouldBe("us-east-1");
		options.AccessKey.ShouldBe("test-access-key");
		options.SecretKey.ShouldBe("test-secret-key");
		options.TableName.ShouldBe("custom_outbox");
		options.GSI1IndexName.ShouldBe("CustomGSI1");
		options.GSI2IndexName.ShouldBe("CustomGSI2");
		options.MaxRetryAttempts.ShouldBe(5);
		options.TimeoutInSeconds.ShouldBe(60);
		options.UseConsistentReads.ShouldBeFalse();
		options.SentMessageTtlSeconds.ShouldBe(86400);
		options.TtlAttributeName.ShouldBe("expires_at");
		options.CreateTableIfNotExists.ShouldBeFalse();
	}

	#endregion

	#region Validation Tests

	[Fact]
	public void Validate_Succeeds_WithServiceUrl()
	{
		// Arrange
		var options = new DynamoDbOutboxOptions
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
		var options = new DynamoDbOutboxOptions
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
		var options = new DynamoDbOutboxOptions();

		// Act & Assert
		var exception = Should.Throw<InvalidOperationException>(() => options.Validate());
		exception.Message.ShouldContain("ServiceUrl");
		exception.Message.ShouldContain("Region");
	}

	[Fact]
	public void Validate_Throws_WhenTableNameIsEmpty()
	{
		// Arrange
		var options = new DynamoDbOutboxOptions
		{
			ServiceUrl = "http://localhost:8000",
			TableName = ""
		};

		// Act & Assert
		var exception = Should.Throw<InvalidOperationException>(() => options.Validate());
		exception.Message.ShouldContain("TableName");
	}

	[Fact]
	public void Validate_Throws_WhenGSI1IndexNameIsEmpty()
	{
		// Arrange
		var options = new DynamoDbOutboxOptions
		{
			ServiceUrl = "http://localhost:8000",
			GSI1IndexName = ""
		};

		// Act & Assert
		var exception = Should.Throw<InvalidOperationException>(() => options.Validate());
		exception.Message.ShouldContain("GSI1IndexName");
	}

	[Fact]
	public void Validate_Throws_WhenGSI2IndexNameIsEmpty()
	{
		// Arrange
		var options = new DynamoDbOutboxOptions
		{
			ServiceUrl = "http://localhost:8000",
			GSI2IndexName = ""
		};

		// Act & Assert
		var exception = Should.Throw<InvalidOperationException>(() => options.Validate());
		exception.Message.ShouldContain("GSI2IndexName");
	}

	#endregion

	#region GetRegionEndpoint Tests

	[Fact]
	public void GetRegionEndpoint_ReturnsEndpoint_WhenRegionIsSet()
	{
		// Arrange
		var options = new DynamoDbOutboxOptions
		{
			Region = "us-east-1"
		};

		// Act
		var endpoint = options.GetRegionEndpoint();

		// Assert
		endpoint.ShouldNotBeNull();
		endpoint.SystemName.ShouldBe("us-east-1");
	}

	[Fact]
	public void GetRegionEndpoint_ReturnsNull_WhenRegionIsNull()
	{
		// Arrange
		var options = new DynamoDbOutboxOptions();

		// Act
		var endpoint = options.GetRegionEndpoint();

		// Assert
		endpoint.ShouldBeNull();
	}

	[Fact]
	public void GetRegionEndpoint_ReturnsNull_WhenRegionIsWhitespace()
	{
		// Arrange
		var options = new DynamoDbOutboxOptions
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
		typeof(DynamoDbOutboxOptions).IsSealed.ShouldBeTrue();
	}

	[Fact]
	public void IsPublic()
	{
		// Assert
		typeof(DynamoDbOutboxOptions).IsPublic.ShouldBeTrue();
	}

	#endregion
}
