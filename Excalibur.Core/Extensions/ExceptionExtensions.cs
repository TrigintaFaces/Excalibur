namespace Excalibur.Core.Extensions;

/// <summary>
///     Provides extension methods for retrieving error and status codes from exceptions.
/// </summary>
public static class ExceptionExtensions
{
	/// <summary>
	///     Attempts to retrieve the error code from an exception.
	/// </summary>
	/// <param name="exception"> The exception to extract the error code from. </param>
	/// <returns> The error code if found; otherwise, <c> null </c>. </returns>
	/// <remarks>
	///     This method checks for an "ErrorCode" property on the exception type, or an "ErrorCode" entry in the exception's
	///     <see cref="Exception.Data" /> dictionary. If not found, it recursively checks the inner exception.
	/// </remarks>
	/// <exception cref="ArgumentNullException"> Thrown if the <paramref name="exception" /> parameter is <c> null </c>. </exception>
	public static int? GetErrorCode(this Exception exception)
	{
		ArgumentNullException.ThrowIfNull(exception);

		var errorCodeProperty = exception.GetType().GetProperty("ErrorCode");

		if (errorCodeProperty != null && errorCodeProperty.PropertyType == typeof(int))
		{
			return (int?)errorCodeProperty.GetValue(exception);
		}

		if (exception.Data.Contains("ErrorCode") && exception.Data["ErrorCode"] is int code)
		{
			return code;
		}

		return exception.InnerException?.GetErrorCode() ?? -1;
	}

	/// <summary>
	///     Attempts to retrieve the status code from an exception.
	/// </summary>
	/// <param name="exception"> The exception to extract the status code from. </param>
	/// <returns> The status code if found; otherwise, <c> null </c>. </returns>
	/// <remarks>
	///     This method checks for a "StatusCode" property on the exception type, or a "StatusCode" entry in the exception's
	///     <see cref="Exception.Data" /> dictionary. If not found, it recursively checks the inner exception.
	/// </remarks>
	/// <exception cref="ArgumentNullException"> Thrown if the <paramref name="exception" /> parameter is <c> null </c>. </exception>
	public static int? GetStatusCode(this Exception exception)
	{
		ArgumentNullException.ThrowIfNull(exception);

		var statusCodeProperty = exception.GetType().GetProperty("StatusCode");

		if (statusCodeProperty != null && statusCodeProperty.PropertyType == typeof(int))
		{
			return (int?)statusCodeProperty.GetValue(exception);
		}

		if (exception.Data.Contains("StatusCode") && exception.Data["StatusCode"] is int code)
		{
			return code;
		}

		return exception.InnerException?.GetStatusCode() ?? 500;
	}
}
