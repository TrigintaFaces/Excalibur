// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.DynamoDb;
namespace Excalibur.Data.Tests.DynamoDb;

/// <summary>
/// Unit tests for <see cref="DynamoDbOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Data")]
[Trait("Feature", "DynamoDb")]
public sealed class DynamoDbOptionsShould : UnitTestBase
{
	[Fact]
	public void HaveDefaultValues()
	{
		// Act
		var options = new DynamoDbOptions();

		// Assert - root properties
		options.Name.ShouldBe("DynamoDb");
		options.DefaultTableName.ShouldBeNull();
		options.DefaultPartitionKeyAttribute.ShouldBe("pk");
		options.DefaultSortKeyAttribute.ShouldBe("sk");
		options.UseConsistentReads.ShouldBeFalse();
		options.EnableStreams.ShouldBeFalse();
		options.StreamViewType.ShouldBe("NEW_AND_OLD_IMAGES");

		// Assert - connection sub-options
		options.Connection.ShouldNotBeNull();
		options.Connection.ServiceUrl.ShouldBeNull();
		options.Connection.Region.ShouldBeNull();
		options.Connection.AccessKey.ShouldBeNull();
		options.Connection.SecretKey.ShouldBeNull();
		options.Connection.MaxRetryAttempts.ShouldBe(3);
		options.Connection.TimeoutInSeconds.ShouldBe(30);
		options.Connection.ReadCapacityUnits.ShouldBeNull();
		options.Connection.WriteCapacityUnits.ShouldBeNull();
	}

	[Fact]
	public void ValidateWithServiceUrl()
	{
		// Arrange
		var options = new DynamoDbOptions
		{
			Connection = new DynamoDbConnectionOptions
			{
				ServiceUrl = "http://localhost:8000",
			},
		};

		// Act & Assert - should not throw
		Should.NotThrow(() => options.Validate());
	}

	[Fact]
	public void ValidateWithRegion()
	{
		// Arrange
		var options = new DynamoDbOptions
		{
			Connection = new DynamoDbConnectionOptions
			{
				Region = "us-east-1",
			},
		};

		// Act & Assert - should not throw
		Should.NotThrow(() => options.Validate());
	}

	[Fact]
	public void ValidateWithExplicitCredentials()
	{
		// Arrange
		var options = new DynamoDbOptions
		{
			Connection = new DynamoDbConnectionOptions
			{
				Region = "us-east-1",
				AccessKey = "AKIAIOSFODNN7EXAMPLE",
				SecretKey = "wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY",
			},
		};

		// Act & Assert - should not throw
		Should.NotThrow(() => options.Validate());
	}

	[Fact]
	public void ThrowWhenNeitherServiceUrlNorRegionProvided()
	{
		// Arrange
		var options = new DynamoDbOptions();

		// Act & Assert
		var exception = Should.Throw<InvalidOperationException>(() => options.Validate());
		exception.Message.ShouldContain("ServiceUrl");
		exception.Message.ShouldContain("Region");
	}

	[Fact]
	public void GetRegionEndpointForValidRegion()
	{
		// Arrange
		var options = new DynamoDbOptions
		{
			Connection = new DynamoDbConnectionOptions
			{
				Region = "us-east-1",
			},
		};

		// Act
		var endpoint = options.GetRegionEndpoint();

		// Assert
		_ = endpoint.ShouldNotBeNull();
		endpoint.SystemName.ShouldBe("us-east-1");
	}

	[Fact]
	public void GetNullRegionEndpointWhenRegionNotSet()
	{
		// Arrange
		var options = new DynamoDbOptions();

		// Act
		var endpoint = options.GetRegionEndpoint();

		// Assert
		endpoint.ShouldBeNull();
	}

	[Fact]
	public void AllowCustomConfiguration()
	{
		// Act
		var options = new DynamoDbOptions
		{
			Name = "CustomDynamoDb",
			DefaultTableName = "TestTable",
			DefaultPartitionKeyAttribute = "customPk",
			DefaultSortKeyAttribute = "customSk",
			UseConsistentReads = true,
			EnableStreams = true,
			StreamViewType = "KEYS_ONLY",
			Connection = new DynamoDbConnectionOptions
			{
				ServiceUrl = "http://localhost:8000",
				Region = "us-west-2",
				AccessKey = "TestKey",
				SecretKey = "TestSecret",
				MaxRetryAttempts = 5,
				TimeoutInSeconds = 60,
				ReadCapacityUnits = 10,
				WriteCapacityUnits = 5,
			},
		};

		// Assert - root
		options.Name.ShouldBe("CustomDynamoDb");
		options.DefaultTableName.ShouldBe("TestTable");
		options.DefaultPartitionKeyAttribute.ShouldBe("customPk");
		options.DefaultSortKeyAttribute.ShouldBe("customSk");
		options.UseConsistentReads.ShouldBeTrue();
		options.EnableStreams.ShouldBeTrue();
		options.StreamViewType.ShouldBe("KEYS_ONLY");

		// Assert - connection
		options.Connection.ServiceUrl.ShouldBe("http://localhost:8000");
		options.Connection.Region.ShouldBe("us-west-2");
		options.Connection.AccessKey.ShouldBe("TestKey");
		options.Connection.SecretKey.ShouldBe("TestSecret");
		options.Connection.MaxRetryAttempts.ShouldBe(5);
		options.Connection.TimeoutInSeconds.ShouldBe(60);
		options.Connection.ReadCapacityUnits.ShouldBe(10);
		options.Connection.WriteCapacityUnits.ShouldBe(5);
	}
}
