// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Exceptions;

namespace Excalibur.Dispatch.Extensions;

/// <summary>
/// Provides extension methods for retrieving error and status codes from exceptions.
/// </summary>
/// <remarks>
/// Framework exceptions (<see cref="ApiException" /> and its derivatives, including <see cref="DispatchException" />)
/// are recognized by a direct type check -- never by probing for a conventionally-named property. Duck-typing a
/// property name off an arbitrary exception type is the job <see cref="Excalibur.Dispatch.IExceptionMapper" />
/// already owns for exception types this framework does not itself define; this class does not compete with it.
/// </remarks>
public static class ExceptionExtensions
{
	/// <summary>
	/// Constants for clarity.
	/// </summary>
	private const string ErrorCodeKey = "ErrorCode";

	private const string StatusCodeKey = "StatusCode";

	/// <summary>
	/// Attempts to retrieve the error code from an exception.
	/// </summary>
	/// <param name="exception"> The exception to extract the error code from. </param>
	/// <returns>
	/// The error code if found; otherwise, <c> null </c>. Returns the first error code found in this order:
	/// 1. The <see cref="DispatchException.ErrorCode" /> of a dispatch exception (a direct type check -- never
	///    a property-name probe over an arbitrary exception type)
	/// 2. ErrorCode entry in the exception's Data dictionary
	/// 3. ErrorCode from inner exceptions (recursively)
	/// 4. For AggregateException, the first non-null error code from inner exceptions.
	/// </returns>
	/// <remarks>
	/// Error codes are text (<c> "MSG005" </c>), not numbers. An exception that types its own code as an
	/// integer still answers here -- the value is rendered as text rather than skipped. A foreign exception
	/// type this framework does not define is not probed for a conventionally-named property: route it
	/// through <see cref="Excalibur.Dispatch.IExceptionMapper" /> instead.
	/// </remarks>
	/// <exception cref="ArgumentNullException"> Thrown if the <paramref name="exception" /> parameter is <c> null </c>. </exception>
	public static string? GetErrorCode(this Exception exception)
	{
		ArgumentNullException.ThrowIfNull(exception);

		// A DispatchException resolves its own code by direct type check.
		if (exception is DispatchException dispatchException)
		{
			return dispatchException.ErrorCode;
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
	/// 1. <see cref="DispatchException.DispatchStatusCode" />, when set
	/// 2. <see cref="ApiException.StatusCode" />, for any <see cref="ApiException" /> (including a
	///    <see cref="DispatchException" /> that did not set <see cref="DispatchException.DispatchStatusCode" />)
	/// 3. StatusCode entry in the exception's Data dictionary
	/// 4. StatusCode from inner exceptions (recursively).
	/// </returns>
	/// <remarks>
	/// Both framework checks are direct type checks, never a property-name probe over an arbitrary exception
	/// type -- that is what made a ValidationException declaring 400 report as 500: reflection found the
	/// inherited <see cref="ApiException.StatusCode" /> default before the more specific
	/// <see cref="DispatchException.DispatchStatusCode" /> was considered. A foreign exception type this
	/// framework does not define is not probed for a conventionally-named property: route it through
	/// <see cref="Excalibur.Dispatch.IExceptionMapper" /> instead.
	/// </remarks>
	/// <exception cref="ArgumentNullException"> Thrown if the <paramref name="exception" /> parameter is <c> null </c>. </exception>
	public static int? GetStatusCode(this Exception exception)
	{
		ArgumentNullException.ThrowIfNull(exception);

		// A DispatchException resolves its own status by direct type check, ahead of the ApiException
		// default it inherits: this mirrors the precedence the exception already applies in its own
		// ToProblemDetails().
		if (exception is DispatchException { DispatchStatusCode: { } dispatchStatusCode })
		{
			return dispatchStatusCode;
		}

		// Every ApiException (DispatchException included) carries a real StatusCode property -- reached by
		// direct type check, never by reflecting for a property literally named "StatusCode".
		if (exception is ApiException apiException)
		{
			return apiException.StatusCode;
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
