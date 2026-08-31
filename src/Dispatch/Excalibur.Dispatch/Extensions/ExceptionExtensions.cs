// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

using Excalibur.Dispatch.Exceptions;

namespace Excalibur.Dispatch.Extensions;

/// <summary>
/// Provides extension methods for retrieving error and status codes from exceptions with improved performance and consistency.
/// </summary>
/// <remarks>
/// This class provides thread-safe methods to extract error codes and status codes from exceptions, with optimized reflection caching and
/// consistent return value handling.
/// </remarks>
public static class ExceptionExtensions
{
	/// <summary>
	/// Maximum number of entries allowed in the reflection property caches.
	/// When the cap is reached, new lookups bypass the cache to prevent unbounded memory growth.
	/// </summary>
	private const int MaxCacheEntries = 1024;

	/// <summary>
	/// Constants for clarity.
	/// </summary>
	private const string ErrorCodeKey = "ErrorCode";

	private const string StatusCodeKey = "StatusCode";

	/// <summary>
	/// Cache for reflection results to improve performance.
	/// </summary>
	private static readonly ConcurrentDictionary<Type, PropertyInfo?> ErrorCodePropertyCache = new();

	private static readonly ConcurrentDictionary<Type, PropertyInfo?> StatusCodePropertyCache = new();

	/// <summary>
	/// Attempts to retrieve the error code from an exception.
	/// </summary>
	/// <param name="exception"> The exception to extract the error code from. </param>
	/// <returns>
	/// The error code if found; otherwise, <c> null </c>. Returns the first error code found in this order:
	/// 1. The <see cref="DispatchException.ErrorCode" /> of a dispatch exception
	/// 2. ErrorCode property on the exception type, rendered as text
	/// 3. ErrorCode entry in the exception's Data dictionary
	/// 4. ErrorCode from inner exceptions (recursively)
	/// 5. For AggregateException, the first non-null error code from inner exceptions.
	/// </returns>
	/// <remarks>
	/// Error codes are text (<c> "MSG005" </c>), not numbers. An exception that types its own code as an
	/// integer still answers here -- the value is rendered as text rather than skipped.
	/// </remarks>
	/// <exception cref="ArgumentNullException"> Thrown if the <paramref name="exception" /> parameter is <c> null </c>. </exception>
	public static string? GetErrorCode(this Exception exception)
	{
		ArgumentNullException.ThrowIfNull(exception);

		// A DispatchException resolves its own code, and that answer outranks reflection -- the same
		// precedence GetStatusCode already applies.
		if (exception is DispatchException dispatchException)
		{
			return dispatchException.ErrorCode;
		}

		// Check for ErrorCode property using cached reflection
		var errorCode = GetProperty(exception, ErrorCodeKey, ErrorCodePropertyCache).GetValueOrNull(exception)?.ToString();
		if (errorCode is not null)
		{
			return errorCode;
		}

		// Check Data dictionary
		if (TryGetDataValue(exception, ErrorCodeKey, out var dataCode))
		{
			return dataCode?.ToString();
		}

		// Handle AggregateException specially
		if (exception is AggregateException aggEx)
		{
			foreach (var inner in aggEx.InnerExceptions)
			{
				var innerCode = inner.GetErrorCode();
				if (innerCode is not null)
				{
					return innerCode;
				}
			}
		}

		// Check inner exception recursively
		return exception.InnerException?.GetErrorCode();
	}

	/// <summary>
	/// Attempts to retrieve the status code from an exception.
	/// </summary>
	/// <param name="exception"> The exception to extract the status code from. </param>
	/// <returns>
	/// The status code if found; otherwise, <c> null </c>. Returns the first status code found in this order:
	/// 1. StatusCode property on the exception type
	/// 2. StatusCode entry in the exception's Data dictionary
	/// 3. StatusCode from inner exceptions (recursively).
	/// </returns>
	/// <exception cref="ArgumentNullException"> Thrown if the <paramref name="exception" /> parameter is <c> null </c>. </exception>
	public static int? GetStatusCode(this Exception exception)
	{
		ArgumentNullException.ThrowIfNull(exception);

		// A DispatchException resolves its own status, and that answer outranks reflection. Reflection
		// looks for a property literally named "StatusCode" and therefore finds the inherited
		// ApiException.StatusCode default, which silently beats the more specific DispatchStatusCode --
		// so a ValidationException declaring 400 was reported as 500. This mirrors the precedence the
		// exception already applies in its own ToProblemDetails().
		if (exception is DispatchException { DispatchStatusCode: { } dispatchStatusCode })
		{
			return dispatchStatusCode;
		}

		// Check for StatusCode property using cached reflection
		if (GetProperty(exception, StatusCodeKey, StatusCodePropertyCache).GetValueOrNull(exception) is int statusCode)
		{
			return statusCode;
		}

		// Check Data dictionary
		if (TryGetDataValue(exception, StatusCodeKey, out var dataCode) && dataCode is int intCode)
		{
			return intCode;
		}

		return exception.InnerException?.GetStatusCode();
	}

	/// <summary>
	/// Gets the status code from an exception with a default value if not found.
	/// </summary>
	/// <param name="exception"> The exception to extract the status code from. </param>
	/// <param name="defaultValue"> The default value to return if no status code is found. </param>
	/// <returns> The status code if found; otherwise, the specified default value. </returns>
	/// <exception cref="ArgumentNullException"> Thrown if the <paramref name="exception" /> parameter is <c> null </c>. </exception>
	public static int GetStatusCodeOrDefault(this Exception exception, int defaultValue = 500) => exception.GetStatusCode() ?? defaultValue;

	/// <summary>
	/// Gets the error code from an exception with a default value if not found.
	/// </summary>
	/// <param name="exception"> The exception to extract the error code from. </param>
	/// <param name="defaultValue"> The default value to return if no error code is found. </param>
	/// <returns> The error code if found; otherwise, the specified default value. </returns>
	/// <exception cref="ArgumentNullException"> Thrown if the <paramref name="exception" /> parameter is <c> null </c>. </exception>
	public static string GetErrorCodeOrDefault(this Exception exception, string defaultValue) => exception.GetErrorCode() ?? defaultValue;

	/// <summary>
	/// Checks if an exception has an error code.
	/// </summary>
	/// <param name="exception"> The exception to check. </param>
	/// <returns> True if the exception has an error code; otherwise, false. </returns>
	/// <exception cref="ArgumentNullException"> Thrown if the <paramref name="exception" /> parameter is <c> null </c>. </exception>
	public static bool HasErrorCode(this Exception exception) => exception.GetErrorCode() is not null;

	/// <summary>
	/// Checks if an exception has a status code.
	/// </summary>
	/// <param name="exception"> The exception to check. </param>
	/// <returns> True if the exception has a status code; otherwise, false. </returns>
	/// <exception cref="ArgumentNullException"> Thrown if the <paramref name="exception" /> parameter is <c> null </c>. </exception>
	public static bool HasStatusCode(this Exception exception) => exception.GetStatusCode().HasValue;


	/// <summary>
	/// Finds a well-known property on an exception type, using a bounded reflection cache.
	/// </summary>
	/// <remarks>
	/// The lookup does not filter on property type. Filtering here is what made the error-code accessor
	/// unable to see a code the exception really carried; the caller interprets the value instead.
	/// </remarks>
	[UnconditionalSuppressMessage(
		"AOT",
		"IL2070:'this' argument does not satisfy 'DynamicallyAccessedMemberTypes.PublicProperties' in call to 'System.Type.GetProperty'",
		Justification =
			"This method only accesses well-known properties (ErrorCode, StatusCode) on exception types. The property access is cached and handles missing properties gracefully.")]
	private static PropertyInfo? GetProperty(Exception exception, string propertyName, ConcurrentDictionary<Type, PropertyInfo?> cache)
	{
		var exceptionType = exception.GetType();

		// Bounded cache: try fast lookup first, then check capacity before adding
		if (cache.TryGetValue(exceptionType, out var propertyInfo))
		{
			return propertyInfo;
		}

		if (cache.Count < MaxCacheEntries)
		{
			return cache.GetOrAdd(
				exceptionType,
				static (type, state) => type.GetProperty(
					state,
					BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static),
				propertyName);
		}

		// Cache full -- compute without caching to prevent unbounded growth
#pragma warning disable IL2075 // GetProperty on runtime Type - BCL return type cannot be annotated
		return exceptionType.GetProperty(
			propertyName,
			BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
#pragma warning restore IL2075
	}

	/// <summary>
	/// Reads a property off the given exception instance, treating a throwing getter as absent.
	/// </summary>
	private static object? GetValueOrNull(this PropertyInfo? propertyInfo, Exception exception)
	{
		if (propertyInfo is null)
		{
			return null;
		}

		try
		{
			return propertyInfo.GetValue(exception);
		}
		catch
		{
			// Property getter threw an exception
			return null;
		}
	}

	/// <summary>
	/// Safely attempts to get a value from the exception's Data dictionary.
	/// </summary>
	private static bool TryGetDataValue(Exception exception, string key, out object? value)
	{
		value = null;

		try
		{
			if (exception.Data.Contains(key))
			{
				value = exception.Data[key];
				return value is not null;
			}
		}
		catch
		{
			// Data dictionary access failed (rare but possible in multi-threaded scenarios)
		}

		return false;
	}
}
