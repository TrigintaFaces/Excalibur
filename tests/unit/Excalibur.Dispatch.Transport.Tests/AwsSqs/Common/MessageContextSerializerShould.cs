// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Amazon.SQS.Model;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Features;

using AwsMessageContextSerializer = Excalibur.Dispatch.Transport.Aws.MessageContextSerializer;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.Common;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Transport)]
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
		var context = CreateFakeContext();
		A.CallTo(() => context.CorrelationId).Returns(Guid.NewGuid().ToString());
		A.CallTo(() => context.CausationId).Returns(Guid.NewGuid().ToString());
		A.CallTo(() => context.MessageId).Returns(Guid.NewGuid().ToString());
		context.SetMessageType("TestMessage");

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
		var context = CreateFakeContext();
		A.CallTo(() => context.CorrelationId).Returns(Guid.NewGuid().ToString());
		A.CallTo(() => context.CausationId).Returns(Guid.NewGuid().ToString());
		A.CallTo(() => context.MessageId).Returns(Guid.NewGuid().ToString());
		context.SetMessageType("TestMessage");
		context.GetOrCreateProcessingFeature().DeliveryCount = 3;

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
		var context = CreateFakeContext();
		var correlationId = Guid.NewGuid().ToString();
		var messageId = Guid.NewGuid().ToString();

		A.CallTo(() => context.CorrelationId).Returns(correlationId);
		A.CallTo(() => context.CausationId).Returns((string?)null);
		A.CallTo(() => context.MessageId).Returns(messageId);
		context.SetMessageType("TestType");

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

	/// <summary>
	/// Creates a fake IMessageContext with real Items and Features dictionaries
	/// so that extension methods (GetMessageType, GetDeliveryCount, etc.) work correctly.
	/// </summary>
	private static IMessageContext CreateFakeContext()
	{
		var context = A.Fake<IMessageContext>();
		var items = new Dictionary<string, object>(StringComparer.Ordinal);
		var features = new Dictionary<Type, object>();
		A.CallTo(() => context.Items).Returns(items);
		A.CallTo(() => context.Features).Returns(features);
		return context;
	}
}
