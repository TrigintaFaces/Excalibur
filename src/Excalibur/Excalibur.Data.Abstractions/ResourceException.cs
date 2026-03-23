// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Data.Abstractions;

/// <summary>
/// Base exception class for resource-related errors in the Excalibur framework.
/// </summary>
/// <remarks>
/// <para>
/// This exception serves as the base class for all resource-related errors, including:
/// <list type="bullet">
///   <item><description><see cref="ResourceNotFoundException"/> - Resource not found (404)</description></item>
///   <item><description><see cref="ConflictException"/> - Resource conflict (409)</description></item>
/// </list>
/// </para>
/// <para>
/// Extends <see cref="ApiException"/> to preserve HTTP status code mapping and
/// RFC 7807 problem details support.
/// </para>
/// </remarks>
public class ResourceException : ApiException
{
	/// <summary>
	/// Initializes a new instance of the <see cref="ResourceException"/> class.
	/// </summary>
	public ResourceException()
		: base(404, "A resource error occurred.", null)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="ResourceException"/> class with a specified error message.
	/// </summary>
	/// <param name="message">The error message that explains the reason for the exception.</param>
	public ResourceException(string message)
		: base(404, message, null)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="ResourceException"/> class with a message and inner exception.
	/// </summary>
	/// <param name="message">The error message that explains the reason for the exception.</param>
	/// <param name="innerException">The exception that is the cause of the current exception.</param>
	public ResourceException(string message, Exception? innerException)
		: base(404, message, innerException)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="ResourceException"/> class with resource information.
	/// </summary>
	/// <param name="resource">The type or name of the resource associated with the error.</param>
	/// <param name="statusCode">The HTTP status code representing the error. Defaults to 404.</param>
	/// <param name="message">A custom error message. Defaults to a generic message using the resource name.</param>
	/// <param name="innerException">The inner exception that caused this exception, if applicable.</param>
	public ResourceException(
		string resource,
		int? statusCode = null,
		string? message = null,
		Exception? innerException = null)
		: base(statusCode ?? 404, message ?? $"Operation failed for resource {resource}", innerException)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(resource);

		Resource = resource;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="ResourceException"/> class with a status code, message, and inner exception.
	/// </summary>
	/// <param name="statusCode">The HTTP status code associated with the exception.</param>
	/// <param name="message">The error message that explains the reason for the exception.</param>
	/// <param name="innerException">The exception that is the cause of the current exception.</param>
	protected ResourceException(int statusCode, string message, Exception? innerException)
		: base(statusCode, message, innerException)
	{
	}

	/// <summary>
	/// Gets the type or name of the resource associated with this exception.
	/// </summary>
	/// <value>The resource type or name, or <see langword="null"/> if not specified.</value>
	public string? Resource { get; protected set; }

	/// <summary>
	/// Gets the identifier of the resource associated with this exception.
	/// </summary>
	/// <value>The resource identifier, or <see langword="null"/> if not specified.</value>
	public string? ResourceId { get; protected init; }

	/// <summary>
	/// Gets additional contextual data related to the exception.
	/// </summary>
	/// <value>A dictionary of context key-value pairs.</value>
	public Dictionary<string, object?> Context { get; } = [];

	/// <summary>
	/// Adds contextual information to the exception.
	/// </summary>
	/// <param name="key">The context key.</param>
	/// <param name="value">The context value.</param>
	/// <returns>The current exception instance for fluent configuration.</returns>
	public ResourceException WithContext(string key, object? value)
	{
		Context[key] = value;
		return this;
	}

	/// <summary>
	/// Creates a new instance of the <see cref="ResourceException"/> class with resource information.
	/// </summary>
	/// <param name="resource">The type or name of the resource.</param>
	/// <param name="resourceId">The optional identifier of the resource.</param>
	/// <returns>A new <see cref="ResourceException"/> instance.</returns>
	public static ResourceException ForResource(string resource, string? resourceId = null)
	{
		var ex = new ResourceException(FormatMessage(resource, resourceId))
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
