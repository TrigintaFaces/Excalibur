// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.DynamoDb;

namespace Excalibur.Data.Tests.DynamoDb;

/// <summary>
/// Unit tests for <see cref="DynamoDbConnectionOptions"/>.
/// Verifies defaults, property assignment, and DataAnnotations range constraints.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class DynamoDbConnectionOptionsShould : UnitTestBase
{
	[Fact]
	public void HaveExpectedDefaults()
	{
		// Arrange & Act
		var options = new DynamoDbConnectionOptions();

		// Assert
		options.ServiceUrl.ShouldBeNull();
		options.Region.ShouldBeNull();
		options.AccessKey.ShouldBeNull();
		options.SecretKey.ShouldBeNull();
		options.MaxRetryAttempts.ShouldBe(3);
		options.TimeoutInSeconds.ShouldBe(30);
		options.ReadCapacityUnits.ShouldBeNull();
		options.WriteCapacityUnits.ShouldBeNull();
	}

	[Fact]
	public void AllowCustomConnectionSettings()
	{
		// Arrange & Act
		var options = new DynamoDbConnectionOptions
		{
			ServiceUrl = "http://localhost:8000",
			Region = "us-east-1",
			AccessKey = "AKID",
			SecretKey = "SECRET"
		};

		// Assert
		options.ServiceUrl.ShouldBe("http://localhost:8000");
		options.Region.ShouldBe("us-east-1");
		options.AccessKey.ShouldBe("AKID");
		options.SecretKey.ShouldBe("SECRET");
	}

	[Fact]
	public void AllowCustomRetryAndTimeout()
	{
		// Arrange & Act
		var options = new DynamoDbConnectionOptions
		{
			MaxRetryAttempts = 5,
			TimeoutInSeconds = 60
		};

		// Assert
		options.MaxRetryAttempts.ShouldBe(5);
		options.TimeoutInSeconds.ShouldBe(60);
	}

	[Fact]
	public void AllowCustomCapacityUnits()
	{
		// Arrange & Act
		var options = new DynamoDbConnectionOptions
		{
			ReadCapacityUnits = 100,
			WriteCapacityUnits = 50
		};

		// Assert
		options.ReadCapacityUnits.ShouldBe(100);
		options.WriteCapacityUnits.ShouldBe(50);
	}

	[Fact]
	public void HaveRangeAttributeOnMaxRetryAttempts()
	{
		var prop = typeof(DynamoDbConnectionOptions).GetProperty(nameof(DynamoDbConnectionOptions.MaxRetryAttempts))!;
		var attr = prop.GetCustomAttributes(typeof(System.ComponentModel.DataAnnotations.RangeAttribute), false);
		attr.ShouldNotBeEmpty();
		var range = (System.ComponentModel.DataAnnotations.RangeAttribute)attr[0];
		range.Minimum.ShouldBe(1);
	}

	[Fact]
	public void HaveRangeAttributeOnTimeoutInSeconds()
	{
		var prop = typeof(DynamoDbConnectionOptions).GetProperty(nameof(DynamoDbConnectionOptions.TimeoutInSeconds))!;
		var attr = prop.GetCustomAttributes(typeof(System.ComponentModel.DataAnnotations.RangeAttribute), false);
		attr.ShouldNotBeEmpty();
		var range = (System.ComponentModel.DataAnnotations.RangeAttribute)attr[0];
		range.Minimum.ShouldBe(1);
	}
}
