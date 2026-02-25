// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Delivery;

namespace Excalibur.Outbox.Tests;

/// <summary>
/// Unit tests for <see cref="DeduplicationContext"/>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class DeduplicationContextShould : UnitTestBase
{
	#region Required Property Tests

	[Fact]
	public void Constructor_RequiresProcessorId()
	{
		// Arrange & Act
		var context = new DeduplicationContext { ProcessorId = "processor-1" };

		// Assert
		context.ProcessorId.ShouldBe("processor-1");
	}

	#endregion

	#region Optional Property Tests

	[Fact]
	public void MessageType_DefaultsToNull()
	{
		// Arrange & Act
		var context = new DeduplicationContext { ProcessorId = "processor-1" };

		// Assert
		context.MessageType.ShouldBeNull();
	}

	[Fact]
	public void MessageType_CanBeSet()
	{
		// Arrange & Act
		var context = new DeduplicationContext
		{
			ProcessorId = "processor-1",
			MessageType = "OrderCreated"
		};

		// Assert
		context.MessageType.ShouldBe("OrderCreated");
	}

	[Fact]
	public void PartitionKey_DefaultsToNull()
	{
		// Arrange & Act
		var context = new DeduplicationContext { ProcessorId = "processor-1" };

		// Assert
		context.PartitionKey.ShouldBeNull();
	}

	[Fact]
	public void PartitionKey_CanBeSet()
	{
		// Arrange & Act
		var context = new DeduplicationContext
		{
			ProcessorId = "processor-1",
			PartitionKey = "tenant-123"
		};

		// Assert
		context.PartitionKey.ShouldBe("tenant-123");
	}

	[Fact]
	public void CorrelationId_DefaultsToNull()
	{
		// Arrange & Act
		var context = new DeduplicationContext { ProcessorId = "processor-1" };

		// Assert
		context.CorrelationId.ShouldBeNull();
	}

	[Fact]
	public void CorrelationId_CanBeSet()
	{
		// Arrange & Act
		var context = new DeduplicationContext
		{
			ProcessorId = "processor-1",
			CorrelationId = "corr-abc-123"
		};

		// Assert
		context.CorrelationId.ShouldBe("corr-abc-123");
	}

	[Fact]
	public void Source_DefaultsToNull()
	{
		// Arrange & Act
		var context = new DeduplicationContext { ProcessorId = "processor-1" };

		// Assert
		context.Source.ShouldBeNull();
	}

	[Fact]
	public void Source_CanBeSet()
	{
		// Arrange & Act
		var context = new DeduplicationContext
		{
			ProcessorId = "processor-1",
			Source = "orders-queue"
		};

		// Assert
		context.Source.ShouldBe("orders-queue");
	}

	#endregion

	#region Tags Tests

	[Fact]
	public void Tags_DefaultsToEmptyDictionary()
	{
		// Arrange & Act
		var context = new DeduplicationContext { ProcessorId = "processor-1" };

		// Assert
		_ = context.Tags.ShouldNotBeNull();
		context.Tags.ShouldBeEmpty();
	}

	[Fact]
	public void Tags_CanBeInitializedWithValues()
	{
		// Arrange & Act
		var context = new DeduplicationContext
		{
			ProcessorId = "processor-1",
			Tags = new Dictionary<string, string>
			{
				["environment"] = "production",
				["region"] = "us-east"
			}
		};

		// Assert
		context.Tags.Count.ShouldBe(2);
		context.Tags["environment"].ShouldBe("production");
		context.Tags["region"].ShouldBe("us-east");
	}

	[Fact]
	public void Tags_CanBeModifiedAfterCreation()
	{
		// Arrange
		var context = new DeduplicationContext { ProcessorId = "processor-1" };

		// Act
		context.Tags["key1"] = "value1";

		// Assert
		context.Tags["key1"].ShouldBe("value1");
	}

	#endregion

	#region Full Context Tests

	[Fact]
	public void FullContext_AllPropertiesSet()
	{
		// Arrange & Act
		var context = new DeduplicationContext
		{
			ProcessorId = "processor-1",
			MessageType = "OrderCreated",
			PartitionKey = "tenant-123",
			CorrelationId = "corr-abc",
			Source = "orders-queue",
			Tags = new Dictionary<string, string>
			{
				["version"] = "2.0"
			}
		};

		// Assert
		context.ProcessorId.ShouldBe("processor-1");
		context.MessageType.ShouldBe("OrderCreated");
		context.PartitionKey.ShouldBe("tenant-123");
		context.CorrelationId.ShouldBe("corr-abc");
		context.Source.ShouldBe("orders-queue");
		context.Tags["version"].ShouldBe("2.0");
	}

	#endregion
}
