// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Represents an error that occurred during a message send operation.
/// Replaces <c>PublishError</c> + <c>PublishErrorCategory</c> with a simpler model
/// using <see cref="IsRetryable"/> and <see cref="Code"/> instead of an enum.
/// </summary>
public sealed class SendError
{
	/// <summary>
	/// Gets the error code (typically the exception type name or a broker-specific code).
	/// </summary>
	/// <value>The error code.</value>
	public string Code { get; init; } = string.Empty;

	/// <summary>
	/// Gets a human-readable description of the error.
	/// </summary>
	/// <value>The error message.</value>
	public string Message { get; init; } = string.Empty;

	/// <summary>
	/// Gets the underlying exception, if available.
	/// </summary>
	/// <value>The exception that caused the error, or <see langword="null"/>.</value>
	public Exception? Exception { get; init; }

	/// <summary>
	/// Gets a value indicating whether the operation can be retried.
	/// </summary>
	/// <value><see langword="true"/> if the error is transient and retryable; otherwise, <see langword="false"/>.</value>
	public bool IsRetryable { get; init; }

	/// <summary>
	/// Creates a <see cref="SendError"/> from an exception.
	/// </summary>
	/// <param name="exception">The exception that occurred.</param>
	/// <param name="isRetryable">Whether the error is retryable.</param>
	/// <returns>A new <see cref="SendError"/> instance.</returns>
	public static SendError FromException(Exception exception, bool isRetryable = false)
	{
		ArgumentNullException.ThrowIfNull(exception);
		return new SendError
		{
			Code = exception.GetType().Name,
			Message = exception.Message,
			Exception = exception,
			IsRetryable = isRetryable,
		};
	}
}
