// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

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
	/// 1. ErrorCode property on the exception type
	/// 2. ErrorCode entry in the exception's Data dictionary
	/// 3. ErrorCode from inner exceptions (recursively)
	/// 4. For AggregateException, the first non-null error code from inner exceptions.
	/// </returns>
	/// <exception cref="ArgumentNullException"> Thrown if the <paramref name="exception" /> parameter is <c> null </c>. </exception>
	public static int? GetErrorCode(this Exception exception)
	{
		ArgumentNullException.ThrowIfNull(exception);

		// Check for ErrorCode property using cached reflection
		var errorCode = GetPropertyValue<int?>(exception, ErrorCodeKey, ErrorCodePropertyCache);
		if (errorCode.HasValue)
		{
			return errorCode;
		}

		// Check Data dictionary
		if (TryGetDataValue(exception, ErrorCodeKey, out var dataCode))
		{
			return dataCode;
		}

		// Handle AggregateException specially
		if (exception is AggregateException aggEx)
		{
			foreach (var inner in aggEx.InnerExceptions)
			{
				var innerCode = inner.GetErrorCode();
				if (innerCode.HasValue)
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

		// Check for StatusCode property using cached reflection
		var statusCode = GetPropertyValue<int?>(exception, StatusCodeKey, StatusCodePropertyCache);
		if (statusCode.HasValue)
		{
			return statusCode;
		}

		// Check Data dictionary
		if (TryGetDataValue(exception, StatusCodeKey, out var dataCode))
		{
			return dataCode;
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
	public static int GetErrorCodeOrDefault(this Exception exception, int defaultValue = -1) => exception.GetErrorCode() ?? defaultValue;

	/// <summary>
	/// Checks if an exception has an error code.
	/// </summary>
	/// <param name="exception"> The exception to check. </param>
	/// <returns> True if the exception has an error code; otherwise, false. </returns>
	/// <exception cref="ArgumentNullException"> Thrown if the <paramref name="exception" /> parameter is <c> null </c>. </exception>
	public static bool HasErrorCode(this Exception exception) => exception.GetErrorCode().HasValue;

	/// <summary>
	/// Checks if an exception has a status code.
	/// </summary>
	/// <param name="exception"> The exception to check. </param>
	/// <returns> True if the exception has a status code; otherwise, false. </returns>
	/// <exception cref="ArgumentNullException"> Thrown if the <paramref name="exception" /> parameter is <c> null </c>. </exception>
	public static bool HasStatusCode(this Exception exception) => exception.GetStatusCode().HasValue;


	/// <summary>
	/// Gets a property value from an exception using cached reflection.
	/// </summary>
	[UnconditionalSuppressMessage(
		"AOT",
		"IL2070:'this' argument does not satisfy 'DynamicallyAccessedMemberTypes.PublicProperties' in call to 'System.Type.GetProperty'",
		Justification =
			"This method only accesses well-known properties (ErrorCode, StatusCode) on exception types. The property access is cached and handles missing properties gracefully.")]
	private static T? GetPropertyValue<T>(Exception exception, string propertyName, ConcurrentDictionary<Type, PropertyInfo?> cache)
	{
		var exceptionType = exception.GetType();
		var propertyInfo = cache.GetOrAdd(
			exceptionType,
			(type, state) =>
			{
				var property = type.GetProperty(
					state,
					BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
				return property?.PropertyType == typeof(T) || property?.PropertyType == typeof(int) ? property : null;
			},
			propertyName);

		if (propertyInfo != null)
		{
			try
			{
				var value = propertyInfo.GetValue(exception);
				return value is T typedValue ? typedValue : default;
			}
			catch
			{
				// Property getter threw an exception
				return default;
			}
		}

		return default;
	}

	/// <summary>
	/// Safely attempts to get a value from the exception's Data dictionary.
	/// </summary>
	private static bool TryGetDataValue(Exception exception, string key, out int value)
	{
		value = 0;

		try
		{
			if (exception.Data.Contains(key))
			{
				var dataValue = exception.Data[key];
				if (dataValue is int intValue)
				{
					value = intValue;
					return true;
				}
			}
		}
		catch
		{
			// Data dictionary access failed (rare but possible in multi-threaded scenarios)
		}

		return false;
	}
}
