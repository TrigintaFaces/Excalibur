// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Exceptions;

/// <summary>
/// Exception thrown when a message reaches the local bus with no handler registered for its type.
/// </summary>
/// <remarks>
/// <para>
/// A missing registration is a configuration fault, not a dispatch outcome: it cannot vary per request, every dispatch of that message
/// type fails identically, and the caller cannot recover from it. Middleware that convert an exception into a failed result — retry,
/// exception mapping, the circuit breaker — deliberately let this type through, so the fault reaches the host as a startup-shaped error
/// instead of arriving at a caller who would map a failed result to a client error and blame the end user for the operator's omission.
/// Retrying it is also wasted work: a registration absent on the first attempt is absent on the last.
/// </para>
/// <para>
/// It derives from <see cref="InvalidOperationException" /> so existing handling of an unconfigured dispatcher continues to catch it.
/// </para>
/// </remarks>
public sealed class HandlerNotRegisteredException : InvalidOperationException
{
	/// <summary>
	/// Initializes a new instance of the <see cref="HandlerNotRegisteredException" /> class.
	/// </summary>
	public HandlerNotRegisteredException()
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="HandlerNotRegisteredException" /> class with a specified error message.
	/// </summary>
	/// <param name="message"> The error message that explains the reason for the exception. </param>
	public HandlerNotRegisteredException(string message)
		: base(message)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="HandlerNotRegisteredException" /> class with a specified error message and a
	/// reference to the inner exception.
	/// </summary>
	/// <param name="message"> The error message that explains the reason for the exception. </param>
	/// <param name="innerException"> The exception that is the cause of the current exception. </param>
	public HandlerNotRegisteredException(string message, Exception innerException)
		: base(message, innerException)
	{
	}
}
