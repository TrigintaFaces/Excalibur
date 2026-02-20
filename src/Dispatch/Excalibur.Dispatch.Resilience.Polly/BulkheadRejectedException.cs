// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Resilience.Polly;

/// <summary>
/// Exception thrown when bulkhead rejects an operation.
/// </summary>
public sealed class BulkheadRejectedException : Exception
{
	/// <summary>
	/// Initializes a new instance of the <see cref="BulkheadRejectedException" /> class.
	/// </summary>
	public BulkheadRejectedException()
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="BulkheadRejectedException" /> class with a specified error message.
	/// </summary>
	/// <param name="message">The message that describes the error.</param>
	public BulkheadRejectedException(string message)
		: base(message)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="BulkheadRejectedException" /> class with a specified error message and inner exception.
	/// </summary>
	/// <param name="message">The message that describes the error.</param>
	/// <param name="innerException">The exception that is the cause of the current exception.</param>
	public BulkheadRejectedException(string message, Exception innerException)
		: base(message, innerException)
	{
	}
}
