// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Transport;
using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Tests.Messaging.Transport;

/// <summary>
/// Unit tests for <see cref="AzureServiceBusMappingContext"/>.
/// </summary>
/// <remarks>
/// Tests the Azure Service Bus mapping context implementation.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
[Trait("Priority", "0")]
public sealed class AzureServiceBusMappingContextShould
{
	#region Default Values Tests

	[Fact]
	public void Default_HasNullTopicOrQueueName()
	{
		// Arrange & Act
		var context = new AzureServiceBusMappingContext();

		// Assert
		context.TopicOrQueueName.ShouldBeNull();
	}

	[Fact]
	public void Default_HasNullSessionId()
	{
		// Arrange & Act
		var context = new AzureServiceBusMappingContext();

		// Assert
		context.SessionId.ShouldBeNull();
	}

	[Fact]
	public void Default_HasNullPartitionKey()
	{
		// Arrange & Act
		var context = new AzureServiceBusMappingContext();

		// Assert
		context.PartitionKey.ShouldBeNull();
	}

	[Fact]
	public void Default_HasNullReplyToSessionId()
	{
		// Arrange & Act
		var context = new AzureServiceBusMappingContext();

		// Assert
		context.ReplyToSessionId.ShouldBeNull();
	}

	[Fact]
	public void Default_HasNullTimeToLive()
	{
		// Arrange & Act
		var context = new AzureServiceBusMappingContext();

		// Assert
		context.TimeToLive.ShouldBeNull();
	}

	[Fact]
	public void Default_HasNullScheduledEnqueueTime()
	{
		// Arrange & Act
		var context = new AzureServiceBusMappingContext();

		// Assert
		context.ScheduledEnqueueTime.ShouldBeNull();
	}

	[Fact]
	public void Default_HasEmptyProperties()
	{
		// Arrange & Act
		var context = new AzureServiceBusMappingContext();

		// Assert
		context.Properties.ShouldBeEmpty();
	}

	#endregion

	#region Property Setter Tests

	[Fact]
	public void TopicOrQueueName_CanBeSet()
	{
		// Arrange
		var context = new AzureServiceBusMappingContext();

		// Act
		context.TopicOrQueueName = "orders-topic";

		// Assert
		context.TopicOrQueueName.ShouldBe("orders-topic");
	}

	[Fact]
	public void SessionId_CanBeSet()
	{
		// Arrange
		var context = new AzureServiceBusMappingContext();

		// Act
		context.SessionId = "session-123";

		// Assert
		context.SessionId.ShouldBe("session-123");
	}

	[Fact]
	public void PartitionKey_CanBeSet()
	{
		// Arrange
		var context = new AzureServiceBusMappingContext();

		// Act
		context.PartitionKey = "partition-abc";

		// Assert
		context.PartitionKey.ShouldBe("partition-abc");
	}

	[Fact]
	public void ReplyToSessionId_CanBeSet()
	{
		// Arrange
		var context = new AzureServiceBusMappingContext();

		// Act
		context.ReplyToSessionId = "reply-session-456";

		// Assert
		context.ReplyToSessionId.ShouldBe("reply-session-456");
	}

	[Fact]
	public void TimeToLive_CanBeSet()
	{
		// Arrange
		var context = new AzureServiceBusMappingContext();
		var ttl = TimeSpan.FromMinutes(30);

		// Act
		context.TimeToLive = ttl;

		// Assert
		context.TimeToLive.ShouldBe(ttl);
	}

	[Fact]
	public void TimeToLive_CanBeSetToZero()
	{
		// Arrange
		var context = new AzureServiceBusMappingContext();

		// Act
		context.TimeToLive = TimeSpan.Zero;

		// Assert
		context.TimeToLive.ShouldBe(TimeSpan.Zero);
	}

	[Fact]
	public void ScheduledEnqueueTime_CanBeSet()
	{
		// Arrange
		var context = new AzureServiceBusMappingContext();
		var scheduledTime = DateTimeOffset.UtcNow.AddHours(1);

		// Act
		context.ScheduledEnqueueTime = scheduledTime;

		// Assert
		context.ScheduledEnqueueTime.ShouldBe(scheduledTime);
	}

	#endregion

	#region SetProperty Tests

	[Fact]
	public void SetProperty_AddsProperty()
	{
		// Arrange
		var context = new AzureServiceBusMappingContext();

		// Act
		context.SetProperty("CustomProperty", "custom-value");

		// Assert
		context.Properties.ShouldContainKey("CustomProperty");
		context.Properties["CustomProperty"].ShouldBe("custom-value");
	}

	[Fact]
	public void SetProperty_WithIntValue()
	{
		// Arrange
		var context = new AzureServiceBusMappingContext();

		// Act
		context.SetProperty("Count", 42);

		// Assert
		context.Properties["Count"].ShouldBe(42);
	}

	[Fact]
	public void SetProperty_WithBoolValue()
	{
		// Arrange
		var context = new AzureServiceBusMappingContext();

		// Act
		context.SetProperty("IsEnabled", true);

		// Assert
		context.Properties["IsEnabled"].ShouldBe(true);
	}

	[Fact]
	public void SetProperty_WithSameKey_OverwritesValue()
	{
		// Arrange
		var context = new AzureServiceBusMappingContext();
		context.SetProperty("prop", "original");

		// Act
		context.SetProperty("prop", "updated");

		// Assert
		context.Properties["prop"].ShouldBe("updated");
		context.Properties.Count.ShouldBe(1);
	}

	[Fact]
	public void SetProperty_IsCaseInsensitive()
	{
		// Arrange
		var context = new AzureServiceBusMappingContext();
		context.SetProperty("Prop", "value1");

		// Act
		context.SetProperty("prop", "value2");

		// Assert
		context.Properties.Count.ShouldBe(1);
	}

	[Fact]
	public void SetProperty_WithNullKey_ThrowsArgumentException()
	{
		// Arrange
		var context = new AzureServiceBusMappingContext();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => context.SetProperty(null!, "value"));
	}

	[Fact]
	public void SetProperty_WithEmptyKey_ThrowsArgumentException()
	{
		// Arrange
		var context = new AzureServiceBusMappingContext();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => context.SetProperty(string.Empty, "value"));
	}

	[Fact]
	public void SetProperty_WithWhitespaceKey_ThrowsArgumentException()
	{
		// Arrange
		var context = new AzureServiceBusMappingContext();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => context.SetProperty("   ", "value"));
	}

	[Fact]
	public void SetProperty_CanAddMultipleProperties()
	{
		// Arrange
		var context = new AzureServiceBusMappingContext();

		// Act
		context.SetProperty("StringProp", "value");
		context.SetProperty("IntProp", 42);
		context.SetProperty("BoolProp", true);

		// Assert
		context.Properties.Count.ShouldBe(3);
	}

	#endregion

	#region ApplyTo Tests

	[Fact]
	public void ApplyTo_WithNullContext_ThrowsArgumentNullException()
	{
		// Arrange
		var context = new AzureServiceBusMappingContext();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => context.ApplyTo(null!));
	}

	[Fact]
	public void ApplyTo_AppliesPropertiesToTransportContext()
	{
		// Arrange
		var mappingContext = new AzureServiceBusMappingContext();
		mappingContext.SetProperty("CustomProp", "custom-value");
		var messageContext = new TransportMessageContext();

		// Act
		mappingContext.ApplyTo(messageContext);

		// Assert
		messageContext.GetTransportProperty<string>("CustomProp").ShouldBe("custom-value");
	}

	[Fact]
	public void ApplyTo_AppliesMultipleProperties()
	{
		// Arrange
		var mappingContext = new AzureServiceBusMappingContext();
		mappingContext.SetProperty("Prop1", "value1");
		mappingContext.SetProperty("Prop2", 42);
		var messageContext = new TransportMessageContext();

		// Act
		mappingContext.ApplyTo(messageContext);

		// Assert
		messageContext.GetTransportProperty<string>("Prop1").ShouldBe("value1");
		messageContext.GetTransportProperty<int>("Prop2").ShouldBe(42);
	}

	[Fact]
	public void ApplyTo_WithNoProperties_DoesNothing()
	{
		// Arrange
		var mappingContext = new AzureServiceBusMappingContext();
		var messageContext = new TransportMessageContext();

		// Act
		mappingContext.ApplyTo(messageContext);

		// Assert
		messageContext.GetAllTransportProperties().ShouldBeEmpty();
	}

	#endregion

	#region Interface Implementation Tests

	[Fact]
	public void ImplementsIAzureServiceBusMappingContext()
	{
		// Arrange & Act
		var context = new AzureServiceBusMappingContext();

		// Assert
		_ = context.ShouldBeAssignableTo<IAzureServiceBusMappingContext>();
	}

	#endregion
}
