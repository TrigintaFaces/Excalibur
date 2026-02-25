// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Transport;
using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Tests.Messaging.Transport;

/// <summary>
/// Unit tests for <see cref="GooglePubSubMappingContext"/>.
/// </summary>
/// <remarks>
/// Tests the Google Pub/Sub mapping context implementation.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
[Trait("Priority", "0")]
public sealed class GooglePubSubMappingContextShould
{
	#region Default Values Tests

	[Fact]
	public void Default_HasNullTopicName()
	{
		// Arrange & Act
		var context = new GooglePubSubMappingContext();

		// Assert
		context.TopicName.ShouldBeNull();
	}

	[Fact]
	public void Default_HasNullOrderingKey()
	{
		// Arrange & Act
		var context = new GooglePubSubMappingContext();

		// Assert
		context.OrderingKey.ShouldBeNull();
	}

	[Fact]
	public void Default_HasEmptyAttributes()
	{
		// Arrange & Act
		var context = new GooglePubSubMappingContext();

		// Assert
		context.Attributes.ShouldBeEmpty();
	}

	#endregion

	#region Property Setter Tests

	[Fact]
	public void TopicName_CanBeSet()
	{
		// Arrange
		var context = new GooglePubSubMappingContext();

		// Act
		context.TopicName = "projects/my-project/topics/my-topic";

		// Assert
		context.TopicName.ShouldBe("projects/my-project/topics/my-topic");
	}

	[Fact]
	public void OrderingKey_CanBeSet()
	{
		// Arrange
		var context = new GooglePubSubMappingContext();

		// Act
		context.OrderingKey = "order-12345";

		// Assert
		context.OrderingKey.ShouldBe("order-12345");
	}

	#endregion

	#region SetAttribute Tests

	[Fact]
	public void SetAttribute_AddsAttribute()
	{
		// Arrange
		var context = new GooglePubSubMappingContext();

		// Act
		context.SetAttribute("custom-attr", "custom-value");

		// Assert
		context.Attributes.ShouldContainKey("custom-attr");
		context.Attributes["custom-attr"].ShouldBe("custom-value");
	}

	[Fact]
	public void SetAttribute_WithSameKey_OverwritesValue()
	{
		// Arrange
		var context = new GooglePubSubMappingContext();
		context.SetAttribute("attr", "value1");

		// Act
		context.SetAttribute("attr", "value2");

		// Assert
		context.Attributes["attr"].ShouldBe("value2");
		context.Attributes.Count.ShouldBe(1);
	}

	[Fact]
	public void SetAttribute_IsCaseInsensitive()
	{
		// Arrange
		var context = new GooglePubSubMappingContext();
		context.SetAttribute("Attr", "value1");

		// Act
		context.SetAttribute("attr", "value2");

		// Assert
		context.Attributes.Count.ShouldBe(1);
	}

	[Fact]
	public void SetAttribute_WithNullKey_ThrowsArgumentException()
	{
		// Arrange
		var context = new GooglePubSubMappingContext();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => context.SetAttribute(null!, "value"));
	}

	[Fact]
	public void SetAttribute_WithEmptyKey_ThrowsArgumentException()
	{
		// Arrange
		var context = new GooglePubSubMappingContext();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => context.SetAttribute(string.Empty, "value"));
	}

	[Fact]
	public void SetAttribute_WithWhitespaceKey_ThrowsArgumentException()
	{
		// Arrange
		var context = new GooglePubSubMappingContext();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => context.SetAttribute("   ", "value"));
	}

	[Fact]
	public void SetAttribute_CanAddMultipleAttributes()
	{
		// Arrange
		var context = new GooglePubSubMappingContext();

		// Act
		context.SetAttribute("attr1", "value1");
		context.SetAttribute("attr2", "value2");
		context.SetAttribute("attr3", "value3");

		// Assert
		context.Attributes.Count.ShouldBe(3);
	}

	[Fact]
	public void SetAttribute_WithNullValue_SetsNullValue()
	{
		// Arrange
		var context = new GooglePubSubMappingContext();

		// Act
		context.SetAttribute("attr", null!);

		// Assert
		context.Attributes.ShouldContainKey("attr");
		context.Attributes["attr"].ShouldBeNull();
	}

	#endregion

	#region ApplyTo Tests

	[Fact]
	public void ApplyTo_WithNullContext_ThrowsArgumentNullException()
	{
		// Arrange
		var context = new GooglePubSubMappingContext();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => context.ApplyTo(null!));
	}

	[Fact]
	public void ApplyTo_AppliesAttributesToTransportContext()
	{
		// Arrange
		var mappingContext = new GooglePubSubMappingContext();
		mappingContext.SetAttribute("CustomAttr", "custom-value");
		var messageContext = new TransportMessageContext();

		// Act
		mappingContext.ApplyTo(messageContext);

		// Assert
		messageContext.GetTransportProperty<string>("gcp.pubsub.CustomAttr").ShouldBe("custom-value");
	}

	[Fact]
	public void ApplyTo_AppliesMultipleAttributes()
	{
		// Arrange
		var mappingContext = new GooglePubSubMappingContext();
		mappingContext.SetAttribute("Attr1", "value1");
		mappingContext.SetAttribute("Attr2", "value2");
		var messageContext = new TransportMessageContext();

		// Act
		mappingContext.ApplyTo(messageContext);

		// Assert
		messageContext.GetTransportProperty<string>("gcp.pubsub.Attr1").ShouldBe("value1");
		messageContext.GetTransportProperty<string>("gcp.pubsub.Attr2").ShouldBe("value2");
	}

	[Fact]
	public void ApplyTo_WithNoAttributes_DoesNothing()
	{
		// Arrange
		var mappingContext = new GooglePubSubMappingContext();
		var messageContext = new TransportMessageContext();

		// Act
		mappingContext.ApplyTo(messageContext);

		// Assert
		messageContext.GetAllTransportProperties().ShouldBeEmpty();
	}

	#endregion

	#region Interface Implementation Tests

	[Fact]
	public void ImplementsIGooglePubSubMappingContext()
	{
		// Arrange & Act
		var context = new GooglePubSubMappingContext();

		// Assert
		_ = context.ShouldBeAssignableTo<IGooglePubSubMappingContext>();
	}

	#endregion
}
