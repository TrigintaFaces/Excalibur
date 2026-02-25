using Excalibur.Dispatch.Exceptions;

namespace Excalibur.Dispatch.Tests.Messaging.Exceptions;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class MessagingExceptionShould
{
	[Fact]
	public void CreateWithDefaultConstructor()
	{
		var ex = new MessagingException();

		ex.ErrorCode.ShouldBe(ErrorCodes.MessageSendFailed);
		ex.Category.ShouldBe(ErrorCategory.Messaging);
	}

	[Fact]
	public void CreateWithMessage()
	{
		var ex = new MessagingException("Send failed");

		ex.Message.ShouldBe("Send failed");
		ex.ErrorCode.ShouldBe(ErrorCodes.MessageSendFailed);
	}

	[Fact]
	public void CreateWithMessageAndInnerException()
	{
		var inner = new TimeoutException("timeout");
		var ex = new MessagingException("Send failed", inner);

		ex.Message.ShouldBe("Send failed");
		ex.InnerException.ShouldBe(inner);
	}

	[Fact]
	public void CreateWithExplicitErrorCode()
	{
		var ex = new MessagingException(ErrorCodes.MessageRoutingFailed, "Routing error");

		ex.ErrorCode.ShouldBe(ErrorCodes.MessageRoutingFailed);
		ex.Message.ShouldBe("Routing error");
	}

	[Fact]
	public void CreateWithExplicitErrorCodeAndInner()
	{
		var inner = new InvalidOperationException("inner");
		var ex = new MessagingException(ErrorCodes.MessageQueueUnavailable, "Queue down", inner);

		ex.ErrorCode.ShouldBe(ErrorCodes.MessageQueueUnavailable);
		ex.InnerException.ShouldBe(inner);
	}

	[Fact]
	public void SupportMessageIdProperty()
	{
		var ex = new MessagingException("test") { MessageId = "msg-123" };

		ex.MessageId.ShouldBe("msg-123");
	}

	[Fact]
	public void SupportMessageTypeProperty()
	{
		var ex = new MessagingException("test") { MessageType = "OrderCreated" };

		ex.MessageType.ShouldBe("OrderCreated");
	}

	[Fact]
	public void SupportQueueNameProperty()
	{
		var ex = new MessagingException("test") { QueueName = "orders-queue" };

		ex.QueueName.ShouldBe("orders-queue");
	}

	[Fact]
	public void CreateHandlerNotFoundExceptionFromType()
	{
		var ex = MessagingException.HandlerNotFound(typeof(string));

		ex.ShouldBeOfType<MessagingException>();
		ex.Message.ShouldContain("String");
		ex.MessageType.ShouldBe(typeof(string).FullName);
		ex.DispatchStatusCode.ShouldBe(500);
	}

	[Fact]
	public void CreateRoutingFailedException()
	{
		var ex = MessagingException.RoutingFailed("msg-456", "No route found");

		ex.ShouldBeOfType<MessagingException>();
		ex.Message.ShouldContain("msg-456");
		ex.Message.ShouldContain("No route found");
		ex.MessageId.ShouldBe("msg-456");
	}

	[Fact]
	public void CreateDuplicateMessageException()
	{
		var ex = MessagingException.DuplicateMessage("msg-789");

		ex.ShouldBeOfType<MessagingException>();
		ex.Message.ShouldContain("msg-789");
		ex.MessageId.ShouldBe("msg-789");
		ex.DispatchStatusCode.ShouldBe(409);
	}

	[Fact]
	public void CreateRetryLimitExceededException()
	{
		var ex = MessagingException.RetryLimitExceeded("msg-101", 5);

		ex.ShouldBeOfType<MessagingException>();
		ex.Message.ShouldContain("msg-101");
		ex.Message.ShouldContain("5");
		ex.MessageId.ShouldBe("msg-101");
	}

	[Fact]
	public void CreateBrokerConnectionFailedException()
	{
		var inner = new IOException("Connection refused");
		var ex = MessagingException.BrokerConnectionFailed("localhost:5672", inner);

		ex.ShouldBeOfType<MessagingException>();
		ex.Message.ShouldContain("localhost:5672");
		ex.InnerException.ShouldBe(inner);
		ex.DispatchStatusCode.ShouldBe(503);
	}

	[Fact]
	public void CreateBrokerConnectionFailedExceptionWithoutInner()
	{
		var ex = MessagingException.BrokerConnectionFailed("rabbitmq:5672");

		ex.ShouldBeOfType<MessagingException>();
		ex.InnerException.ShouldNotBeNull();
	}
}
