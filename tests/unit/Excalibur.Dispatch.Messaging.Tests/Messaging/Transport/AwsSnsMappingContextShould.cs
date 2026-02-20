// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Transport;
using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Tests.Messaging.Transport;

/// <summary>
/// Unit tests for <see cref="AwsSnsMappingContext"/>.
/// </summary>
/// <remarks>
/// Tests the AWS SNS mapping context implementation.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
[Trait("Priority", "0")]
public sealed class AwsSnsMappingContextShould
{
	#region Default Values Tests

	[Fact]
	public void Default_HasNullTopicArn()
	{
		// Arrange & Act
		var context = new AwsSnsMappingContext();

		// Assert
		context.TopicArn.ShouldBeNull();
	}

	[Fact]
	public void Default_HasNullMessageGroupId()
	{
		// Arrange & Act
		var context = new AwsSnsMappingContext();

		// Assert
		context.MessageGroupId.ShouldBeNull();
	}

	[Fact]
	public void Default_HasNullMessageDeduplicationId()
	{
		// Arrange & Act
		var context = new AwsSnsMappingContext();

		// Assert
		context.MessageDeduplicationId.ShouldBeNull();
	}

	[Fact]
	public void Default_HasNullSubject()
	{
		// Arrange & Act
		var context = new AwsSnsMappingContext();

		// Assert
		context.Subject.ShouldBeNull();
	}

	[Fact]
	public void Default_HasEmptyAttributes()
	{
		// Arrange & Act
		var context = new AwsSnsMappingContext();

		// Assert
		context.Attributes.ShouldBeEmpty();
	}

	#endregion

	#region Property Setter Tests

	[Fact]
	public void TopicArn_CanBeSet()
	{
		// Arrange
		var context = new AwsSnsMappingContext();

		// Act
		context.TopicArn = "arn:aws:sns:us-east-1:123456789012:my-topic";

		// Assert
		context.TopicArn.ShouldBe("arn:aws:sns:us-east-1:123456789012:my-topic");
	}

	[Fact]
	public void MessageGroupId_CanBeSet()
	{
		// Arrange
		var context = new AwsSnsMappingContext();

		// Act
		context.MessageGroupId = "group-123";

		// Assert
		context.MessageGroupId.ShouldBe("group-123");
	}

	[Fact]
	public void MessageDeduplicationId_CanBeSet()
	{
		// Arrange
		var context = new AwsSnsMappingContext();

		// Act
		context.MessageDeduplicationId = "dedup-456";

		// Assert
		context.MessageDeduplicationId.ShouldBe("dedup-456");
	}

	[Fact]
	public void Subject_CanBeSet()
	{
		// Arrange
		var context = new AwsSnsMappingContext();

		// Act
		context.Subject = "Order Created";

		// Assert
		context.Subject.ShouldBe("Order Created");
	}

	#endregion

	#region SetAttribute Tests

	[Fact]
	public void SetAttribute_AddsAttribute()
	{
		// Arrange
		var context = new AwsSnsMappingContext();

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
		var context = new AwsSnsMappingContext();

		// Act
		context.SetAttribute("NumberAttribute", "42", "Number");

		// Assert
		context.Attributes["NumberAttribute"].Value.ShouldBe("42");
		context.Attributes["NumberAttribute"].DataType.ShouldBe("Number");
	}

	[Fact]
	public void SetAttribute_WithSameKey_OverwritesValue()
	{
		// Arrange
		var context = new AwsSnsMappingContext();
		context.SetAttribute("attr", "value1");

		// Act
		context.SetAttribute("attr", "value2");

		// Assert
		context.Attributes["attr"].Value.ShouldBe("value2");
		context.Attributes.Count.ShouldBe(1);
	}

	[Fact]
	public void SetAttribute_IsCaseInsensitive()
	{
		// Arrange
		var context = new AwsSnsMappingContext();
		context.SetAttribute("Attr", "value1");

		// Act
		context.SetAttribute("attr", "value2");

		// Assert
		context.Attributes.Count.ShouldBe(1);
	}

	[Fact]
	public void SetAttribute_WithNullName_ThrowsArgumentException()
	{
		// Arrange
		var context = new AwsSnsMappingContext();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => context.SetAttribute(null!, "value"));
	}

	[Fact]
	public void SetAttribute_WithEmptyName_ThrowsArgumentException()
	{
		// Arrange
		var context = new AwsSnsMappingContext();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => context.SetAttribute(string.Empty, "value"));
	}

	[Fact]
	public void SetAttribute_WithWhitespaceName_ThrowsArgumentException()
	{
		// Arrange
		var context = new AwsSnsMappingContext();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => context.SetAttribute("   ", "value"));
	}

	[Fact]
	public void SetAttribute_CanAddMultipleAttributes()
	{
		// Arrange
		var context = new AwsSnsMappingContext();

		// Act
		context.SetAttribute("attr1", "value1", "String");
		context.SetAttribute("attr2", "42", "Number");
		context.SetAttribute("attr3", "binary-data", "Binary");

		// Assert
		context.Attributes.Count.ShouldBe(3);
	}

	#endregion

	#region ApplyTo Tests

	[Fact]
	public void ApplyTo_WithNullContext_ThrowsArgumentNullException()
	{
		// Arrange
		var context = new AwsSnsMappingContext();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => context.ApplyTo(null!));
	}

	[Fact]
	public void ApplyTo_AppliesAttributesToTransportContext()
	{
		// Arrange
		var mappingContext = new AwsSnsMappingContext();
		mappingContext.SetAttribute("CustomAttr", "custom-value");
		var messageContext = new TransportMessageContext();

		// Act
		mappingContext.ApplyTo(messageContext);

		// Assert
		messageContext.GetTransportProperty<string>("aws.sns.CustomAttr").ShouldBe("custom-value");
	}

	[Fact]
	public void ApplyTo_AppliesMultipleAttributes()
	{
		// Arrange
		var mappingContext = new AwsSnsMappingContext();
		mappingContext.SetAttribute("Attr1", "value1");
		mappingContext.SetAttribute("Attr2", "value2");
		var messageContext = new TransportMessageContext();

		// Act
		mappingContext.ApplyTo(messageContext);

		// Assert
		messageContext.GetTransportProperty<string>("aws.sns.Attr1").ShouldBe("value1");
		messageContext.GetTransportProperty<string>("aws.sns.Attr2").ShouldBe("value2");
	}

	[Fact]
	public void ApplyTo_WithNoAttributes_DoesNothing()
	{
		// Arrange
		var mappingContext = new AwsSnsMappingContext();
		var messageContext = new TransportMessageContext();

		// Act
		mappingContext.ApplyTo(messageContext);

		// Assert
		messageContext.GetAllTransportProperties().ShouldBeEmpty();
	}

	#endregion

	#region Interface Implementation Tests

	[Fact]
	public void ImplementsIAwsSnsMappingContext()
	{
		// Arrange & Act
		var context = new AwsSnsMappingContext();

		// Assert
		_ = context.ShouldBeAssignableTo<IAwsSnsMappingContext>();
	}

	#endregion
}
