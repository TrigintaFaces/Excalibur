using Excalibur.Dispatch.Exceptions;

namespace Excalibur.Dispatch.Tests.Messaging.Exceptions;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ExceptionFactoryShould
{
	[Fact]
	public void CreateConfigurationExceptionWithConfigKey()
	{
		var ex = ExceptionFactory.Configuration("Missing key", "ConnectionString");

		ex.ShouldBeOfType<ConfigurationException>();
		ex.Message.ShouldBe("Missing key");
		ex.Context["configKey"].ShouldBe("ConnectionString");
	}

	[Fact]
	public void CreateConfigurationExceptionWithoutConfigKey()
	{
		var ex = ExceptionFactory.Configuration("Generic config error");

		ex.ShouldBeOfType<ConfigurationException>();
		ex.Context.ShouldNotContainKey("configKey");
	}

	[Fact]
	public void CreateValidationExceptionWithErrors()
	{
		var errors = new Dictionary<string, string[]>
		{
			["Name"] = ["Name is required"],
		};

		var ex = ExceptionFactory.Validation(errors);

		ex.ShouldBeOfType<ValidationException>();
	}

	[Fact]
	public void CreateValidationExceptionWithSingleError()
	{
		var ex = ExceptionFactory.Validation("Email", "Invalid format");

		ex.ShouldBeOfType<ValidationException>();
	}

	[Fact]
	public void CreateMessagingExceptionWithMessageId()
	{
		var ex = ExceptionFactory.Messaging("Send failed", "msg-123");

		ex.ShouldBeOfType<MessagingException>();
		ex.Message.ShouldBe("Send failed");
		ex.MessageId.ShouldBe("msg-123");
		ex.Context["messageId"].ShouldBe("msg-123");
	}

	[Fact]
	public void CreateMessagingExceptionWithoutMessageId()
	{
		var ex = ExceptionFactory.Messaging("Generic messaging error");

		ex.ShouldBeOfType<MessagingException>();
		ex.MessageId.ShouldBeNull();
	}

	[Fact]
	public void CreateSerializationExceptionWithTargetType()
	{
		var ex = ExceptionFactory.Serialization("Failed to serialize", typeof(string));

		ex.ShouldBeOfType<DispatchSerializationException>();
		ex.Message.ShouldBe("Failed to serialize");
		ex.Context["targetType"].ShouldBe(typeof(string).FullName);
	}

	[Fact]
	public void CreateSerializationExceptionWithoutTargetType()
	{
		var ex = ExceptionFactory.Serialization("Generic serialization error");

		ex.ShouldBeOfType<DispatchSerializationException>();
		ex.Context.ShouldNotContainKey("targetType");
	}

	[Fact]
	public void CreateResourceNotFoundException()
	{
		var ex = ExceptionFactory.ResourceNotFound("Order", "order-123");

		ex.ShouldBeOfType<DispatchException>();
		ex.Message.ShouldContain("Order");
		ex.Message.ShouldContain("order-123");
		ex.DispatchStatusCode.ShouldBe(404);
		ex.Context["resourceType"].ShouldBe("Order");
		ex.Context["resourceId"].ShouldBe("order-123");
	}

	[Fact]
	public void CreateResourceNotFoundWithoutId()
	{
		var ex = ExceptionFactory.ResourceNotFound("Customer");

		ex.Message.ShouldContain("Customer");
		ex.Message.ShouldContain("was not found");
	}

	[Fact]
	public void CreateTimeoutException()
	{
		var timeout = TimeSpan.FromSeconds(30);
		var ex = ExceptionFactory.Timeout("ProcessOrder", timeout);

		ex.DispatchStatusCode.ShouldBe(408);
		ex.Message.ShouldContain("ProcessOrder");
		ex.Message.ShouldContain("30");
		ex.Context["operation"].ShouldBe("ProcessOrder");
		ex.Context["timeoutSeconds"].ShouldBe(30.0);
	}

	[Fact]
	public void CreateUnauthorizedException()
	{
		var ex = ExceptionFactory.Unauthorized("Invalid token");

		ex.DispatchStatusCode.ShouldBe(401);
		ex.Message.ShouldBe("Invalid token");
	}

	[Fact]
	public void CreateUnauthorizedExceptionWithDefaultMessage()
	{
		var ex = ExceptionFactory.Unauthorized();

		ex.DispatchStatusCode.ShouldBe(401);
		ex.Message.ShouldContain("Authentication is required");
	}

	[Fact]
	public void CreateForbiddenException()
	{
		var ex = ExceptionFactory.Forbidden("Insufficient role");

		ex.DispatchStatusCode.ShouldBe(403);
		ex.Message.ShouldBe("Insufficient role");
	}

	[Fact]
	public void CreateForbiddenExceptionWithDefaultMessage()
	{
		var ex = ExceptionFactory.Forbidden();

		ex.DispatchStatusCode.ShouldBe(403);
		ex.Message.ShouldContain("permission");
	}

	[Fact]
	public void CreateCircuitBreakerOpenException()
	{
		var ex = ExceptionFactory.CircuitBreakerOpen("PaymentService", TimeSpan.FromSeconds(60));

		ex.DispatchStatusCode.ShouldBe(503);
		ex.Message.ShouldContain("PaymentService");
		ex.Message.ShouldContain("60");
		ex.Context["serviceName"].ShouldBe("PaymentService");
	}

	[Fact]
	public void CreateCircuitBreakerOpenExceptionWithoutRetryAfter()
	{
		var ex = ExceptionFactory.CircuitBreakerOpen("OrderService");

		ex.DispatchStatusCode.ShouldBe(503);
		ex.Message.ShouldContain("OrderService");
		ex.Message.ShouldNotContain("Retry after");
	}

	[Fact]
	public void CreateConcurrencyConflictException()
	{
		var ex = ExceptionFactory.ConcurrencyConflict("Order", "order-456");

		ex.DispatchStatusCode.ShouldBe(409);
		ex.Message.ShouldContain("Order");
		ex.Message.ShouldContain("order-456");
		ex.Context["resourceType"].ShouldBe("Order");
	}

	[Fact]
	public void CreateConcurrencyConflictExceptionWithoutId()
	{
		var ex = ExceptionFactory.ConcurrencyConflict("Aggregate");

		ex.Message.ShouldContain("Aggregate");
		ex.Message.ShouldNotContain("ID");
	}

	[Fact]
	public void WrapException()
	{
		var inner = new IOException("disk full");
		var ex = ExceptionFactory.Wrap(inner, "Failed to persist", "SYS002");

		ex.InnerException.ShouldBe(inner);
		ex.Message.ShouldBe("Failed to persist");
		ex.ErrorCode.ShouldBe("SYS002");
	}

	[Fact]
	public void WrapExceptionWithDefaultErrorCode()
	{
		var inner = new InvalidOperationException("oops");
		var ex = ExceptionFactory.Wrap(inner, "Something failed");

		ex.ErrorCode.ShouldBe(ErrorCodes.UnknownError);
	}
}
