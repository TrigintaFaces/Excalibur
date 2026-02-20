// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Amazon;

namespace Excalibur.Outbox.Tests.DynamoDb;

/// <summary>
/// Unit tests for <see cref="DynamoDbOutboxOptions" />.
/// </summary>
[Trait("Category", "Unit")]
public sealed class DynamoDbOutboxOptionsShould : UnitTestBase
{
	[Fact]
	public void DefaultValues_AreCorrect()
	{
		// Act
		var options = new DynamoDbOutboxOptions();

		// Assert
		options.ServiceUrl.ShouldBeNull();
		options.Region.ShouldBeNull();
		options.AccessKey.ShouldBeNull();
		options.SecretKey.ShouldBeNull();
		options.TableName.ShouldBe("outbox");
		options.PartitionKeyAttribute.ShouldBe("pk");
		options.SortKeyAttribute.ShouldBe("sk");
		options.TtlAttribute.ShouldBe("ttl");
		options.DefaultTimeToLiveSeconds.ShouldBe(604800); // 7 days
		options.MaxRetryAttempts.ShouldBe(3);
		options.CreateTableIfNotExists.ShouldBeTrue();
		options.EnableStreams.ShouldBeTrue();
	}

	[Fact]
	public void ServiceUrl_CanBeSet()
	{
		// Arrange
		var options = new DynamoDbOutboxOptions();
		const string serviceUrl = "http://localhost:8000";

		// Act
		options.ServiceUrl = serviceUrl;

		// Assert
		options.ServiceUrl.ShouldBe(serviceUrl);
	}

	[Fact]
	public void Region_CanBeSet()
	{
		// Arrange
		var options = new DynamoDbOutboxOptions();
		const string region = "us-east-1";

		// Act
		options.Region = region;

		// Assert
		options.Region.ShouldBe(region);
	}

	[Fact]
	public void AccessKey_CanBeSet()
	{
		// Arrange
		var options = new DynamoDbOutboxOptions();
		const string accessKey = "AKIAIOSFODNN7EXAMPLE";

		// Act
		options.AccessKey = accessKey;

		// Assert
		options.AccessKey.ShouldBe(accessKey);
	}

	[Fact]
	public void SecretKey_CanBeSet()
	{
		// Arrange
		var options = new DynamoDbOutboxOptions();
		const string secretKey = "wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY";

		// Act
		options.SecretKey = secretKey;

		// Assert
		options.SecretKey.ShouldBe(secretKey);
	}

	[Fact]
	public void TableName_CanBeSet()
	{
		// Arrange
		var options = new DynamoDbOutboxOptions();
		const string tableName = "custom-outbox";

		// Act
		options.TableName = tableName;

		// Assert
		options.TableName.ShouldBe(tableName);
	}

	[Fact]
	public void DefaultTimeToLiveSeconds_CanBeSet()
	{
		// Arrange
		var options = new DynamoDbOutboxOptions();
		const int ttl = 0; // Disable TTL

		// Act
		options.DefaultTimeToLiveSeconds = ttl;

		// Assert
		options.DefaultTimeToLiveSeconds.ShouldBe(ttl);
	}

	[Fact]
	public void MaxRetryAttempts_CanBeSet()
	{
		// Arrange
		var options = new DynamoDbOutboxOptions();
		const int retries = 5;

		// Act
		options.MaxRetryAttempts = retries;

		// Assert
		options.MaxRetryAttempts.ShouldBe(retries);
	}

	[Fact]
	public void GetRegionEndpoint_ReturnsNull_WhenRegionNotSet()
	{
		// Arrange
		var options = new DynamoDbOutboxOptions();

		// Act
		var endpoint = options.GetRegionEndpoint();

		// Assert
		endpoint.ShouldBeNull();
	}

	[Fact]
	public void GetRegionEndpoint_ReturnsEndpoint_WhenRegionSet()
	{
		// Arrange
		var options = new DynamoDbOutboxOptions
		{
			Region = "us-west-2",
		};

		// Act
		var endpoint = options.GetRegionEndpoint();

		// Assert
		_ = endpoint.ShouldNotBeNull();
		endpoint.ShouldBe(RegionEndpoint.USWest2);
	}

	[Fact]
	public void Validate_Succeeds_WhenServiceUrlProvided()
	{
		// Arrange
		var options = new DynamoDbOutboxOptions
		{
			ServiceUrl = "http://localhost:8000",
		};

		// Act & Assert - Should not throw
		options.Validate();
	}

	[Fact]
	public void Validate_Succeeds_WhenRegionProvided()
	{
		// Arrange
		var options = new DynamoDbOutboxOptions
		{
			Region = "us-east-1",
		};

		// Act & Assert - Should not throw
		options.Validate();
	}

	[Fact]
	public void Validate_ThrowsInvalidOperationException_WhenNoConfigProvided()
	{
		// Arrange
		var options = new DynamoDbOutboxOptions();

		// Act & Assert
		var exception = Should.Throw<InvalidOperationException>(() => options.Validate());
		exception.Message.ShouldContain("ServiceUrl");
		exception.Message.ShouldContain("Region");
	}
}
