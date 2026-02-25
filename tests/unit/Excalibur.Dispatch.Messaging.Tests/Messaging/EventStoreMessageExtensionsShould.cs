// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Messaging;
using Excalibur.Dispatch.Messaging;

using FakeItEasy;

namespace Excalibur.Dispatch.Tests.Messaging;

/// <summary>
/// Unit tests for <see cref="EventStoreMessageExtensions"/>.
/// </summary>
/// <remarks>
/// Tests the event store message extension methods.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Messaging")]
[Trait("Priority", "0")]
public sealed class EventStoreMessageExtensionsShould
{
	#region ToEvent Tests

	[Fact]
	public void ToEvent_WithNullMessage_ThrowsArgumentNullException()
	{
		// Arrange
		IEventStoreMessage<Guid>? message = null;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => message.ToEvent());
	}

	[Fact]
	public void ToEvent_WithValidMessage_ReturnsEventBody()
	{
		// Arrange
		const string eventBody = "{\"data\":\"test\"}";
		var message = A.Fake<IEventStoreMessage<Guid>>();
		_ = A.CallTo(() => message.EventBody).Returns(eventBody);

		// Act
		var result = message.ToEvent();

		// Assert
		result.ShouldBe(eventBody);
	}

	[Fact]
	public void ToEvent_WithStringKeyMessage_ReturnsEventBody()
	{
		// Arrange
		const string eventBody = "{\"key\":\"value\"}";
		var message = A.Fake<IEventStoreMessage<string>>();
		_ = A.CallTo(() => message.EventBody).Returns(eventBody);

		// Act
		var result = message.ToEvent();

		// Assert
		result.ShouldBe(eventBody);
	}

	[Fact]
	public void ToEvent_WithIntKeyMessage_ReturnsEventBody()
	{
		// Arrange
		const string eventBody = "{\"id\":123}";
		var message = A.Fake<IEventStoreMessage<int>>();
		_ = A.CallTo(() => message.EventBody).Returns(eventBody);

		// Act
		var result = message.ToEvent();

		// Assert
		result.ShouldBe(eventBody);
	}

	[Fact]
	public void ToEvent_WithEmptyEventBody_ReturnsEmptyString()
	{
		// Arrange
		var message = A.Fake<IEventStoreMessage<Guid>>();
		_ = A.CallTo(() => message.EventBody).Returns(string.Empty);

		// Act
		var result = message.ToEvent();

		// Assert
		result.ShouldBe(string.Empty);
	}

	[Fact]
	public void ToEvent_WithJsonEventBody_ReturnsJsonString()
	{
		// Arrange
		const string eventBody = "{\"orderId\":\"12345\",\"customerName\":\"Test Customer\",\"amount\":100.50}";
		var message = A.Fake<IEventStoreMessage<Guid>>();
		_ = A.CallTo(() => message.EventBody).Returns(eventBody);

		// Act
		var result = message.ToEvent();

		// Assert
		result.ShouldBe(eventBody);
	}

	[Fact]
	public void ToEvent_WithLongKeyMessage_ReturnsEventBody()
	{
		// Arrange
		const string eventBody = "{\"items\":[1,2,3,4,5]}";
		var message = A.Fake<IEventStoreMessage<long>>();
		_ = A.CallTo(() => message.EventBody).Returns(eventBody);

		// Act
		var result = message.ToEvent();

		// Assert
		result.ShouldBe(eventBody);
	}

	[Fact]
	public void ToEvent_PreservesEventBodyReference()
	{
		// Arrange
		const string eventBody = "{\"reference\":\"test\"}";
		var message = A.Fake<IEventStoreMessage<Guid>>();
		_ = A.CallTo(() => message.EventBody).Returns(eventBody);

		// Act
		var result1 = message.ToEvent();
		var result2 = message.ToEvent();

		// Assert - Both should be equal strings
		result1.ShouldBe(result2);
	}

	[Fact]
	public void ToEvent_WithDifferentKeyTypes_WorksCorrectly()
	{
		// Arrange
		const string eventBody = "{\"multi\":\"test\"}";

		var guidMessage = A.Fake<IEventStoreMessage<Guid>>();
		var stringMessage = A.Fake<IEventStoreMessage<string>>();
		var intMessage = A.Fake<IEventStoreMessage<int>>();
		var longMessage = A.Fake<IEventStoreMessage<long>>();

		_ = A.CallTo(() => guidMessage.EventBody).Returns(eventBody);
		_ = A.CallTo(() => stringMessage.EventBody).Returns(eventBody);
		_ = A.CallTo(() => intMessage.EventBody).Returns(eventBody);
		_ = A.CallTo(() => longMessage.EventBody).Returns(eventBody);

		// Act & Assert
		guidMessage.ToEvent().ShouldBe(eventBody);
		stringMessage.ToEvent().ShouldBe(eventBody);
		intMessage.ToEvent().ShouldBe(eventBody);
		longMessage.ToEvent().ShouldBe(eventBody);
	}

	[Fact]
	public void ToEvent_ReturnsObjectType()
	{
		// Arrange
		const string eventBody = "{\"test\":true}";
		var message = A.Fake<IEventStoreMessage<Guid>>();
		_ = A.CallTo(() => message.EventBody).Returns(eventBody);

		// Act
		var result = message.ToEvent();

		// Assert
		_ = result.ShouldBeOfType<string>();
	}

	#endregion
}
