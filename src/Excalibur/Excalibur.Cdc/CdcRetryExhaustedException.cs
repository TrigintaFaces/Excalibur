// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Data;

namespace Excalibur.Cdc;

/// <summary>
/// The exception a CDC processor stops with when a failure it treated as transient did not clear within
/// the configured number of consecutive attempts.
/// </summary>
/// <remarks>
/// <para>
/// This is distinct from a fatal error on purpose, because the remedy differs. A fatal error will fail the
/// same way on every attempt, so restarting is futile until something is changed. A retry that was
/// exhausted was expected to clear and did not, often because another process still held a resource, so
/// restarting the processor after a delay is a reasonable response. The last failure is the
/// <see cref="Exception.InnerException"/>.
/// </para>
/// <para>
/// Raised only when <see cref="CdcFatalErrorOptions{TEvent}.MaxConsecutiveTransientFailures"/> is set.
/// </para>
/// </remarks>
public sealed class CdcRetryExhaustedException : ResourceException
{
	/// <summary>
	/// Initializes a new instance of the <see cref="CdcRetryExhaustedException"/> class.
	/// </summary>
	public CdcRetryExhaustedException()
		: base("The CDC processor stopped after repeated transient failures.")
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="CdcRetryExhaustedException"/> class with a message.
	/// </summary>
	/// <param name="message">The message that describes the error.</param>
	public CdcRetryExhaustedException(string message)
		: base(message)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="CdcRetryExhaustedException"/> class with a message and
	/// the failure that caused it.
	/// </summary>
	/// <param name="message">The message that describes the error.</param>
	/// <param name="innerException">The last transient failure.</param>
	public CdcRetryExhaustedException(string message, Exception? innerException)
		: base(message, innerException)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="CdcRetryExhaustedException"/> class for a processor that
	/// reached its limit of consecutive transient failures.
	/// </summary>
	/// <param name="consecutiveFailures">The number of consecutive transient failures observed.</param>
	/// <param name="lastFailure">The last transient failure.</param>
	public CdcRetryExhaustedException(int consecutiveFailures, Exception lastFailure)
		: base(
			$"The CDC processor stopped after {consecutiveFailures} consecutive transient failures without progress.",
			lastFailure)
	{
		ConsecutiveFailures = consecutiveFailures;
	}

	/// <summary>
	/// Gets the number of consecutive transient failures the processor observed before stopping.
	/// </summary>
	/// <value>The failure count, or zero when the exception was created without one.</value>
	public int ConsecutiveFailures { get; }
}
