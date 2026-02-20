// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR
// AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Exceptions;

/// <summary>
/// Base exception class for all Excalibur framework exceptions. Provides consistent error handling, categorization, and structured error data.
/// </summary>
/// <remarks>
/// <para>
/// This exception extends <see cref="ApiException" /> to provide a unified exception hierarchy for the Excalibur framework. It adds rich
/// error categorization, distributed tracing support, and fluent configuration capabilities.
/// </para>
/// <para> The inheritance chain is: <c> Exception → ApiException → DispatchException → [Specialized Exceptions] </c> </para>
/// </remarks>
[Serializable]
public class DispatchException : ApiException
{
	/// <summary>
	/// Initializes a new instance of the <see cref="DispatchException" /> class with a default error message.
	/// </summary>
	public DispatchException()
		: this(ErrorCodes.UnknownError, ErrorMessages.DispatchFrameworkError)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="DispatchException" /> class with a specified error message.
	/// </summary>
	/// <param name="message"> The error message that explains the reason for the exception. </param>
	public DispatchException(string message)
		: this(ErrorCodes.UnknownError, message)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="DispatchException" /> class with a specified error message and a reference to the inner
	/// exception that is the cause of this exception.
	/// </summary>
	/// <param name="message"> The error message that explains the reason for the exception. </param>
	/// <param name="innerException"> The exception that is the cause of the current exception. </param>
	public DispatchException(string message, Exception? innerException)
		: this(ErrorCodes.UnknownError, message, innerException)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="DispatchException" /> class with a specified error code and message.
	/// </summary>
	/// <param name="errorCode"> The error code associated with this exception. </param>
	/// <param name="message"> The error message that explains the reason for the exception. </param>
	public DispatchException(string errorCode, string message)
		: this(errorCode, message, innerException: null)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="DispatchException" /> class with all properties.
	/// </summary>
	/// <param name="errorCode"> The error code associated with this exception. </param>
	/// <param name="message"> The error message that explains the reason for the exception. </param>
	/// <param name="innerException"> The exception that is the cause of the current exception. </param>
	public DispatchException(string errorCode, string message, Exception? innerException)
		: base(message, innerException)
	{
		ErrorCode = errorCode ?? ErrorCodes.UnknownError;
		Category = DetermineCategory(errorCode);
		Severity = DetermineSeverity(Category);
		InstanceId = Guid.NewGuid();
		Timestamp = DateTimeOffset.UtcNow;

		// Capture activity context if available
		var activity = Activity.Current;
		if (activity != null)
		{
			TraceId = activity.TraceId.ToString();
			SpanId = activity.SpanId.ToString();
			CorrelationId = activity.RootId;
		}
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="DispatchException" /> class with a status code, error code, message, and inner exception.
	/// </summary>
	/// <param name="statusCode"> The HTTP status code associated with the exception. </param>
	/// <param name="errorCode"> The error code associated with this exception. </param>
	/// <param name="message"> The error message that explains the reason for the exception. </param>
	/// <param name="innerException"> The exception that is the cause of the current exception. </param>
	public DispatchException(int statusCode, string errorCode, string message, Exception? innerException)
		: base(statusCode, message, innerException)
	{
		ErrorCode = errorCode ?? ErrorCodes.UnknownError;
		Category = DetermineCategory(errorCode);
		Severity = DetermineSeverity(Category);
		InstanceId = Guid.NewGuid();
		Timestamp = DateTimeOffset.UtcNow;
		DispatchStatusCode = statusCode;

		// Capture activity context if available
		var activity = Activity.Current;
		if (activity != null)
		{
			TraceId = activity.TraceId.ToString();
			SpanId = activity.SpanId.ToString();
			CorrelationId = activity.RootId;
		}
	}

	/// <summary>
	/// Gets the unique error code associated with this exception.
	/// </summary>
	/// <value> The current <see cref="ErrorCode" /> value. </value>
	public string ErrorCode { get; }

	/// <summary>
	/// Gets the category of the error.
	/// </summary>
	/// <value> The current <see cref="Category" /> value. </value>
	public ErrorCategory Category { get; }

	/// <summary>
	/// Gets the severity level of the error.
	/// </summary>
	/// <value> The current <see cref="Severity" /> value. </value>
	public ErrorSeverity Severity { get; }

	/// <summary>
	/// Gets the unique instance identifier for this specific error occurrence.
	/// </summary>
	/// <value> The current <see cref="InstanceId" /> value. </value>
	public Guid InstanceId { get; }

	/// <summary>
	/// Gets the timestamp when the exception occurred.
	/// </summary>
	/// <value> The current <see cref="Timestamp" /> value. </value>
	public DateTimeOffset Timestamp { get; }

	/// <summary>
	/// Gets the trace ID from the current activity context, if available.
	/// </summary>
	/// <value> The current <see cref="TraceId" /> value. </value>
	public string? TraceId { get; }

	/// <summary>
	/// Gets the span ID from the current activity context, if available.
	/// </summary>
	/// <value> The current <see cref="SpanId" /> value. </value>
	public string? SpanId { get; }

	/// <summary>
	/// Gets or sets the correlation ID for tracing the error across services.
	/// </summary>
	/// <value> The current <see cref="CorrelationId" /> value. </value>
	public string? CorrelationId { get; set; }

	/// <summary>
	/// Gets additional contextual data related to the exception.
	/// </summary>
	/// <value> The current <see cref="Context" /> value. </value>
	public Dictionary<string, object?> Context { get; } = [];

	/// <summary>
	/// Gets or sets the user-friendly message that can be displayed to end users.
	/// </summary>
	/// <value> The current <see cref="UserMessage" /> value. </value>
	public string? UserMessage { get; set; }

	/// <summary>
	/// Gets or sets suggested actions to resolve the error.
	/// </summary>
	/// <value> The current <see cref="SuggestedAction" /> value. </value>
	public string? SuggestedAction { get; set; }

	/// <summary>
	/// Gets or sets the HTTP status code override for this exception.
	/// </summary>
	/// <value>
	/// The HTTP status code when explicitly set, otherwise <see langword="null" />. If null, the status code is determined automatically
	/// from the <see cref="Category" />.
	/// </value>
	/// <remarks>
	/// <para>
	/// This property allows overriding the status code that would be determined by <see cref="DetermineStatusCode" />. When set, it takes
	/// precedence over the category-based status code.
	/// </para>
	/// <para> Use <see cref="ApiException.StatusCode" /> to get the inherited base status code. </para>
	/// </remarks>
	public int? DispatchStatusCode { get; set; }

	/// <summary>
	/// Adds contextual information to the exception.
	/// </summary>
	/// <param name="key"> The context key. </param>
	/// <param name="value"> The context value. </param>
	/// <returns> The current exception instance for fluent configuration. </returns>
	public DispatchException WithContext(string key, object? value)
	{
		Context[key] = value;
		return this;
	}

	/// <summary>
	/// Sets the correlation ID for the exception.
	/// </summary>
	/// <param name="correlationId"> The correlation ID. </param>
	/// <returns> The current exception instance for fluent configuration. </returns>
	public DispatchException WithCorrelationId(string correlationId)
	{
		CorrelationId = correlationId;
		return this;
	}

	/// <summary>
	/// Sets a user-friendly message for the exception.
	/// </summary>
	/// <param name="userMessage"> The user-friendly message. </param>
	/// <returns> The current exception instance for fluent configuration. </returns>
	public DispatchException WithUserMessage(string userMessage)
	{
		UserMessage = userMessage;
		return this;
	}

	/// <summary>
	/// Sets a suggested action to resolve the error.
	/// </summary>
	/// <param name="suggestedAction"> The suggested action. </param>
	/// <returns> The current exception instance for fluent configuration. </returns>
	public DispatchException WithSuggestedAction(string suggestedAction)
	{
		SuggestedAction = suggestedAction;
		return this;
	}

	/// <summary>
	/// Sets the HTTP status code override for the exception.
	/// </summary>
	/// <param name="statusCode"> The HTTP status code. </param>
	/// <returns> The current exception instance for fluent configuration. </returns>
	public DispatchException WithStatusCode(int statusCode)
	{
		DispatchStatusCode = statusCode;
		return this;
	}

	/// <summary>
	/// Converts the exception to a problem details representation.
	/// </summary>
	/// <returns> A problem details object representing this exception. </returns>
	public virtual DispatchProblemDetails ToDispatchProblemDetails() =>
		new()
		{
			Type = DetermineProblemDetailsType(),
			Title = UserMessage ?? Message,
			Status = DispatchStatusCode ?? DetermineStatusCode(),
			Detail = Message,
			Instance = $"urn:excalibur:error:{InstanceId}",
			ErrorCode = ErrorCode,
			Category = Category.ToString(),
			Severity = Severity.ToString(),
			CorrelationId = CorrelationId,
			TraceId = TraceId,
			SpanId = SpanId,
			Timestamp = Timestamp,
			SuggestedAction = SuggestedAction,
			Extensions = Context,
		};

	/// <inheritdoc />
	/// <remarks>
	/// <para>
	/// This override provides a <see cref="MessageProblemDetails" /> representation of the exception for consumers that require the base
	/// RFC 7807 format.
	/// </para>
	/// <para>
	/// For the full Dispatch-specific problem details with distributed tracing information, use <see cref="ToDispatchProblemDetails" /> instead.
	/// </para>
	/// </remarks>
	public override MessageProblemDetails ToProblemDetails()
	{
		var statusCode = DispatchStatusCode ?? DetermineStatusCode();
		return new MessageProblemDetails
		{
			Type = DetermineProblemDetailsType(),
			Title = UserMessage ?? Message,
			Status = statusCode,
			ErrorCode = statusCode,
			Detail = Message,
			Instance = $"urn:excalibur:error:{InstanceId}",
		};
	}

	/// <summary>
	/// Determines the error category based on the error code.
	/// </summary>
	private static ErrorCategory DetermineCategory(string? errorCode)
	{
		if (string.IsNullOrEmpty(errorCode))
		{
			return ErrorCategory.Unknown;
		}

		return errorCode switch
		{
			_ when errorCode.StartsWith("CFG", StringComparison.OrdinalIgnoreCase) => ErrorCategory.Configuration,
			_ when errorCode.StartsWith("VAL", StringComparison.OrdinalIgnoreCase) => ErrorCategory.Validation,
			_ when errorCode.StartsWith("MSG", StringComparison.OrdinalIgnoreCase) => ErrorCategory.Messaging,
			_ when errorCode.StartsWith("SER", StringComparison.OrdinalIgnoreCase) => ErrorCategory.Serialization,
			_ when errorCode.StartsWith("NET", StringComparison.OrdinalIgnoreCase) => ErrorCategory.Network,
			_ when errorCode.StartsWith("SEC", StringComparison.OrdinalIgnoreCase) => ErrorCategory.Security,
			_ when errorCode.StartsWith("DAT", StringComparison.OrdinalIgnoreCase) => ErrorCategory.Data,
			_ when errorCode.StartsWith("TIM", StringComparison.OrdinalIgnoreCase) => ErrorCategory.Timeout,
			_ when errorCode.StartsWith("RES", StringComparison.OrdinalIgnoreCase) => ErrorCategory.Resource,
			_ when errorCode.StartsWith("SYS", StringComparison.OrdinalIgnoreCase) => ErrorCategory.System,
			_ => ErrorCategory.Unknown,
		};
	}

	/// <summary>
	/// Determines the error severity based on the category.
	/// </summary>
	private static ErrorSeverity DetermineSeverity(ErrorCategory category) =>
		category switch
		{
			ErrorCategory.Configuration => ErrorSeverity.Critical,
			ErrorCategory.Security => ErrorSeverity.Critical,
			ErrorCategory.System => ErrorSeverity.Error,
			ErrorCategory.Data => ErrorSeverity.Error,
			ErrorCategory.Messaging => ErrorSeverity.Warning,
			ErrorCategory.Validation => ErrorSeverity.Warning,
			ErrorCategory.Timeout => ErrorSeverity.Warning,
			ErrorCategory.Network => ErrorSeverity.Warning,
			ErrorCategory.Serialization => ErrorSeverity.Error,
			ErrorCategory.Resource => ErrorSeverity.Error,
			_ => ErrorSeverity.Information,
		};

	/// <summary>
	/// Determines the appropriate HTTP status code based on the error category.
	/// </summary>
	private int DetermineStatusCode() =>
		Category switch
		{
			ErrorCategory.Validation => 400, // Bad Request
			ErrorCategory.Security => 401, // Unauthorized
			ErrorCategory.Resource => 404, // Not Found
			ErrorCategory.Timeout => 408, // Request Timeout
			ErrorCategory.Configuration => 500, // Internal Server Error
			ErrorCategory.System => 500, // Internal Server Error
			ErrorCategory.Data => 422, // Unprocessable Entity
			ErrorCategory.Network => 503, // Service Unavailable
			ErrorCategory.Messaging => 500, // Internal Server Error
			ErrorCategory.Serialization => 400, // Bad Request
			_ => 500, // Internal Server Error
		};

	/// <summary>
	/// Determines the Problem Details Type URN based on the error category.
	/// </summary>
	/// <returns>A URN from <see cref="ProblemDetailsTypes"/>.</returns>
	private string DetermineProblemDetailsType() =>
		Category switch
		{
			ErrorCategory.Validation => ProblemDetailsTypes.Validation,
			ErrorCategory.Security => ProblemDetailsTypes.Forbidden,
			ErrorCategory.Resource => ProblemDetailsTypes.NotFound,
			ErrorCategory.Timeout => ProblemDetailsTypes.Timeout,
			ErrorCategory.Configuration => ProblemDetailsTypes.Internal,
			ErrorCategory.System => ProblemDetailsTypes.Internal,
			ErrorCategory.Data => ProblemDetailsTypes.Concurrency,
			ErrorCategory.Network => ProblemDetailsTypes.Transport,
			ErrorCategory.Messaging => ProblemDetailsTypes.Routing,
			ErrorCategory.Serialization => ProblemDetailsTypes.Serialization,
			_ => ProblemDetailsTypes.Internal,
		};
}
