// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Amazon.SQS.Model;

using Excalibur.Dispatch.Abstractions;

using AwsMessageContextSerializer = Excalibur.Dispatch.Transport.Aws.MessageContextSerializer;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.Common;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class MessageContextSerializerShould
{
	[Fact]
	public void ThrowWhenContextIsNull()
	{
		// Arrange
		var attrs = new Dictionary<string, MessageAttributeValue>();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			AwsMessageContextSerializer.SerializeToMessageAttributes(null!, attrs));
	}

	[Fact]
	public void ThrowWhenMessageAttributesDictionaryIsNull()
	{
		// Arrange
		var context = A.Fake<IMessageContext>();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			AwsMessageContextSerializer.SerializeToMessageAttributes(context, null!));
	}

	[Fact]
	public void ThrowWhenDeserializeMessageAttributesIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			AwsMessageContextSerializer.DeserializeFromMessageAttributes(null!, A.Fake<IServiceProvider>()));
	}

	[Fact]
	public void ThrowWhenDeserializeServiceProviderIsNull()
	{
		// Arrange
		var attrs = new Dictionary<string, MessageAttributeValue>();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			AwsMessageContextSerializer.DeserializeFromMessageAttributes(attrs, null!));
	}

	[Fact]
	public void ThrowWhenDeserializingEmptyAttributes()
	{
		// Arrange — empty attributes are missing required MessageId field
		var attrs = new Dictionary<string, MessageAttributeValue>();
		var sp = A.Fake<IServiceProvider>();

		// Act & Assert
		Should.Throw<InvalidOperationException>(() =>
			AwsMessageContextSerializer.DeserializeFromMessageAttributes(attrs, sp));
	}

	[Fact]
	public void SerializeContextToMessageAttributes()
	{
		// Arrange
		var context = A.Fake<IMessageContext>();
		var items = new Dictionary<string, object>(StringComparer.Ordinal)
		{
			["CorrelationId"] = Guid.NewGuid().ToString(),
		};
		A.CallTo(() => context.Items).Returns(items);
		A.CallTo(() => context.CorrelationId).Returns(Guid.NewGuid().ToString());
		A.CallTo(() => context.CausationId).Returns(Guid.NewGuid().ToString());
		A.CallTo(() => context.MessageId).Returns(Guid.NewGuid().ToString());
		A.CallTo(() => context.MessageType).Returns("TestMessage");
		A.CallTo(() => context.DeliveryCount).Returns(0);

		var attrs = new Dictionary<string, MessageAttributeValue>();

		// Act
		AwsMessageContextSerializer.SerializeToMessageAttributes(context, attrs);

		// Assert — should populate attributes from the serialized context
		attrs.ShouldNotBeEmpty();
	}

	[Fact]
	public void UseNumberDataTypeForNumericKeys()
	{
		// Arrange
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.Items).Returns(new Dictionary<string, object>(StringComparer.Ordinal));
		A.CallTo(() => context.CorrelationId).Returns(Guid.NewGuid().ToString());
		A.CallTo(() => context.CausationId).Returns(Guid.NewGuid().ToString());
		A.CallTo(() => context.MessageId).Returns(Guid.NewGuid().ToString());
		A.CallTo(() => context.MessageType).Returns("TestMessage");
		A.CallTo(() => context.DeliveryCount).Returns(3);

		var attrs = new Dictionary<string, MessageAttributeValue>();

		// Act
		AwsMessageContextSerializer.SerializeToMessageAttributes(context, attrs);

		// Assert — delivery count key should use Number data type
		if (attrs.TryGetValue("X-DeliveryCount", out var deliveryAttr))
		{
			deliveryAttr.DataType.ShouldBe("Number");
		}
	}

	[Fact]
	public void RoundTripSerializeDeserialize()
	{
		// Arrange
		var context = A.Fake<IMessageContext>();
		var correlationId = Guid.NewGuid().ToString();
		var messageId = Guid.NewGuid().ToString();

		A.CallTo(() => context.Items).Returns(new Dictionary<string, object>(StringComparer.Ordinal));
		A.CallTo(() => context.CorrelationId).Returns(correlationId);
		A.CallTo(() => context.CausationId).Returns((string?)null);
		A.CallTo(() => context.MessageId).Returns(messageId);
		A.CallTo(() => context.MessageType).Returns("TestType");
		A.CallTo(() => context.DeliveryCount).Returns(0);

		var attrs = new Dictionary<string, MessageAttributeValue>();
		AwsMessageContextSerializer.SerializeToMessageAttributes(context, attrs);

		var sp = A.Fake<IServiceProvider>();

		// Act
		var deserialized = AwsMessageContextSerializer.DeserializeFromMessageAttributes(attrs, sp);

		// Assert
		deserialized.ShouldNotBeNull();
		deserialized.CorrelationId.ShouldBe(correlationId);
		deserialized.MessageId.ShouldBe(messageId);
	}
}
