// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch;

namespace Excalibur.Data.DataProcessing.Exceptions;

/// <summary>
/// Represents an exception that is thrown when no <see cref="IDataProcessor" /> implementation is found for the specified record type.
/// </summary>
/// <remarks>
/// This exception is designed to handle cases where a record type cannot be processed due to the absence of a corresponding data
/// processor implementation in the system.
/// </remarks>
public sealed class MissingDataProcessorException : ApiException
{
	/// <summary>
	/// Initializes a new instance of the <see cref="MissingDataProcessorException" /> class
	/// with a record type and optional status code.
	/// </summary>
	/// <param name="recordType"> The record type for which the data processor is missing. </param>
	/// <param name="statusCode"> The HTTP status code for the exception. Defaults to 500. </param>
	/// <param name="innerException"> The inner exception, if any, that caused this exception. </param>
	/// <exception cref="ArgumentException">
	/// Thrown if <paramref name="recordType" /> is <c> null </c>, empty, or consists only of whitespace.
	/// </exception>
	public MissingDataProcessorException(string recordType, int statusCode, Exception? innerException = null)
		: base(statusCode,
			$"No {nameof(IDataProcessor)} implementation found for recordType {recordType}.",
			innerException)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(recordType);

		RecordType = recordType;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="MissingDataProcessorException" /> class
	/// with a record type, using the default status code of 500.
	/// </summary>
	/// <param name="recordType"> The record type for which the data processor is missing. </param>
	/// <param name="innerException"> The inner exception, if any, that caused this exception. </param>
	/// <exception cref="ArgumentException">
	/// Thrown if <paramref name="recordType" /> is <c> null </c>, empty, or consists only of whitespace.
	/// </exception>
	public MissingDataProcessorException(string recordType, Exception? innerException)
		: this(recordType, 500, innerException)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="MissingDataProcessorException" /> class with default values.
	/// </summary>
	public MissingDataProcessorException() : base()
	{
		RecordType = string.Empty;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="MissingDataProcessorException" /> class with a status code, message, and inner exception.
	/// </summary>
	/// <param name="statusCode">The HTTP status code.</param>
	/// <param name="message">The error message.</param>
	/// <param name="innerException">The inner exception.</param>
	public MissingDataProcessorException(int statusCode, string? message, Exception? innerException) : base(statusCode, message, innerException)
	{
		RecordType = string.Empty;
	}

	/// <summary>
	/// Gets the record type for which the data processor was not found.
	/// </summary>
	/// <value> A string representing the record type. </value>
	public string RecordType { get; }
}
