// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Transport;
using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Tests.Messaging.Transport;

/// <summary>
/// Unit tests for <see cref="KafkaMappingContext"/>.
/// </summary>
/// <remarks>
/// Tests the Kafka mapping context implementation.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
[Trait("Priority", "0")]
public sealed class KafkaMappingContextShould
{
	#region Default Values Tests

	[Fact]
	public void Default_HasNullTopic()
	{
		// Arrange & Act
		var context = new KafkaMappingContext();

		// Assert
		context.Topic.ShouldBeNull();
	}

	[Fact]
	public void Default_HasNullKey()
	{
		// Arrange & Act
		var context = new KafkaMappingContext();

		// Assert
		context.Key.ShouldBeNull();
	}

	[Fact]
	public void Default_HasNullPartition()
	{
		// Arrange & Act
		var context = new KafkaMappingContext();

		// Assert
		context.Partition.ShouldBeNull();
	}

	[Fact]
	public void Default_HasNullSchemaId()
	{
		// Arrange & Act
		var context = new KafkaMappingContext();

		// Assert
		context.SchemaId.ShouldBeNull();
	}

	[Fact]
	public void Default_HasEmptyHeaders()
	{
		// Arrange & Act
		var context = new KafkaMappingContext();

		// Assert
		context.Headers.ShouldBeEmpty();
	}

	#endregion

	#region Property Setter Tests

	[Fact]
	public void Topic_CanBeSet()
	{
		// Arrange
		var context = new KafkaMappingContext();

		// Act
		context.Topic = "orders-topic";

		// Assert
		context.Topic.ShouldBe("orders-topic");
	}

	[Fact]
	public void Key_CanBeSet()
	{
		// Arrange
		var context = new KafkaMappingContext();

		// Act
		context.Key = "order-12345";

		// Assert
		context.Key.ShouldBe("order-12345");
	}

	[Fact]
	public void Partition_CanBeSet()
	{
		// Arrange
		var context = new KafkaMappingContext();

		// Act
		context.Partition = 5;

		// Assert
		context.Partition.ShouldBe(5);
	}

	[Fact]
	public void Partition_CanBeSetToZero()
	{
		// Arrange
		var context = new KafkaMappingContext();

		// Act
		context.Partition = 0;

		// Assert
		context.Partition.ShouldBe(0);
	}

	[Fact]
	public void SchemaId_CanBeSet()
	{
		// Arrange
		var context = new KafkaMappingContext();

		// Act
		context.SchemaId = 42;

		// Assert
		context.SchemaId.ShouldBe(42);
	}

	#endregion

	#region SetHeader Tests

	[Fact]
	public void SetHeader_AddsHeader()
	{
		// Arrange
		var context = new KafkaMappingContext();

		// Act
		context.SetHeader("x-custom-header", "custom-value");

		// Assert
		context.Headers.ShouldContainKey("x-custom-header");
		context.Headers["x-custom-header"].ShouldBe("custom-value");
	}

	[Fact]
	public void SetHeader_WithSameKey_OverwritesValue()
	{
		// Arrange
		var context = new KafkaMappingContext();
		context.SetHeader("x-header", "value1");

		// Act
		context.SetHeader("x-header", "value2");

		// Assert
		context.Headers["x-header"].ShouldBe("value2");
		context.Headers.Count.ShouldBe(1);
	}

	[Fact]
	public void SetHeader_IsCaseInsensitive()
	{
		// Arrange
		var context = new KafkaMappingContext();
		context.SetHeader("X-Custom-Header", "value1");

		// Act
		context.SetHeader("x-custom-header", "value2");

		// Assert
		context.Headers.Count.ShouldBe(1);
		context.Headers["X-CUSTOM-HEADER"].ShouldBe("value2");
	}

	[Fact]
	public void SetHeader_WithNullKey_ThrowsArgumentException()
	{
		// Arrange
		var context = new KafkaMappingContext();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => context.SetHeader(null!, "value"));
	}

	[Fact]
	public void SetHeader_WithEmptyKey_ThrowsArgumentException()
	{
		// Arrange
		var context = new KafkaMappingContext();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => context.SetHeader(string.Empty, "value"));
	}

	[Fact]
	public void SetHeader_WithWhitespaceKey_ThrowsArgumentException()
	{
		// Arrange
		var context = new KafkaMappingContext();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => context.SetHeader("   ", "value"));
	}

	[Fact]
	public void SetHeader_CanAddMultipleHeaders()
	{
		// Arrange
		var context = new KafkaMappingContext();

		// Act
		context.SetHeader("header1", "value1");
		context.SetHeader("header2", "value2");
		context.SetHeader("header3", "value3");

		// Assert
		context.Headers.Count.ShouldBe(3);
		context.Headers["header1"].ShouldBe("value1");
		context.Headers["header2"].ShouldBe("value2");
		context.Headers["header3"].ShouldBe("value3");
	}

	[Fact]
	public void SetHeader_WithNullValue_SetsNullValue()
	{
		// Arrange
		var context = new KafkaMappingContext();

		// Act
		context.SetHeader("header", null!);

		// Assert
		context.Headers.ShouldContainKey("header");
		context.Headers["header"].ShouldBeNull();
	}

	#endregion

	#region ApplyTo Tests

	[Fact]
	public void ApplyTo_WithNullContext_ThrowsArgumentNullException()
	{
		// Arrange
		var context = new KafkaMappingContext();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => context.ApplyTo(null!));
	}

	[Fact]
	public void ApplyTo_AppliesTopic()
	{
		// Arrange
		var mappingContext = new KafkaMappingContext { Topic = "test-topic" };
		var messageContext = new KafkaMessageContext();

		// Act
		mappingContext.ApplyTo(messageContext);

		// Assert
		messageContext.Topic.ShouldBe("test-topic");
	}

	[Fact]
	public void ApplyTo_AppliesKey()
	{
		// Arrange
		var mappingContext = new KafkaMappingContext { Key = "message-key" };
		var messageContext = new KafkaMessageContext();

		// Act
		mappingContext.ApplyTo(messageContext);

		// Assert
		messageContext.Key.ShouldBe("message-key");
	}

	[Fact]
	public void ApplyTo_AppliesPartition()
	{
		// Arrange
		var mappingContext = new KafkaMappingContext { Partition = 7 };
		var messageContext = new KafkaMessageContext();

		// Act
		mappingContext.ApplyTo(messageContext);

		// Assert
		messageContext.Partition.ShouldBe(7);
	}

	[Fact]
	public void ApplyTo_AppliesPartitionZero()
	{
		// Arrange
		var mappingContext = new KafkaMappingContext { Partition = 0 };
		var messageContext = new KafkaMessageContext();

		// Act
		mappingContext.ApplyTo(messageContext);

		// Assert
		messageContext.Partition.ShouldBe(0);
	}

	[Fact]
	public void ApplyTo_AppliesHeaders()
	{
		// Arrange
		var mappingContext = new KafkaMappingContext();
		mappingContext.SetHeader("x-custom", "custom-value");
		var messageContext = new KafkaMessageContext();

		// Act
		mappingContext.ApplyTo(messageContext);

		// Assert
		messageContext.Headers.ShouldContainKey("x-custom");
		messageContext.Headers["x-custom"].ShouldBe("custom-value");
	}

	[Fact]
	public void ApplyTo_DoesNotApplyNullTopic()
	{
		// Arrange
		var mappingContext = new KafkaMappingContext(); // Topic is null
		var messageContext = new KafkaMessageContext { Topic = "existing-topic" };

		// Act
		mappingContext.ApplyTo(messageContext);

		// Assert - Original value should be preserved
		messageContext.Topic.ShouldBe("existing-topic");
	}

	[Fact]
	public void ApplyTo_DoesNotApplyNullKey()
	{
		// Arrange
		var mappingContext = new KafkaMappingContext(); // Key is null
		var messageContext = new KafkaMessageContext { Key = "existing-key" };

		// Act
		mappingContext.ApplyTo(messageContext);

		// Assert - Original value should be preserved
		messageContext.Key.ShouldBe("existing-key");
	}

	[Fact]
	public void ApplyTo_DoesNotApplyNullPartition()
	{
		// Arrange
		var mappingContext = new KafkaMappingContext(); // Partition is null
		var messageContext = new KafkaMessageContext { Partition = 5 };

		// Act
		mappingContext.ApplyTo(messageContext);

		// Assert - Original value should be preserved
		messageContext.Partition.ShouldBe(5);
	}

	[Fact]
	public void ApplyTo_AppliesAllConfiguredValues()
	{
		// Arrange
		var mappingContext = new KafkaMappingContext
		{
			Topic = "my-topic",
			Key = "my-key",
			Partition = 3,
			SchemaId = 100, // Note: SchemaId is not applied by ApplyTo
		};
		mappingContext.SetHeader("header1", "value1");
		mappingContext.SetHeader("header2", "value2");

		var messageContext = new KafkaMessageContext();

		// Act
		mappingContext.ApplyTo(messageContext);

		// Assert
		messageContext.Topic.ShouldBe("my-topic");
		messageContext.Key.ShouldBe("my-key");
		messageContext.Partition.ShouldBe(3);
		messageContext.Headers["header1"].ShouldBe("value1");
		messageContext.Headers["header2"].ShouldBe("value2");
	}

	[Fact]
	public void ApplyTo_MergesHeadersWithExisting()
	{
		// Arrange
		var mappingContext = new KafkaMappingContext();
		mappingContext.SetHeader("new-header", "new-value");

		var messageContext = new KafkaMessageContext();
		messageContext.SetHeader("existing-header", "existing-value");

		// Act
		mappingContext.ApplyTo(messageContext);

		// Assert
		messageContext.Headers.Count.ShouldBe(2);
		messageContext.Headers["existing-header"].ShouldBe("existing-value");
		messageContext.Headers["new-header"].ShouldBe("new-value");
	}

	[Fact]
	public void ApplyTo_OverwritesExistingHeader()
	{
		// Arrange
		var mappingContext = new KafkaMappingContext();
		mappingContext.SetHeader("shared-header", "mapping-value");

		var messageContext = new KafkaMessageContext();
		messageContext.SetHeader("shared-header", "original-value");

		// Act
		mappingContext.ApplyTo(messageContext);

		// Assert
		messageContext.Headers["shared-header"].ShouldBe("mapping-value");
	}

	#endregion

	#region Interface Implementation Tests

	[Fact]
	public void ImplementsIKafkaMappingContext()
	{
		// Arrange & Act
		var context = new KafkaMappingContext();

		// Assert
		_ = context.ShouldBeAssignableTo<IKafkaMappingContext>();
	}

	#endregion
}
