// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.CosmosDb;

namespace Excalibur.EventSourcing.Tests.CosmosDb;

/// <summary>
/// Unit tests for <see cref="CosmosDbEventStoreOptions"/> configuration and validation.
/// </summary>
[Trait("Category", "Unit")]
public sealed class CosmosDbEventStoreOptionsShould : UnitTestBase
{
	#region Default Values Tests

	[Fact]
	public void HaveDefaultEventsContainerName()
	{
		// Arrange & Act
		var options = new CosmosDbEventStoreOptions();

		// Assert
		options.EventsContainerName.ShouldBe("events");
	}

	[Fact]
	public void HaveDefaultPartitionKeyPath()
	{
		// Arrange & Act
		var options = new CosmosDbEventStoreOptions();

		// Assert
		options.PartitionKeyPath.ShouldBe("/streamId");
	}

	[Fact]
	public void HaveDefaultTimeToLiveOfMinusOne()
	{
		// Arrange & Act
		var options = new CosmosDbEventStoreOptions();

		// Assert
		options.DefaultTimeToLiveSeconds.ShouldBe(-1);
	}

	[Fact]
	public void HaveDefaultUseTransactionalBatchTrue()
	{
		// Arrange & Act
		var options = new CosmosDbEventStoreOptions();

		// Assert
		options.UseTransactionalBatch.ShouldBeTrue();
	}

	[Fact]
	public void HaveDefaultMaxBatchSizeOf100()
	{
		// Arrange & Act
		var options = new CosmosDbEventStoreOptions();

		// Assert
		options.MaxBatchSize.ShouldBe(100);
	}

	[Fact]
	public void HaveDefaultChangeFeedPollIntervalOf1000()
	{
		// Arrange & Act
		var options = new CosmosDbEventStoreOptions();

		// Assert
		options.ChangeFeedPollIntervalMs.ShouldBe(1000);
	}

	[Fact]
	public void HaveDefaultCreateContainerIfNotExistsTrue()
	{
		// Arrange & Act
		var options = new CosmosDbEventStoreOptions();

		// Assert
		options.CreateContainerIfNotExists.ShouldBeTrue();
	}

	[Fact]
	public void HaveDefaultContainerThroughputOf400()
	{
		// Arrange & Act
		var options = new CosmosDbEventStoreOptions();

		// Assert
		options.ContainerThroughput.ShouldBe(400);
	}

	#endregion Default Values Tests

	#region Property Setters Tests

	[Fact]
	public void AllowCustomEventsContainerName()
	{
		// Arrange & Act
		var options = new CosmosDbEventStoreOptions
		{
			EventsContainerName = "custom_events"
		};

		// Assert
		options.EventsContainerName.ShouldBe("custom_events");
	}

	[Fact]
	public void AllowCustomPartitionKeyPath()
	{
		// Arrange & Act
		var options = new CosmosDbEventStoreOptions
		{
			PartitionKeyPath = "/customPartition"
		};

		// Assert
		options.PartitionKeyPath.ShouldBe("/customPartition");
	}

	[Fact]
	public void AllowCustomDefaultTimeToLive()
	{
		// Arrange & Act
		var options = new CosmosDbEventStoreOptions
		{
			DefaultTimeToLiveSeconds = 86400
		};

		// Assert
		options.DefaultTimeToLiveSeconds.ShouldBe(86400);
	}

	[Fact]
	public void AllowZeroTimeToLive()
	{
		// Arrange & Act
		var options = new CosmosDbEventStoreOptions
		{
			DefaultTimeToLiveSeconds = 0
		};

		// Assert
		options.DefaultTimeToLiveSeconds.ShouldBe(0);
	}

	[Fact]
	public void AllowCustomUseTransactionalBatch()
	{
		// Arrange & Act
		var options = new CosmosDbEventStoreOptions
		{
			UseTransactionalBatch = false
		};

		// Assert
		options.UseTransactionalBatch.ShouldBeFalse();
	}

	[Fact]
	public void AllowCustomMaxBatchSize()
	{
		// Arrange & Act
		var options = new CosmosDbEventStoreOptions
		{
			MaxBatchSize = 50
		};

		// Assert
		options.MaxBatchSize.ShouldBe(50);
	}

	[Fact]
	public void AllowCustomChangeFeedPollInterval()
	{
		// Arrange & Act
		var options = new CosmosDbEventStoreOptions
		{
			ChangeFeedPollIntervalMs = 2000
		};

		// Assert
		options.ChangeFeedPollIntervalMs.ShouldBe(2000);
	}

	[Fact]
	public void AllowCustomCreateContainerIfNotExists()
	{
		// Arrange & Act
		var options = new CosmosDbEventStoreOptions
		{
			CreateContainerIfNotExists = false
		};

		// Assert
		options.CreateContainerIfNotExists.ShouldBeFalse();
	}

	[Fact]
	public void AllowCustomContainerThroughput()
	{
		// Arrange & Act
		var options = new CosmosDbEventStoreOptions
		{
			ContainerThroughput = 1000
		};

		// Assert
		options.ContainerThroughput.ShouldBe(1000);
	}

	#endregion Property Setters Tests
}
