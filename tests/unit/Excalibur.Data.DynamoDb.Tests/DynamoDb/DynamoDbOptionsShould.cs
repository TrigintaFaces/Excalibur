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

		// Assert
		options.Name.ShouldBe("DynamoDb");
		options.ServiceUrl.ShouldBeNull();
		options.Region.ShouldBeNull();
		options.AccessKey.ShouldBeNull();
		options.SecretKey.ShouldBeNull();
		options.DefaultTableName.ShouldBeNull();
		options.DefaultPartitionKeyAttribute.ShouldBe("pk");
		options.DefaultSortKeyAttribute.ShouldBe("sk");
		options.MaxRetryAttempts.ShouldBe(3);
		options.TimeoutInSeconds.ShouldBe(30);
		options.UseConsistentReads.ShouldBeFalse();
		options.ReadCapacityUnits.ShouldBeNull();
		options.WriteCapacityUnits.ShouldBeNull();
		options.EnableStreams.ShouldBeFalse();
		options.StreamViewType.ShouldBe("NEW_AND_OLD_IMAGES");
	}

	[Fact]
	public void ValidateWithServiceUrl()
	{
		// Arrange
		var options = new DynamoDbOptions
		{
			ServiceUrl = "http://localhost:8000"
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
			Region = "us-east-1"
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
			Region = "us-east-1",
			AccessKey = "AKIAIOSFODNN7EXAMPLE",
			SecretKey = "wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY"
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
			Region = "us-east-1"
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
			ServiceUrl = "http://localhost:8000",
			Region = "us-west-2",
			AccessKey = "TestKey",
			SecretKey = "TestSecret",
			DefaultTableName = "TestTable",
			DefaultPartitionKeyAttribute = "customPk",
			DefaultSortKeyAttribute = "customSk",
			MaxRetryAttempts = 5,
			TimeoutInSeconds = 60,
			UseConsistentReads = true,
			ReadCapacityUnits = 10,
			WriteCapacityUnits = 5,
			EnableStreams = true,
			StreamViewType = "KEYS_ONLY"
		};

		// Assert
		options.Name.ShouldBe("CustomDynamoDb");
		options.ServiceUrl.ShouldBe("http://localhost:8000");
		options.Region.ShouldBe("us-west-2");
		options.AccessKey.ShouldBe("TestKey");
		options.SecretKey.ShouldBe("TestSecret");
		options.DefaultTableName.ShouldBe("TestTable");
		options.DefaultPartitionKeyAttribute.ShouldBe("customPk");
		options.DefaultSortKeyAttribute.ShouldBe("customSk");
		options.MaxRetryAttempts.ShouldBe(5);
		options.TimeoutInSeconds.ShouldBe(60);
		options.UseConsistentReads.ShouldBeTrue();
		options.ReadCapacityUnits.ShouldBe(10);
		options.WriteCapacityUnits.ShouldBe(5);
		options.EnableStreams.ShouldBeTrue();
		options.StreamViewType.ShouldBe("KEYS_ONLY");
	}
}
