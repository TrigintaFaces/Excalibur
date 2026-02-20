// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Exceptions;

/// <summary>
/// Base exception class for resource-related errors in the Dispatch framework.
/// </summary>
/// <remarks>
/// <para>
/// This exception serves as the base class for all resource-related errors, including:
/// <list type="bullet">
///   <item><description><see cref="ResourceNotFoundException"/> - Resource not found (404)</description></item>
///   <item><description><see cref="ConflictException"/> - Resource conflict (409)</description></item>
///   <item><description><see cref="ForbiddenException"/> - Access forbidden (403)</description></item>
/// </list>
/// </para>
/// <para>
/// The default HTTP status code is 404 (Not Found) as this is the most common resource error,
/// but derived classes may override this based on the specific error type.
/// </para>
/// </remarks>
[Serializable]
public class ResourceException : DispatchException
{
	/// <summary>
	/// Initializes a new instance of the <see cref="ResourceException"/> class.
	/// </summary>
	public ResourceException()
		: this(ErrorCodes.ResourceNotFound, "A resource error occurred.")
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="ResourceException"/> class with a specified error message.
	/// </summary>
	/// <param name="message">The error message that explains the reason for the exception.</param>
	public ResourceException(string message)
		: this(ErrorCodes.ResourceNotFound, message)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="ResourceException"/> class with a message and inner exception.
	/// </summary>
	/// <param name="message">The error message that explains the reason for the exception.</param>
	/// <param name="innerException">The exception that is the cause of the current exception.</param>
	public ResourceException(string message, Exception? innerException)
		: this(ErrorCodes.ResourceNotFound, message, innerException)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="ResourceException"/> class with an error code and message.
	/// </summary>
	/// <param name="errorCode">The error code associated with this exception.</param>
	/// <param name="message">The error message that explains the reason for the exception.</param>
	protected ResourceException(string errorCode, string message)
		: base(errorCode, message)
	{
		DispatchStatusCode = 404;
	}

	/// <summary>
	/// Creates a new instance of the <see cref="ResourceException"/> class with resource information.
	/// </summary>
	/// <param name="resource">The type or name of the resource.</param>
	/// <param name="resourceId">The optional identifier of the resource.</param>
	/// <returns>A new <see cref="ResourceException"/> instance.</returns>
	public static ResourceException ForResource(string resource, string? resourceId = null)
	{
		var ex = new ResourceException(ErrorCodes.ResourceNotFound, FormatMessage(resource, resourceId))
		{
			Resource = resource,
			ResourceId = resourceId,
		};
		_ = ex.WithContext("resource", resource);
		if (resourceId != null)
		{
			_ = ex.WithContext("resourceId", resourceId);
		}

		return ex;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="ResourceException"/> class with an error code, message, and inner exception.
	/// </summary>
	/// <param name="errorCode">The error code associated with this exception.</param>
	/// <param name="message">The error message that explains the reason for the exception.</param>
	/// <param name="innerException">The exception that is the cause of the current exception.</param>
	public ResourceException(string errorCode, string message, Exception? innerException)
		: base(errorCode, message, innerException)
	{
		DispatchStatusCode = 404;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="ResourceException"/> class with a status code, error code, message, and inner exception.
	/// </summary>
	/// <param name="statusCode">The HTTP status code associated with the exception.</param>
	/// <param name="errorCode">The error code associated with this exception.</param>
	/// <param name="message">The error message that explains the reason for the exception.</param>
	/// <param name="innerException">The exception that is the cause of the current exception.</param>
	protected ResourceException(int statusCode, string errorCode, string message, Exception? innerException)
		: base(statusCode, errorCode, message, innerException)
	{
	}

	/// <summary>
	/// Gets the type or name of the resource associated with this exception.
	/// </summary>
	/// <value>The resource type or name, or <see langword="null"/> if not specified.</value>
	public string? Resource { get; protected init; }

	/// <summary>
	/// Gets the identifier of the resource associated with this exception.
	/// </summary>
	/// <value>The resource identifier, or <see langword="null"/> if not specified.</value>
	public string? ResourceId { get; protected init; }

	/// <summary>
	/// Formats the default error message for a resource error.
	/// </summary>
	/// <param name="resource">The resource type.</param>
	/// <param name="resourceId">The optional resource identifier.</param>
	/// <returns>A formatted error message.</returns>
	protected static string FormatMessage(string resource, string? resourceId) =>
		resourceId != null
			? $"Resource '{resource}' with ID '{resourceId}' error."
			: $"Resource '{resource}' error.";

	/// <inheritdoc/>
	protected override IDictionary<string, object?>? GetProblemDetailsExtensions()
	{
		var extensions = new Dictionary<string, object?>(StringComparer.Ordinal);

		if (Resource != null)
		{
			extensions["resource"] = Resource;
		}

		if (ResourceId != null)
		{
			extensions["resourceId"] = ResourceId;
		}

		// Merge with any context data
		foreach (var (key, value) in Context)
		{
			_ = extensions.TryAdd(key, value);
		}

		return extensions.Count > 0 ? extensions : null;
	}
}
