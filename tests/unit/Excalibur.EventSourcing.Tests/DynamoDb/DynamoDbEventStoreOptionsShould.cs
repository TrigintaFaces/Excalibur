// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.DynamoDb;

namespace Excalibur.EventSourcing.Tests.DynamoDb;

/// <summary>
/// Unit tests for <see cref="DynamoDbEventStoreOptions"/> configuration and validation.
/// </summary>
[Trait("Category", "Unit")]
public sealed class DynamoDbEventStoreOptionsShould : UnitTestBase
{
	#region Default Values Tests

	[Fact]
	public void HaveDefaultEventsTableName()
	{
		// Arrange & Act
		var options = new DynamoDbEventStoreOptions();

		// Assert
		options.EventsTableName.ShouldBe("Events");
	}

	[Fact]
	public void HaveDefaultPartitionKeyAttribute()
	{
		// Arrange & Act
		var options = new DynamoDbEventStoreOptions();

		// Assert
		options.PartitionKeyAttribute.ShouldBe("pk");
	}

	[Fact]
	public void HaveDefaultSortKeyAttribute()
	{
		// Arrange & Act
		var options = new DynamoDbEventStoreOptions();

		// Assert
		options.SortKeyAttribute.ShouldBe("sk");
	}

	[Fact]
	public void HaveDefaultUseTransactionalWriteTrue()
	{
		// Arrange & Act
		var options = new DynamoDbEventStoreOptions();

		// Assert
		options.UseTransactionalWrite.ShouldBeTrue();
	}

	[Fact]
	public void HaveDefaultMaxBatchSizeOf100()
	{
		// Arrange & Act
		var options = new DynamoDbEventStoreOptions();

		// Assert
		options.MaxBatchSize.ShouldBe(100);
	}

	[Fact]
	public void HaveDefaultStreamsPollIntervalOf1000()
	{
		// Arrange & Act
		var options = new DynamoDbEventStoreOptions();

		// Assert
		options.StreamsPollIntervalMs.ShouldBe(1000);
	}

	[Fact]
	public void HaveDefaultCreateTableIfNotExistsTrue()
	{
		// Arrange & Act
		var options = new DynamoDbEventStoreOptions();

		// Assert
		options.CreateTableIfNotExists.ShouldBeTrue();
	}

	[Fact]
	public void HaveDefaultReadCapacityUnitsOf5()
	{
		// Arrange & Act
		var options = new DynamoDbEventStoreOptions();

		// Assert
		options.ReadCapacityUnits.ShouldBe(5);
	}

	[Fact]
	public void HaveDefaultWriteCapacityUnitsOf5()
	{
		// Arrange & Act
		var options = new DynamoDbEventStoreOptions();

		// Assert
		options.WriteCapacityUnits.ShouldBe(5);
	}

	[Fact]
	public void HaveDefaultUseOnDemandCapacityTrue()
	{
		// Arrange & Act
		var options = new DynamoDbEventStoreOptions();

		// Assert
		options.UseOnDemandCapacity.ShouldBeTrue();
	}

	[Fact]
	public void HaveDefaultEnableStreamsTrue()
	{
		// Arrange & Act
		var options = new DynamoDbEventStoreOptions();

		// Assert
		options.EnableStreams.ShouldBeTrue();
	}

	#endregion Default Values Tests

	#region Property Setters Tests

	[Fact]
	public void AllowCustomEventsTableName()
	{
		// Arrange & Act
		var options = new DynamoDbEventStoreOptions
		{
			EventsTableName = "CustomEvents"
		};

		// Assert
		options.EventsTableName.ShouldBe("CustomEvents");
	}

	[Fact]
	public void AllowCustomPartitionKeyAttribute()
	{
		// Arrange & Act
		var options = new DynamoDbEventStoreOptions
		{
			PartitionKeyAttribute = "custom_pk"
		};

		// Assert
		options.PartitionKeyAttribute.ShouldBe("custom_pk");
	}

	[Fact]
	public void AllowCustomSortKeyAttribute()
	{
		// Arrange & Act
		var options = new DynamoDbEventStoreOptions
		{
			SortKeyAttribute = "custom_sk"
		};

		// Assert
		options.SortKeyAttribute.ShouldBe("custom_sk");
	}

	[Fact]
	public void AllowCustomUseTransactionalWrite()
	{
		// Arrange & Act
		var options = new DynamoDbEventStoreOptions
		{
			UseTransactionalWrite = false
		};

		// Assert
		options.UseTransactionalWrite.ShouldBeFalse();
	}

	[Fact]
	public void AllowCustomMaxBatchSize()
	{
		// Arrange & Act
		var options = new DynamoDbEventStoreOptions
		{
			MaxBatchSize = 50
		};

		// Assert
		options.MaxBatchSize.ShouldBe(50);
	}

	[Fact]
	public void AllowCustomStreamsPollInterval()
	{
		// Arrange & Act
		var options = new DynamoDbEventStoreOptions
		{
			StreamsPollIntervalMs = 2000
		};

		// Assert
		options.StreamsPollIntervalMs.ShouldBe(2000);
	}

	[Fact]
	public void AllowCustomCreateTableIfNotExists()
	{
		// Arrange & Act
		var options = new DynamoDbEventStoreOptions
		{
			CreateTableIfNotExists = false
		};

		// Assert
		options.CreateTableIfNotExists.ShouldBeFalse();
	}

	[Fact]
	public void AllowCustomReadCapacityUnits()
	{
		// Arrange & Act
		var options = new DynamoDbEventStoreOptions
		{
			ReadCapacityUnits = 10
		};

		// Assert
		options.ReadCapacityUnits.ShouldBe(10);
	}

	[Fact]
	public void AllowCustomWriteCapacityUnits()
	{
		// Arrange & Act
		var options = new DynamoDbEventStoreOptions
		{
			WriteCapacityUnits = 10
		};

		// Assert
		options.WriteCapacityUnits.ShouldBe(10);
	}

	[Fact]
	public void AllowCustomUseOnDemandCapacity()
	{
		// Arrange & Act
		var options = new DynamoDbEventStoreOptions
		{
			UseOnDemandCapacity = false
		};

		// Assert
		options.UseOnDemandCapacity.ShouldBeFalse();
	}

	[Fact]
	public void AllowCustomEnableStreams()
	{
		// Arrange & Act
		var options = new DynamoDbEventStoreOptions
		{
			EnableStreams = false
		};

		// Assert
		options.EnableStreams.ShouldBeFalse();
	}

	#endregion Property Setters Tests
}
