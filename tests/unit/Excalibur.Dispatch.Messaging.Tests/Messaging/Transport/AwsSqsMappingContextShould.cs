// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Transport;
using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Tests.Messaging.Transport;

/// <summary>
/// Unit tests for <see cref="AwsSqsMappingContext"/>.
/// </summary>
/// <remarks>
/// Tests the AWS SQS mapping context implementation.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
[Trait("Priority", "0")]
public sealed class AwsSqsMappingContextShould
{
	#region Default Values Tests

	[Fact]
	public void Default_HasNullQueueUrl()
	{
		// Arrange & Act
		var context = new AwsSqsMappingContext();

		// Assert
		context.QueueUrl.ShouldBeNull();
	}

	[Fact]
	public void Default_HasNullMessageGroupId()
	{
		// Arrange & Act
		var context = new AwsSqsMappingContext();

		// Assert
		context.MessageGroupId.ShouldBeNull();
	}

	[Fact]
	public void Default_HasNullMessageDeduplicationId()
	{
		// Arrange & Act
		var context = new AwsSqsMappingContext();

		// Assert
		context.MessageDeduplicationId.ShouldBeNull();
	}

	[Fact]
	public void Default_HasNullDelaySeconds()
	{
		// Arrange & Act
		var context = new AwsSqsMappingContext();

		// Assert
		context.DelaySeconds.ShouldBeNull();
	}

	[Fact]
	public void Default_HasEmptyAttributes()
	{
		// Arrange & Act
		var context = new AwsSqsMappingContext();

		// Assert
		context.Attributes.ShouldBeEmpty();
	}

	#endregion

	#region Property Setter Tests

	[Fact]
	public void QueueUrl_CanBeSet()
	{
		// Arrange
		var context = new AwsSqsMappingContext();

		// Act
		context.QueueUrl = "https://sqs.us-east-1.amazonaws.com/123456789012/my-queue";

		// Assert
		context.QueueUrl.ShouldBe("https://sqs.us-east-1.amazonaws.com/123456789012/my-queue");
	}

	[Fact]
	public void MessageGroupId_CanBeSet()
	{
		// Arrange
		var context = new AwsSqsMappingContext();

		// Act
		context.MessageGroupId = "group-abc";

		// Assert
		context.MessageGroupId.ShouldBe("group-abc");
	}

	[Fact]
	public void MessageDeduplicationId_CanBeSet()
	{
		// Arrange
		var context = new AwsSqsMappingContext();

		// Act
		context.MessageDeduplicationId = "dedup-xyz";

		// Assert
		context.MessageDeduplicationId.ShouldBe("dedup-xyz");
	}

	[Fact]
	public void DelaySeconds_CanBeSet()
	{
		// Arrange
		var context = new AwsSqsMappingContext();

		// Act
		context.DelaySeconds = 60;

		// Assert
		context.DelaySeconds.ShouldBe(60);
	}

	[Fact]
	public void DelaySeconds_CanBeSetToZero()
	{
		// Arrange
		var context = new AwsSqsMappingContext();

		// Act
		context.DelaySeconds = 0;

		// Assert
		context.DelaySeconds.ShouldBe(0);
	}

	[Fact]
	public void DelaySeconds_CanBeSetToMaxValue()
	{
		// Arrange
		var context = new AwsSqsMappingContext();

		// Act - SQS max delay is 900 seconds
		context.DelaySeconds = 900;

		// Assert
		context.DelaySeconds.ShouldBe(900);
	}

	#endregion

	#region SetAttribute Tests

	[Fact]
	public void SetAttribute_AddsAttribute()
	{
		// Arrange
		var context = new AwsSqsMappingContext();

		// Act
		context.SetAttribute("CustomAttribute", "custom-value");

		// Assert
		context.Attributes.ShouldContainKey("CustomAttribute");
		context.Attributes["CustomAttribute"].Value.ShouldBe("custom-value");
		context.Attributes["CustomAttribute"].DataType.ShouldBe("String");
	}

	[Fact]
	public void SetAttribute_WithCustomDataType_SetsDataType()
	{
		// Arrange
		var context = new AwsSqsMappingContext();

		// Act
		context.SetAttribute("NumberAttribute", "100", "Number");

		// Assert
		context.Attributes["NumberAttribute"].Value.ShouldBe("100");
		context.Attributes["NumberAttribute"].DataType.ShouldBe("Number");
	}

	[Fact]
	public void SetAttribute_WithSameKey_OverwritesValue()
	{
		// Arrange
		var context = new AwsSqsMappingContext();
		context.SetAttribute("attr", "original");

		// Act
		context.SetAttribute("attr", "updated");

		// Assert
		context.Attributes["attr"].Value.ShouldBe("updated");
		context.Attributes.Count.ShouldBe(1);
	}

	[Fact]
	public void SetAttribute_IsCaseInsensitive()
	{
		// Arrange
		var context = new AwsSqsMappingContext();
		context.SetAttribute("ATTR", "value1");

		// Act
		context.SetAttribute("attr", "value2");

		// Assert
		context.Attributes.Count.ShouldBe(1);
	}

	[Fact]
	public void SetAttribute_WithNullName_ThrowsArgumentException()
	{
		// Arrange
		var context = new AwsSqsMappingContext();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => context.SetAttribute(null!, "value"));
	}

	[Fact]
	public void SetAttribute_WithEmptyName_ThrowsArgumentException()
	{
		// Arrange
		var context = new AwsSqsMappingContext();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => context.SetAttribute(string.Empty, "value"));
	}

	[Fact]
	public void SetAttribute_WithWhitespaceName_ThrowsArgumentException()
	{
		// Arrange
		var context = new AwsSqsMappingContext();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => context.SetAttribute("   ", "value"));
	}

	[Fact]
	public void SetAttribute_CanAddMultipleAttributes()
	{
		// Arrange
		var context = new AwsSqsMappingContext();

		// Act
		context.SetAttribute("attr1", "value1");
		context.SetAttribute("attr2", "42", "Number");
		context.SetAttribute("attr3", "binary", "Binary");

		// Assert
		context.Attributes.Count.ShouldBe(3);
	}

	#endregion

	#region ApplyTo Tests

	[Fact]
	public void ApplyTo_WithNullContext_ThrowsArgumentNullException()
	{
		// Arrange
		var context = new AwsSqsMappingContext();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => context.ApplyTo(null!));
	}

	[Fact]
	public void ApplyTo_AppliesAttributesToTransportContext()
	{
		// Arrange
		var mappingContext = new AwsSqsMappingContext();
		mappingContext.SetAttribute("CustomAttr", "custom-value");
		var messageContext = new TransportMessageContext();

		// Act
		mappingContext.ApplyTo(messageContext);

		// Assert
		messageContext.GetTransportProperty<string>("aws.sqs.CustomAttr").ShouldBe("custom-value");
	}

	[Fact]
	public void ApplyTo_AppliesMultipleAttributes()
	{
		// Arrange
		var mappingContext = new AwsSqsMappingContext();
		mappingContext.SetAttribute("Attr1", "value1");
		mappingContext.SetAttribute("Attr2", "value2");
		var messageContext = new TransportMessageContext();

		// Act
		mappingContext.ApplyTo(messageContext);

		// Assert
		messageContext.GetTransportProperty<string>("aws.sqs.Attr1").ShouldBe("value1");
		messageContext.GetTransportProperty<string>("aws.sqs.Attr2").ShouldBe("value2");
	}

	[Fact]
	public void ApplyTo_WithNoAttributes_DoesNothing()
	{
		// Arrange
		var mappingContext = new AwsSqsMappingContext();
		var messageContext = new TransportMessageContext();

		// Act
		mappingContext.ApplyTo(messageContext);

		// Assert
		messageContext.GetAllTransportProperties().ShouldBeEmpty();
	}

	#endregion

	#region Interface Implementation Tests

	[Fact]
	public void ImplementsIAwsSqsMappingContext()
	{
		// Arrange & Act
		var context = new AwsSqsMappingContext();

		// Assert
		_ = context.ShouldBeAssignableTo<IAwsSqsMappingContext>();
	}

	#endregion
}
