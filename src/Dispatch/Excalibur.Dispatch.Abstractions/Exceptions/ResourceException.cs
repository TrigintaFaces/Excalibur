// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Represents a base exception for errors that occur during operations on a specific resource.
/// </summary>
/// <remarks>
/// This class serves as a foundation for more specific resource-related exceptions, such as concurrency conflicts or resource existence checks.
/// </remarks>
[Serializable]
public class ResourceException : ApiException
{
	/// <summary>
	/// Initializes a new instance of the <see cref="ResourceException" /> class.
	/// </summary>
	/// <param name="resource"> The name or type of the resource associated with the error. </param>
	/// <param name="statusCode"> The HTTP status code representing the error. Defaults to <c> 500 Internal Server Error </c> if not specified. </param>
	/// <param name="message"> A custom error message describing the error. Defaults to a generic message using the resource name. </param>
	/// <param name="innerException"> The inner exception that caused this exception, if applicable. </param>
	public ResourceException(
		string resource,
		int? statusCode = null,
		string? message = null,
		Exception? innerException = null)
		: base(statusCode ?? 500, message ?? $"Operation failed for resource {resource}", innerException)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(resource);

		Resource = resource;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="ResourceException" /> class with a default message.
	/// </summary>
	public ResourceException()
	{
		Resource = string.Empty;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="ResourceException" /> class with a specified error message.
	/// </summary>
	/// <param name="message"> The error message describing the exception. </param>
	public ResourceException(string message) : base(message)
	{
		Resource = string.Empty;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="ResourceException" /> class with a specified error message and an inner exception.
	/// </summary>
	/// <param name="message"> The error message describing the exception. </param>
	/// <param name="innerException"> The exception that caused the current exception. </param>
	public ResourceException(string message, Exception? innerException) : base(message, innerException)
	{
		Resource = string.Empty;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="ResourceException" /> class with a status code, message, and inner exception.
	/// </summary>
	/// <param name="statusCode"> The HTTP status code associated with the exception. </param>
	/// <param name="message"> The error message describing the exception. </param>
	/// <param name="innerException"> The exception that caused the current exception. </param>
	public ResourceException(int statusCode, string? message, Exception? innerException)
		: base(statusCode, message, innerException)
	{
		Resource = string.Empty;
	}

	/// <summary>
	/// Gets or sets the name or type of the resource that caused the error.
	/// </summary>
	/// <value> A string representing the resource associated with the exception. </value>
	public string Resource { get; protected set; }
}
