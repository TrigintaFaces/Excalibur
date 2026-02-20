// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Exceptions;

/// <summary>
/// Factory for creating standardized exceptions with consistent error codes and messages.
/// </summary>
public static class ExceptionFactory
{
	/// <summary>
	/// Creates a configuration exception.
	/// </summary>
	public static ConfigurationException Configuration(string message, string? configKey = null)
	{
		var ex = new ConfigurationException(message);
		if (!string.IsNullOrEmpty(configKey))
		{
			_ = ex.WithContext("configKey", configKey);
		}

		return ex;
	}

	/// <summary>
	/// Creates a validation exception with field errors.
	/// </summary>
	public static ValidationException Validation(IDictionary<string, string[]> errors) => new(errors);

	/// <summary>
	/// Creates a validation exception with a single error message.
	/// </summary>
	public static ValidationException Validation(string fieldName, string errorMessage)
	{
		var errors = new Dictionary<string, string[]>(StringComparer.Ordinal) { [fieldName] = [errorMessage] };
		return new ValidationException(errors);
	}

	/// <summary>
	/// Creates a messaging exception.
	/// </summary>
	public static MessagingException Messaging(string message, string? messageId = null)
	{
		var ex = new MessagingException(message);
		if (!string.IsNullOrEmpty(messageId))
		{
			ex.MessageId = messageId;
			_ = ex.WithContext("messageId", messageId);
		}

		return ex;
	}

	/// <summary>
	/// Creates a serialization exception.
	/// </summary>
	public static DispatchSerializationException Serialization(string message, Type? targetType = null)
	{
		var ex = new DispatchSerializationException(message);
		if (targetType != null)
		{
			_ = ex.WithContext("targetType", targetType.FullName);
		}

		return ex;
	}

	/// <summary>
	/// Creates a resource not found exception.
	/// </summary>
	public static DispatchException ResourceNotFound(string resourceType, string? resourceId = null)
	{
		var message = resourceId != null
			? $"The {resourceType} with ID '{resourceId}' was not found."
			: $"The requested {resourceType} was not found.";

		return new DispatchException(ErrorCodes.ResourceNotFound, message)
			.WithContext("resourceType", resourceType)
			.WithContext("resourceId", resourceId)
			.WithStatusCode(404)
			.WithUserMessage($"The requested {resourceType} could not be found.")
			.WithSuggestedAction("Please check the resource identifier and try again.");
	}

	/// <summary>
	/// Creates a timeout exception.
	/// </summary>
	public static DispatchException Timeout(string operation, TimeSpan timeout) =>
		new DispatchException(
				ErrorCodes.TimeoutOperation,
				$"Operation '{operation}' timed out after {timeout.TotalSeconds} seconds.")
			.WithContext("operation", operation)
			.WithContext("timeoutSeconds", timeout.TotalSeconds)
			.WithStatusCode(408)
			.WithUserMessage("The operation took too long to complete.")
			.WithSuggestedAction("Please try again. If the problem persists, contact support.");

	/// <summary>
	/// Creates an unauthorized exception.
	/// </summary>
	public static DispatchException Unauthorized(string? reason = null) =>
		new DispatchException(
				ErrorCodes.SecurityAuthenticationFailed,
				reason ?? "Authentication is required to access this resource.")
			.WithStatusCode(401)
			.WithUserMessage("You need to be authenticated to access this resource.")
			.WithSuggestedAction("Please log in and try again.");

	/// <summary>
	/// Creates a forbidden exception.
	/// </summary>
	public static DispatchException Forbidden(string? reason = null) =>
		new DispatchException(
				ErrorCodes.SecurityAuthorizationFailed,
				reason ?? "You do not have permission to access this resource.")
			.WithStatusCode(403)
			.WithUserMessage("You don't have permission to perform this action.")
			.WithSuggestedAction("Contact your administrator if you believe you should have access.");

	/// <summary>
	/// Creates a circuit breaker open exception.
	/// </summary>
	public static DispatchException CircuitBreakerOpen(string serviceName, TimeSpan? retryAfter = null)
	{
		var message = $"Circuit breaker is open for service '{serviceName}'.";
		if (retryAfter.HasValue)
		{
			message += $" Retry after {retryAfter.Value.TotalSeconds} seconds.";
		}

		return new DispatchException(ErrorCodes.ResilienceCircuitBreakerOpen, message)
			.WithContext("serviceName", serviceName)
			.WithContext("retryAfterSeconds", retryAfter?.TotalSeconds)
			.WithStatusCode(503)
			.WithUserMessage("The service is temporarily unavailable.")
			.WithSuggestedAction("Please try again later.");
	}

	/// <summary>
	/// Creates a concurrency conflict exception.
	/// </summary>
	public static DispatchException ConcurrencyConflict(string resourceType, string? resourceId = null)
	{
		var message = resourceId != null
			? $"Concurrency conflict detected for {resourceType} with ID '{resourceId}'."
			: $"Concurrency conflict detected for {resourceType}.";

		return new DispatchException(ErrorCodes.DataConcurrencyConflict, message)
			.WithContext("resourceType", resourceType)
			.WithContext("resourceId", resourceId)
			.WithStatusCode(409)
			.WithUserMessage("The resource has been modified by another user.")
			.WithSuggestedAction("Please refresh and try again.");
	}

	/// <summary>
	/// Wraps an existing exception with additional context.
	/// </summary>
	public static DispatchException Wrap(Exception innerException, string message, string? errorCode = null) =>
		new(
			errorCode ?? ErrorCodes.UnknownError,
			message,
			innerException);
}
