// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Exceptions;

namespace Excalibur.Dispatch.Tests.Messaging.Exceptions;

/// <summary>
/// Unit tests for <see cref="MessagingException"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class MessagingExceptionShould
{
	[Fact]
	public void InheritFromDispatchException()
	{
		// Arrange & Act
		var exception = new MessagingException();

		// Assert
		_ = exception.ShouldBeAssignableTo<DispatchException>();
		_ = exception.ShouldBeAssignableTo<ApiException>();
		_ = exception.ShouldBeAssignableTo<Exception>();
	}

	[Fact]
	public void HaveDefaultConstructor()
	{
		// Arrange & Act
		var exception = new MessagingException();

		// Assert
		exception.ErrorCode.ShouldBe(ErrorCodes.MessageSendFailed);
		exception.Message.ShouldNotBeNullOrWhiteSpace();
	}

	[Fact]
	public void AcceptMessage()
	{
		// Arrange
		const string message = "Custom messaging error";

		// Act
		var exception = new MessagingException(message);

		// Assert
		exception.Message.ShouldBe(message);
		exception.ErrorCode.ShouldBe(ErrorCodes.MessageSendFailed);
	}

	[Fact]
	public void AcceptMessageAndInnerException()
	{
		// Arrange
		var innerException = new InvalidOperationException("Inner error");
		const string message = "Messaging error";

		// Act
		var exception = new MessagingException(message, innerException);

		// Assert
		exception.Message.ShouldBe(message);
		exception.InnerException.ShouldBe(innerException);
		exception.ErrorCode.ShouldBe(ErrorCodes.MessageSendFailed);
	}

	[Fact]
	public void AcceptErrorCodeAndMessage()
	{
		// Arrange
		const string errorCode = "CUSTOM_MESSAGING";
		const string message = "Custom messaging error";

		// Act
		var exception = new MessagingException(errorCode, message);

		// Assert
		exception.ErrorCode.ShouldBe(errorCode);
		exception.Message.ShouldBe(message);
	}

	[Fact]
	public void AcceptErrorCodeMessageAndInnerException()
	{
		// Arrange
		const string errorCode = "CUSTOM_MESSAGING";
		const string message = "Custom messaging error";
		var innerException = new ArgumentNullException("param");

		// Act
		var exception = new MessagingException(errorCode, message, innerException);

		// Assert
		exception.ErrorCode.ShouldBe(errorCode);
		exception.Message.ShouldBe(message);
		exception.InnerException.ShouldBe(innerException);
	}

	[Fact]
	public void HaveNullPropertiesByDefault()
	{
		// Arrange & Act
		var exception = new MessagingException();

		// Assert
		exception.MessageId.ShouldBeNull();
		exception.MessageType.ShouldBeNull();
		exception.QueueName.ShouldBeNull();
	}

	[Fact]
	public void AllowSettingMessageId()
	{
		// Arrange
		var exception = new MessagingException();

		// Act
		exception.MessageId = "msg-123";

		// Assert
		exception.MessageId.ShouldBe("msg-123");
	}

	[Fact]
	public void AllowSettingMessageType()
	{
		// Arrange
		var exception = new MessagingException();

		// Act
		exception.MessageType = "OrderCreated";

		// Assert
		exception.MessageType.ShouldBe("OrderCreated");
	}

	[Fact]
	public void AllowSettingQueueName()
	{
		// Arrange
		var exception = new MessagingException();

		// Act
		exception.QueueName = "orders-queue";

		// Assert
		exception.QueueName.ShouldBe("orders-queue");
	}

	[Fact]
	public void CreateHandlerNotFoundException()
	{
		// Arrange
		var messageType = typeof(TestMessage);

		// Act
		var exception = MessagingException.HandlerNotFound(messageType);

		// Assert
		exception.Message.ShouldContain("TestMessage");
		exception.Message.ShouldContain("No handler found");
		exception.MessageType.ShouldBe(messageType.FullName);
		exception.Context.ShouldContainKey("messageType");
		exception.SuggestedAction.ShouldContain("Register a handler");
		exception.DispatchStatusCode.ShouldBe(500);
		exception.Data["ErrorCode"].ShouldBe(ErrorCodes.MessageHandlerNotFound);
	}

	[Fact]
	public void CreateRoutingFailedException()
	{
		// Arrange
		const string messageId = "msg-456";
		const string reason = "Queue not found";

		// Act
		var exception = MessagingException.RoutingFailed(messageId, reason);

		// Assert
		exception.Message.ShouldContain(messageId);
		exception.Message.ShouldContain(reason);
		exception.MessageId.ShouldBe(messageId);
		exception.Context.ShouldContainKey("messageId");
		exception.Context.ShouldContainKey("reason");
		exception.SuggestedAction.ShouldContain("routing configuration");
		exception.DispatchStatusCode.ShouldBe(500);
		exception.Data["ErrorCode"].ShouldBe(ErrorCodes.MessageRoutingFailed);
	}

	[Fact]
	public void CreateDuplicateMessageException()
	{
		// Arrange
		const string messageId = "msg-789";

		// Act
		var exception = MessagingException.DuplicateMessage(messageId);

		// Assert
		exception.Message.ShouldContain(messageId);
		exception.Message.ShouldContain("Duplicate");
		exception.MessageId.ShouldBe(messageId);
		exception.Context.ShouldContainKey("messageId");
		exception.SuggestedAction.ShouldContain("already been processed");
		exception.DispatchStatusCode.ShouldBe(409); // Conflict
		exception.Data["ErrorCode"].ShouldBe(ErrorCodes.MessageDuplicate);
	}

	[Fact]
	public void CreateRetryLimitExceededException()
	{
		// Arrange
		const string messageId = "msg-101";
		const int retryCount = 5;

		// Act
		var exception = MessagingException.RetryLimitExceeded(messageId, retryCount);

		// Assert
		exception.Message.ShouldContain(messageId);
		exception.Message.ShouldContain("5");
		exception.Message.ShouldContain("exceeded");
		exception.MessageId.ShouldBe(messageId);
		exception.Context.ShouldContainKey("messageId");
		exception.Context.ShouldContainKeyAndValue("retryCount", retryCount);
		exception.SuggestedAction.ShouldContain("dead letter queue");
		exception.DispatchStatusCode.ShouldBe(500);
		exception.Data["ErrorCode"].ShouldBe(ErrorCodes.MessageRetryLimitExceeded);
	}

	[Fact]
	public void CreateBrokerConnectionFailedException()
	{
		// Arrange
		const string brokerAddress = "localhost:5672";

		// Act
		var exception = MessagingException.BrokerConnectionFailed(brokerAddress);

		// Assert
		exception.Message.ShouldContain(brokerAddress);
		exception.Message.ShouldContain("Failed to connect");
		exception.Context.ShouldContainKey("brokerAddress");
		exception.SuggestedAction.ShouldContain("network connectivity");
		exception.DispatchStatusCode.ShouldBe(503); // Service Unavailable
		exception.Data["ErrorCode"].ShouldBe(ErrorCodes.MessageBrokerConnectionFailed);
		exception.InnerException.ShouldNotBeNull();
	}

	[Fact]
	public void CreateBrokerConnectionFailedWithInnerException()
	{
		// Arrange
		const string brokerAddress = "rabbitmq.example.com:5672";
		var innerException = new TimeoutException("Connection timed out");

		// Act
		var exception = MessagingException.BrokerConnectionFailed(brokerAddress, innerException);

		// Assert
		exception.Message.ShouldContain(brokerAddress);
		exception.InnerException.ShouldBe(innerException);
		exception.DispatchStatusCode.ShouldBe(503);
	}

	[Fact]
	public void HaveSerializableAttribute()
	{
		// Assert
		typeof(MessagingException)
			.GetCustomAttributes(typeof(SerializableAttribute), false)
			.ShouldNotBeEmpty();
	}

	[Fact]
	public void BeCatchableAsDispatchException()
	{
		// Arrange
		var exception = new MessagingException("Test error");

		// Act & Assert
		try
		{
			throw exception;
		}
		catch (DispatchException caught)
		{
			caught.ShouldBe(exception);
		}
	}

	// Helper class for testing
	private sealed class TestMessage { }
}
