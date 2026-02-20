// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Represents an exception specifically designed for API-related errors.
/// </summary>
[Serializable]
public class ApiException : Exception
{
	private const int DefaultStatusCode = 500;
	private static readonly string DefaultMessage = ErrorMessages.UnexpectedErrorOccurred;
	private static readonly string StatusCodeRangeMessage = ErrorMessages.StatusCodeMustBeBetween100And599;

	/// <summary>
	/// Initializes a new instance of the <see cref="ApiException" /> class with a default error message.
	/// </summary>
	public ApiException()
		: base(DefaultMessage)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="ApiException" /> class with a specified error message.
	/// </summary>
	/// <param name="message"> The error message describing the exception. </param>
	public ApiException(string message)
		: base(message)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="ApiException" /> class with a specified error message and an inner exception that
	/// caused this exception.
	/// </summary>
	/// <param name="message"> The error message describing the exception. </param>
	/// <param name="innerException"> The exception that caused the current exception. </param>
	public ApiException(string message, Exception? innerException)
		: base(message, innerException)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="ApiException" /> class with a specified status code, error message, and inner exception.
	/// </summary>
	/// <param name="statusCode"> The HTTP status code associated with the exception. </param>
	/// <param name="message"> The error message describing the exception. If null, a default message is used. </param>
	/// <param name="innerException"> The exception that caused the current exception. </param>
	/// <exception cref="ArgumentOutOfRangeException"> Thrown if the <paramref name="statusCode" /> is outside the valid range (100-599). </exception>
	public ApiException(int statusCode, string? message, Exception? innerException)
		: base(message ?? DefaultMessage, innerException)
	{
		if (statusCode is < 100 or > 599)
		{
			throw new ArgumentOutOfRangeException(nameof(statusCode), statusCode, StatusCodeRangeMessage);
		}

		StatusCode = statusCode;
	}

	/// <summary>
	/// Gets a unique identifier for the error instance.
	/// </summary>
	/// <value>
	/// A unique identifier for the error instance.
	/// </value>
	public Guid Id { get; } = Guid.NewGuid();

	/// <summary>
	/// Gets the HTTP status code associated with this exception.
	/// </summary>
	/// <value>
	/// The HTTP status code associated with this exception.
	/// </value>
	public int StatusCode { get; init; } = DefaultStatusCode;

	/// <summary>
	/// Converts this exception to RFC 7807 problem details format.
	/// </summary>
	/// <returns> A <see cref="MessageProblemDetails"/> instance representing this exception. </returns>
	/// <remarks>
	/// <para>
	/// This method creates a standardized problem details response suitable for API error responses.
	/// The generated problem details follows RFC 7807 and includes:
	/// </para>
	/// <list type="bullet">
	///   <item><description><c>Type</c>: A URN derived from the exception type name</description></item>
	///   <item><description><c>Title</c>: The exception type name in human-readable form</description></item>
	///   <item><description><c>ErrorCode</c>: From <see cref="GetProblemDetailsErrorCode"/> (defaults to status code)</description></item>
	///   <item><description><c>Status</c>: The HTTP status code</description></item>
	///   <item><description><c>Detail</c>: The exception message</description></item>
	///   <item><description><c>Instance</c>: A unique URN using the exception's Id</description></item>
	///   <item><description><c>Extensions</c>: From <see cref="GetProblemDetailsExtensions"/> (derived types can add custom data)</description></item>
	/// </list>
	/// <para>
	/// Derived classes can override <see cref="GetProblemDetailsErrorCode"/> and <see cref="GetProblemDetailsExtensions"/>
	/// to customize the problem details without overriding the entire method.
	/// </para>
	/// </remarks>
	public virtual MessageProblemDetails ToProblemDetails()
	{
		// Type URIs are conventionally lowercase per RFC 7807
#pragma warning disable CA1308 // Normalize strings to uppercase
		var typeUri = $"urn:dispatch:error:{GetType().Name.ToLowerInvariant()}";
#pragma warning restore CA1308

		var problemDetails = new MessageProblemDetails
		{
			Type = typeUri,
			Title = GetType().Name,
			ErrorCode = GetProblemDetailsErrorCode(),
			Status = StatusCode,
			Detail = Message,
			Instance = $"urn:dispatch:exception:{Id}",
		};

		// Apply any extensions from derived types
		var extensions = GetProblemDetailsExtensions();
		if (extensions is { Count: > 0 })
		{
			foreach (var (key, value) in extensions)
			{
				problemDetails.Extensions[key] = value;
			}
		}

		return problemDetails;
	}

	/// <summary>
	/// Gets the error code to include in problem details.
	/// </summary>
	/// <returns>The error code. Defaults to the HTTP status code.</returns>
	/// <remarks>
	/// <para>
	/// Derived types can override this method to provide a more specific error code
	/// that consumers can use for programmatic error handling.
	/// </para>
	/// </remarks>
	protected virtual int GetProblemDetailsErrorCode() => StatusCode;

	/// <summary>
	/// Gets additional extension data to include in problem details.
	/// </summary>
	/// <returns>
	/// A dictionary of extension data to include in the problem details response,
	/// or <see langword="null"/> if no extensions should be added.
	/// </returns>
	/// <remarks>
	/// <para>
	/// Derived types can override this method to add type-specific data to the
	/// problem details response. For example, a <c>ValidationException</c> might
	/// include a dictionary of field-level validation errors.
	/// </para>
	/// <para>
	/// The returned dictionary keys will be added to the problem details
	/// <see cref="MessageProblemDetails.Extensions"/> collection.
	/// </para>
	/// </remarks>
	protected virtual IDictionary<string, object?>? GetProblemDetailsExtensions() => null;
}
