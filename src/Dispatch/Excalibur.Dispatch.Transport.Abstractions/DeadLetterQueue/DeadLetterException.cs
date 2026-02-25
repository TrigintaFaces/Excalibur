// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Exception that wraps dead letter error information.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="DeadLetterException" /> class. </remarks>
public sealed class DeadLetterException(string message, string exceptionType, string? stackTrace) : Exception(message)
{
	public DeadLetterException()
		: this(string.Empty, string.Empty, null)
	{
	}

	public DeadLetterException(string? message)
		: this(message ?? string.Empty, string.Empty, null)
	{
	}

	public DeadLetterException(string? message, Exception? innerException)
		: this(message ?? string.Empty, innerException?.GetType().FullName ?? string.Empty, innerException?.StackTrace)
	{
	}

	/// <summary>
	/// Gets the original exception type name.
	/// </summary>
	/// <value>The current <see cref="ExceptionType"/> value.</value>
	public string ExceptionType { get; } = exceptionType;

	/// <summary>
	/// Gets the original stack trace.
	/// </summary>
	/// <value>The current <see cref="OriginalStackTrace"/> value.</value>
	public string? OriginalStackTrace { get; } = stackTrace;

	/// <inheritdoc />
	public override string? StackTrace => OriginalStackTrace ?? base.StackTrace;
}
