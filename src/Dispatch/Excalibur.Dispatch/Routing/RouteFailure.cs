// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Routing;

/// <summary>
/// Represents failure details for a specific route delivery attempt.
/// </summary>
internal sealed class RouteFailure
{
	/// <summary>
	/// Initializes a new instance of the <see cref="RouteFailure" /> class.
	/// </summary>
	/// <param name="message"> The failure message. </param>
	/// <param name="exceptionType"> The exception type name, if available. </param>
	public RouteFailure(string message, string? exceptionType = null)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(message);
		Message = message;
		ExceptionType = exceptionType;
	}

	/// <summary>
	/// Gets the failure message.
	/// </summary>
	public string Message { get; }

	/// <summary>
	/// Gets the exception type name, if available.
	/// </summary>
	public string? ExceptionType { get; }

	/// <summary>
	/// Creates a <see cref="RouteFailure" /> from an exception.
	/// </summary>
	/// <param name="exception"> The exception that caused the failure. </param>
	/// <returns> A route failure with exception details. </returns>
	public static RouteFailure FromException(Exception exception)
	{
		ArgumentNullException.ThrowIfNull(exception);
		var typeName = exception.GetType().FullName ?? exception.GetType().Name;
		return new RouteFailure(exception.Message, typeName);
	}
}
