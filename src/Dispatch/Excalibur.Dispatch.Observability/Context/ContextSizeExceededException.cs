// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Observability.Context;

/// <summary>
/// Exception thrown when context size exceeds threshold.
/// </summary>
public sealed class ContextSizeExceededException : Exception
{
	/// <summary>
	/// Initializes a new instance of the <see cref="ContextSizeExceededException" /> class.
	/// </summary>
	public ContextSizeExceededException()
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="ContextSizeExceededException" /> class.
	/// </summary>
	/// <param name="message"> The message that describes the error. </param>
	public ContextSizeExceededException(string message)
		: base(message)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="ContextSizeExceededException" /> class.
	/// </summary>
	/// <param name="message"> The error message that explains the reason for the exception. </param>
	/// <param name="innerException"> The exception that is the cause of the current exception. </param>
	public ContextSizeExceededException(string message, Exception innerException)
		: base(message, innerException)
	{
	}
}
