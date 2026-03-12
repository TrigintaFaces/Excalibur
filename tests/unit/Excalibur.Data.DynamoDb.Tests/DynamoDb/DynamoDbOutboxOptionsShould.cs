// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Outbox.DynamoDb;

namespace Excalibur.Data.Tests.DynamoDb;

/// <summary>
/// Unit tests for <see cref="DynamoDbOutboxOptions"/>.
/// </summary>
/// <remarks>
/// Sprint 514 (S514.4): DynamoDB unit tests.
/// Sprint 633: Updated for ISP-split -- Connection sub-options, renamed properties.
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
		options.Connection.ServiceUrl.ShouldBeNull();
	}

	[Fact]
	public void Region_DefaultsToNull()
	{
		// Arrange & Act
		var options = new DynamoDbOutboxOptions();

		// Assert
		options.Connection.Region.ShouldBeNull();
	}

	[Fact]
	public void AccessKey_DefaultsToNull()
	{
		// Arrange & Act
		var options = new DynamoDbOutboxOptions();

		// Assert
		options.Connection.AccessKey.ShouldBeNull();
	}

	[Fact]
	public void SecretKey_DefaultsToNull()
	{
		// Arrange & Act
		var options = new DynamoDbOutboxOptions();

		// Assert
		options.Connection.SecretKey.ShouldBeNull();
	}

	[Fact]
	public void TableName_DefaultsToOutbox()
	{
		// Arrange & Act
		var options = new DynamoDbOutboxOptions();

		// Assert
		options.TableName.ShouldBe("outbox");
	}

	[Fact]
	public void PartitionKeyAttribute_DefaultsToPk()
	{
		// Arrange & Act
		var options = new DynamoDbOutboxOptions();

		// Assert
		options.PartitionKeyAttribute.ShouldBe("pk");
	}

	[Fact]
	public void SortKeyAttribute_DefaultsToSk()
	{
		// Arrange & Act
		var options = new DynamoDbOutboxOptions();

		// Assert
		options.SortKeyAttribute.ShouldBe("sk");
	}

	[Fact]
	public void TtlAttribute_DefaultsToTtl()
	{
		// Arrange & Act
		var options = new DynamoDbOutboxOptions();

		// Assert
		options.TtlAttribute.ShouldBe("ttl");
	}

	[Fact]
	public void DefaultTimeToLiveSeconds_DefaultsToSevenDays()
	{
		// Arrange & Act
		var options = new DynamoDbOutboxOptions();

		// Assert
		options.DefaultTimeToLiveSeconds.ShouldBe(604800); // 7 days in seconds
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
	public void CreateTableIfNotExists_DefaultsToTrue()
	{
		// Arrange & Act
		var options = new DynamoDbOutboxOptions();

		// Assert
		options.CreateTableIfNotExists.ShouldBeTrue();
	}

	[Fact]
	public void EnableStreams_DefaultsToTrue()
	{
		// Arrange & Act
		var options = new DynamoDbOutboxOptions();

		// Assert
		options.EnableStreams.ShouldBeTrue();
	}

	[Fact]
	public void Connection_DefaultsToNewInstance()
	{
		// Arrange & Act
		var options = new DynamoDbOutboxOptions();

		// Assert
		options.Connection.ShouldNotBeNull();
	}

	#endregion

	#region Property Initialization Tests

	[Fact]
	public void AllProperties_CanBeInitialized()
	{
		// Act
		var options = new DynamoDbOutboxOptions
		{
			Connection =
			{
				ServiceUrl = "http://localhost:8000",
				Region = "us-east-1",
				AccessKey = "test-access-key",
				SecretKey = "test-secret-key"
			},
			TableName = "custom_outbox",
			PartitionKeyAttribute = "custom_pk",
			SortKeyAttribute = "custom_sk",
			TtlAttribute = "expires_at",
			DefaultTimeToLiveSeconds = 86400,
			MaxRetryAttempts = 5,
			CreateTableIfNotExists = false,
			EnableStreams = false
		};

		// Assert
		options.Connection.ServiceUrl.ShouldBe("http://localhost:8000");
		options.Connection.Region.ShouldBe("us-east-1");
		options.Connection.AccessKey.ShouldBe("test-access-key");
		options.Connection.SecretKey.ShouldBe("test-secret-key");
		options.TableName.ShouldBe("custom_outbox");
		options.PartitionKeyAttribute.ShouldBe("custom_pk");
		options.SortKeyAttribute.ShouldBe("custom_sk");
		options.TtlAttribute.ShouldBe("expires_at");
		options.DefaultTimeToLiveSeconds.ShouldBe(86400);
		options.MaxRetryAttempts.ShouldBe(5);
		options.CreateTableIfNotExists.ShouldBeFalse();
		options.EnableStreams.ShouldBeFalse();
	}

	#endregion

	#region Validation Tests

	[Fact]
	public void Validate_Succeeds_WithServiceUrl()
	{
		// Arrange
		var options = new DynamoDbOutboxOptions();
		options.Connection.ServiceUrl = "http://localhost:8000";

		// Act & Assert - Should not throw
		options.Validate();
	}

	[Fact]
	public void Validate_Succeeds_WithRegion()
	{
		// Arrange
		var options = new DynamoDbOutboxOptions();
		options.Connection.Region = "us-east-1";

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

	#endregion

	#region GetRegionEndpoint Tests

	[Fact]
	public void GetRegionEndpoint_ReturnsEndpoint_WhenRegionIsSet()
	{
		// Arrange
		var options = new DynamoDbOutboxOptions();
		options.Connection.Region = "us-east-1";

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
		var options = new DynamoDbOutboxOptions();
		options.Connection.Region = "   ";

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
