// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Data.SqlServer.Cdc;

/// <summary>
/// Exception thrown when unmatched UpdateBefore/UpdateAfter record pairs are detected during CDC (Change Data Capture) processing.
/// </summary>
/// <remarks>
/// This exception indicates that during the processing of a specific LSN (Log Sequence Number), there were UpdateBefore records without
/// corresponding UpdateAfter records, or vice versa, which violates the expected CDC data integrity constraints.
/// </remarks>
[Serializable]
public class UnmatchedUpdateRecordsException : ApiException
{
	/// <summary>
	/// Initializes a new instance of the <see cref="UnmatchedUpdateRecordsException" /> class.
	/// </summary>
	/// <param name="lsn"> The lsn with unmatched update records. </param>
	/// <param name="statusCode"> The HTTP status code associated with the exception. Defaults to 500 if not specified. </param>
	/// <param name="message">
	/// The custom error message. Defaults to a message indicating that no handler is found for the specified table.
	/// </param>
	/// <param name="innerException"> The inner exception that caused the current exception, if applicable. </param>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="lsn" /> is null. </exception>
	public UnmatchedUpdateRecordsException(byte[] lsn, int? statusCode = null, string? message = null, Exception? innerException = null)
		: base(
			statusCode ?? 500,
			message ?? $"Unmatched UpdateBefore/UpdateAfter pairs detected at the end of LSN processing for LSN {lsn.ByteArrayToHex()}.",
			innerException)
	{
		ArgumentNullException.ThrowIfNull(lsn);

		Lsn = lsn;
	}

	public UnmatchedUpdateRecordsException() : base()
	{
	}

	public UnmatchedUpdateRecordsException(string message) : base(message)
	{
	}

	public UnmatchedUpdateRecordsException(string message, Exception? innerException) : base(message, innerException)
	{
	}

	public UnmatchedUpdateRecordsException(int statusCode, string? message, Exception? innerException) : base(statusCode, message,
		innerException)
	{
	}

	/// <summary>
	/// Gets or sets the LSN for which the duplicate record exists.
	/// </summary>
	/// <value> The LSN for which the duplicate record exists. </value>
	public byte[] Lsn { get; protected set; }
}
