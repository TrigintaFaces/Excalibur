// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.DynamoDb;
namespace Excalibur.Data.Tests.DynamoDb.Cdc;

/// <summary>
/// Unit tests for <see cref="DynamoDbCdcOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Data")]
[Trait("Feature", "DynamoDb")]
public sealed class DynamoDbCdcOptionsShould : UnitTestBase
{
	[Fact]
	public void HaveDefaultValues()
	{
		// Act
		var options = new DynamoDbCdcOptions();

		// Assert
		options.TableName.ShouldBe(string.Empty);
		options.StreamArn.ShouldBeNull();
		options.ProcessorName.ShouldBe("cdc-processor");
		options.MaxBatchSize.ShouldBe(100);
		options.PollInterval.ShouldBe(TimeSpan.FromSeconds(1));
		options.StartPosition.ShouldBeNull();
		options.StreamViewType.ShouldBe(DynamoDbStreamViewType.NewAndOldImages);
		options.MaxWaitTime.ShouldBe(TimeSpan.FromSeconds(30));
		options.AutoDiscoverShards.ShouldBeTrue();
		options.ShardDiscoveryInterval.ShouldBe(TimeSpan.FromMinutes(5));
	}

	[Fact]
	public void ValidateWithTableName()
	{
		// Arrange
		var options = new DynamoDbCdcOptions
		{
			TableName = "TestTable"
		};

		// Act & Assert - should not throw
		Should.NotThrow(() => options.Validate());
	}

	[Fact]
	public void ValidateWithStreamArn()
	{
		// Arrange
		var options = new DynamoDbCdcOptions
		{
			StreamArn = "arn:aws:dynamodb:us-east-1:123456789012:table/TestTable/stream/2023-01-01T00:00:00.000"
		};

		// Act & Assert - should not throw
		Should.NotThrow(() => options.Validate());
	}

	[Fact]
	public void ThrowWhenBothTableNameAndStreamArnMissing()
	{
		// Arrange
		var options = new DynamoDbCdcOptions();

		// Act & Assert
		var exception = Should.Throw<InvalidOperationException>(() => options.Validate());
		exception.Message.ShouldContain("TableName");
		exception.Message.ShouldContain("StreamArn");
	}

	[Fact]
	public void ThrowWhenProcessorNameEmpty()
	{
		// Arrange
		var options = new DynamoDbCdcOptions
		{
			TableName = "TestTable",
			ProcessorName = ""
		};

		// Act & Assert
		var exception = Should.Throw<InvalidOperationException>(() => options.Validate());
		exception.Message.ShouldContain("ProcessorName");
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	[InlineData(1001)]
	public void ThrowWhenMaxBatchSizeInvalid(int maxBatchSize)
	{
		// Arrange
		var options = new DynamoDbCdcOptions
		{
			TableName = "TestTable",
			MaxBatchSize = maxBatchSize
		};

		// Act & Assert
		var exception = Should.Throw<InvalidOperationException>(() => options.Validate());
		exception.Message.ShouldContain("MaxBatchSize");
	}

	[Fact]
	public void ValidateWithMaxBatchSizeAtUpperBound()
	{
		// Arrange
		var options = new DynamoDbCdcOptions
		{
			TableName = "TestTable",
			MaxBatchSize = 1000
		};

		// Act & Assert - should not throw
		Should.NotThrow(() => options.Validate());
	}

	[Fact]
	public void ThrowWhenPollIntervalInvalid()
	{
		// Arrange
		var options = new DynamoDbCdcOptions
		{
			TableName = "TestTable",
			PollInterval = TimeSpan.Zero
		};

		// Act & Assert
		var exception = Should.Throw<InvalidOperationException>(() => options.Validate());
		exception.Message.ShouldContain("PollInterval");
	}

	[Fact]
	public void ThrowWhenMaxWaitTimeInvalid()
	{
		// Arrange
		var options = new DynamoDbCdcOptions
		{
			TableName = "TestTable",
			MaxWaitTime = TimeSpan.FromSeconds(-1)
		};

		// Act & Assert
		var exception = Should.Throw<InvalidOperationException>(() => options.Validate());
		exception.Message.ShouldContain("MaxWaitTime");
	}

	[Fact]
	public void ThrowWhenShardDiscoveryIntervalInvalid()
	{
		// Arrange
		var options = new DynamoDbCdcOptions
		{
			TableName = "TestTable",
			ShardDiscoveryInterval = TimeSpan.Zero
		};

		// Act & Assert
		var exception = Should.Throw<InvalidOperationException>(() => options.Validate());
		exception.Message.ShouldContain("ShardDiscoveryInterval");
	}

	[Fact]
	public void AllowCustomConfiguration()
	{
		// Arrange
		var streamArn = "arn:aws:dynamodb:us-east-1:123456789012:table/TestTable/stream/2023-01-01";
		var startPosition = DynamoDbCdcPosition.Now(streamArn);

		// Act
		var options = new DynamoDbCdcOptions
		{
			TableName = "CustomTable",
			StreamArn = streamArn,
			ProcessorName = "custom-processor",
			MaxBatchSize = 500,
			PollInterval = TimeSpan.FromSeconds(2),
			StartPosition = startPosition,
			StreamViewType = DynamoDbStreamViewType.KeysOnly,
			MaxWaitTime = TimeSpan.FromMinutes(1),
			AutoDiscoverShards = false,
			ShardDiscoveryInterval = TimeSpan.FromMinutes(10)
		};

		// Assert
		options.TableName.ShouldBe("CustomTable");
		options.StreamArn.ShouldBe(streamArn);
		options.ProcessorName.ShouldBe("custom-processor");
		options.MaxBatchSize.ShouldBe(500);
		options.PollInterval.ShouldBe(TimeSpan.FromSeconds(2));
		options.StartPosition.ShouldBe(startPosition);
		options.StreamViewType.ShouldBe(DynamoDbStreamViewType.KeysOnly);
		options.MaxWaitTime.ShouldBe(TimeSpan.FromMinutes(1));
		options.AutoDiscoverShards.ShouldBeFalse();
		options.ShardDiscoveryInterval.ShouldBe(TimeSpan.FromMinutes(10));
	}

	[Theory]
	[InlineData("Orders")]
	[InlineData("orders-prod")]
	[InlineData("orders_prod_v2")]
	public void ValidateAcceptsTableIdentifierShapes(string tableName)
	{
		var options = new DynamoDbCdcOptions
		{
			TableName = tableName
		};

		Should.NotThrow(() => options.Validate());
	}
}
