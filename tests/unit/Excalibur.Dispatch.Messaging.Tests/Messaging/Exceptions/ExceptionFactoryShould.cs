// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Exceptions;

namespace Excalibur.Dispatch.Tests.Messaging.Exceptions;

/// <summary>
/// Unit tests for <see cref="ExceptionFactory"/>.
/// </summary>
/// <remarks>
/// Tests the standardized exception factory methods.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Exceptions")]
[Trait("Priority", "0")]
public sealed class ExceptionFactoryShould
{
	#region Configuration Exception Tests

	[Fact]
	public void Configuration_WithMessage_CreatesConfigurationException()
	{
		// Arrange & Act
		var ex = ExceptionFactory.Configuration("Invalid configuration");

		// Assert
		_ = ex.ShouldNotBeNull();
		_ = ex.ShouldBeOfType<ConfigurationException>();
		ex.Message.ShouldBe("Invalid configuration");
	}

	[Fact]
	public void Configuration_WithMessageAndKey_AddsContextKey()
	{
		// Arrange & Act
		var ex = ExceptionFactory.Configuration("Missing value", "ConnectionString");

		// Assert
		_ = ex.ShouldNotBeNull();
		ex.Message.ShouldBe("Missing value");
		ex.Context.ShouldContainKey("configKey");
		ex.Context["configKey"].ShouldBe("ConnectionString");
	}

	[Fact]
	public void Configuration_WithNullConfigKey_DoesNotAddContext()
	{
		// Arrange & Act
		var ex = ExceptionFactory.Configuration("Invalid config", configKey: null);

		// Assert
		_ = ex.ShouldNotBeNull();
		ex.Context.ShouldNotContainKey("configKey");
	}

	#endregion

	#region Validation Exception Tests

	[Fact]
	public void Validation_WithDictionary_CreatesValidationException()
	{
		// Arrange
		var errors = new Dictionary<string, string[]>
		{
			["Email"] = ["Invalid email format"],
			["Password"] = ["Password too short", "Must contain special character"],
		};

		// Act
		var ex = ExceptionFactory.Validation(errors);

		// Assert
		_ = ex.ShouldNotBeNull();
		_ = ex.ShouldBeOfType<ValidationException>();
		ex.ValidationErrors.Count.ShouldBe(2);
		ex.ValidationErrors["Email"].ShouldContain("Invalid email format");
		ex.ValidationErrors["Password"].Length.ShouldBe(2);
	}

	[Fact]
	public void Validation_WithFieldAndMessage_CreatesSingleFieldError()
	{
		// Arrange & Act
		var ex = ExceptionFactory.Validation("Amount", "Amount must be positive");

		// Assert
		_ = ex.ShouldNotBeNull();
		ex.ValidationErrors.Count.ShouldBe(1);
		ex.ValidationErrors["Amount"].ShouldContain("Amount must be positive");
	}

	#endregion

	#region Messaging Exception Tests

	[Fact]
	public void Messaging_WithMessage_CreatesMessagingException()
	{
		// Arrange & Act
		var ex = ExceptionFactory.Messaging("Queue unavailable");

		// Assert
		_ = ex.ShouldNotBeNull();
		_ = ex.ShouldBeOfType<MessagingException>();
		ex.Message.ShouldBe("Queue unavailable");
	}

	[Fact]
	public void Messaging_WithMessageId_AddsMessageIdToContext()
	{
		// Arrange & Act
		var ex = ExceptionFactory.Messaging("Failed to publish", "msg-12345");

		// Assert
		_ = ex.ShouldNotBeNull();
		ex.MessageId.ShouldBe("msg-12345");
		ex.Context.ShouldContainKey("messageId");
		ex.Context["messageId"].ShouldBe("msg-12345");
	}

	#endregion

	#region Serialization Exception Tests

	[Fact]
	public void Serialization_WithMessage_CreatesSerializationException()
	{
		// Arrange & Act
		var ex = ExceptionFactory.Serialization("Failed to deserialize");

		// Assert
		_ = ex.ShouldNotBeNull();
		_ = ex.ShouldBeOfType<DispatchSerializationException>();
		ex.Message.ShouldBe("Failed to deserialize");
	}

	[Fact]
	public void Serialization_WithTargetType_AddsTypeToContext()
	{
		// Arrange & Act
		var ex = ExceptionFactory.Serialization("Invalid JSON", typeof(string));

		// Assert
		_ = ex.ShouldNotBeNull();
		ex.Context.ShouldContainKey("targetType");
		ex.Context["targetType"].ShouldBe("System.String");
	}

	[Fact]
	public void Serialization_WithNullTargetType_DoesNotAddContext()
	{
		// Arrange & Act
		var ex = ExceptionFactory.Serialization("Invalid data", targetType: null);

		// Assert
		_ = ex.ShouldNotBeNull();
		ex.Context.ShouldNotContainKey("targetType");
	}

	#endregion

	#region ResourceNotFound Exception Tests

	[Fact]
	public void ResourceNotFound_WithResourceType_CreatesResourceNotFoundException()
	{
		// Arrange & Act
		var ex = ExceptionFactory.ResourceNotFound("Order");

		// Assert
		_ = ex.ShouldNotBeNull();
		ex.Message.ShouldContain("Order");
		ex.Message.ShouldContain("was not found");
		ex.Context["resourceType"].ShouldBe("Order");
		ex.DispatchStatusCode.ShouldBe(404);
	}

	[Fact]
	public void ResourceNotFound_WithResourceId_IncludesIdInMessage()
	{
		// Arrange & Act
		var ex = ExceptionFactory.ResourceNotFound("Customer", "cust-123");

		// Assert
		_ = ex.ShouldNotBeNull();
		ex.Message.ShouldContain("Customer");
		ex.Message.ShouldContain("cust-123");
		ex.Context["resourceId"].ShouldBe("cust-123");
		ex.UserMessage.ShouldContain("could not be found");
	}

	#endregion

	#region Timeout Exception Tests

	[Fact]
	public void Timeout_CreatesTimeoutException()
	{
		// Arrange
		var timeout = TimeSpan.FromSeconds(30);

		// Act
		var ex = ExceptionFactory.Timeout("ProcessOrder", timeout);

		// Assert
		_ = ex.ShouldNotBeNull();
		ex.Message.ShouldContain("ProcessOrder");
		ex.Message.ShouldContain("30");
		ex.Context["operation"].ShouldBe("ProcessOrder");
		ex.Context["timeoutSeconds"].ShouldBe(30.0);
		ex.DispatchStatusCode.ShouldBe(408);
	}

	[Fact]
	public void Timeout_HasUserFriendlyMessage()
	{
		// Arrange & Act
		var ex = ExceptionFactory.Timeout("LongRunningOperation", TimeSpan.FromMinutes(5));

		// Assert
		ex.UserMessage.ShouldContain("too long");
		ex.SuggestedAction.ShouldContain("try again");
	}

	#endregion

	#region Unauthorized Exception Tests

	[Fact]
	public void Unauthorized_WithoutReason_CreatesDefaultMessage()
	{
		// Arrange & Act
		var ex = ExceptionFactory.Unauthorized();

		// Assert
		_ = ex.ShouldNotBeNull();
		ex.Message.ShouldContain("Authentication is required");
		ex.DispatchStatusCode.ShouldBe(401);
		ex.UserMessage.ShouldContain("authenticated");
	}

	[Fact]
	public void Unauthorized_WithReason_UsesCustomMessage()
	{
		// Arrange & Act
		var ex = ExceptionFactory.Unauthorized("Token expired");

		// Assert
		_ = ex.ShouldNotBeNull();
		ex.Message.ShouldBe("Token expired");
		ex.DispatchStatusCode.ShouldBe(401);
	}

	#endregion

	#region Forbidden Exception Tests

	[Fact]
	public void Forbidden_WithoutReason_CreatesDefaultMessage()
	{
		// Arrange & Act
		var ex = ExceptionFactory.Forbidden();

		// Assert
		_ = ex.ShouldNotBeNull();
		ex.Message.ShouldContain("do not have permission");
		ex.DispatchStatusCode.ShouldBe(403);
	}

	[Fact]
	public void Forbidden_WithReason_UsesCustomMessage()
	{
		// Arrange & Act
		var ex = ExceptionFactory.Forbidden("Admin access required");

		// Assert
		_ = ex.ShouldNotBeNull();
		ex.Message.ShouldBe("Admin access required");
		ex.DispatchStatusCode.ShouldBe(403);
		ex.SuggestedAction.ShouldContain("administrator");
	}

	#endregion

	#region CircuitBreakerOpen Exception Tests

	[Fact]
	public void CircuitBreakerOpen_WithServiceName_CreatesCircuitBreakerException()
	{
		// Arrange & Act
		var ex = ExceptionFactory.CircuitBreakerOpen("PaymentService");

		// Assert
		_ = ex.ShouldNotBeNull();
		ex.Message.ShouldContain("PaymentService");
		ex.Message.ShouldContain("Circuit breaker is open");
		ex.Context["serviceName"].ShouldBe("PaymentService");
		ex.DispatchStatusCode.ShouldBe(503);
	}

	[Fact]
	public void CircuitBreakerOpen_WithRetryAfter_IncludesRetryInfo()
	{
		// Arrange
		var retryAfter = TimeSpan.FromSeconds(60);

		// Act
		var ex = ExceptionFactory.CircuitBreakerOpen("OrderService", retryAfter);

		// Assert
		ex.Message.ShouldContain("60");
		ex.Context["retryAfterSeconds"].ShouldBe(60.0);
		ex.UserMessage.ShouldContain("temporarily unavailable");
	}

	#endregion

	#region ConcurrencyConflict Exception Tests

	[Fact]
	public void ConcurrencyConflict_WithResourceType_CreatesConcurrencyException()
	{
		// Arrange & Act
		var ex = ExceptionFactory.ConcurrencyConflict("Order");

		// Assert
		_ = ex.ShouldNotBeNull();
		ex.Message.ShouldContain("Order");
		ex.Message.ShouldContain("Concurrency conflict");
		ex.Context["resourceType"].ShouldBe("Order");
		ex.DispatchStatusCode.ShouldBe(409);
	}

	[Fact]
	public void ConcurrencyConflict_WithResourceId_IncludesId()
	{
		// Arrange & Act
		var ex = ExceptionFactory.ConcurrencyConflict("Document", "doc-456");

		// Assert
		ex.Message.ShouldContain("doc-456");
		ex.Context["resourceId"].ShouldBe("doc-456");
		ex.UserMessage.ShouldContain("modified by another user");
		ex.SuggestedAction.ShouldContain("refresh");
	}

	#endregion

	#region Wrap Exception Tests

	[Fact]
	public void Wrap_WithInnerException_CreatesWrappedException()
	{
		// Arrange
		var innerEx = new InvalidOperationException("Original error");

		// Act
		var ex = ExceptionFactory.Wrap(innerEx, "Wrapped message");

		// Assert
		_ = ex.ShouldNotBeNull();
		ex.Message.ShouldBe("Wrapped message");
		ex.InnerException.ShouldBe(innerEx);
	}

	[Fact]
	public void Wrap_WithCustomErrorCode_UsesProvidedCode()
	{
		// Arrange
		var innerEx = new ArgumentException("Bad argument");

		// Act
		var ex = ExceptionFactory.Wrap(innerEx, "Custom wrapped", "CUSTOM-001");

		// Assert
		ex.ErrorCode.ShouldBe("CUSTOM-001");
	}

	[Fact]
	public void Wrap_WithoutErrorCode_UsesUnknownError()
	{
		// Arrange
		var innerEx = new InvalidOperationException("Unexpected null");

		// Act
		var ex = ExceptionFactory.Wrap(innerEx, "Unknown issue");

		// Assert
		ex.ErrorCode.ShouldBe(ErrorCodes.UnknownError);
	}

	#endregion

	#region Typical Usage Scenarios

	[Fact]
	public void ValidationScenario_MultipleFieldErrors()
	{
		// Arrange
		var errors = new Dictionary<string, string[]>
		{
			["Email"] = ["Required", "Invalid format"],
			["Password"] = ["Too short"],
			["Username"] = ["Already taken"],
		};

		// Act
		var ex = ExceptionFactory.Validation(errors);

		// Assert
		ex.ValidationErrors.Count.ShouldBe(3);
	}

	[Fact]
	public void HttpStatusCodeMapping_Scenario()
	{
		// Arrange & Act
		var resourceNotFound = ExceptionFactory.ResourceNotFound("Item");
		var timeout = ExceptionFactory.Timeout("Process", TimeSpan.FromSeconds(10));
		var unauthorized = ExceptionFactory.Unauthorized();
		var forbidden = ExceptionFactory.Forbidden();
		var conflict = ExceptionFactory.ConcurrencyConflict("Entity");
		var serviceUnavailable = ExceptionFactory.CircuitBreakerOpen("Service");

		// Assert - HTTP status code mapping (via DispatchStatusCode property)
		resourceNotFound.DispatchStatusCode.ShouldBe(404);
		timeout.DispatchStatusCode.ShouldBe(408);
		unauthorized.DispatchStatusCode.ShouldBe(401);
		forbidden.DispatchStatusCode.ShouldBe(403);
		conflict.DispatchStatusCode.ShouldBe(409);
		serviceUnavailable.DispatchStatusCode.ShouldBe(503);
	}

	#endregion
}
