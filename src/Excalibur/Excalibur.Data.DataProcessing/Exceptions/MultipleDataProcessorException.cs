// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch;

namespace Excalibur.Data.DataProcessing.Exceptions;

/// <summary>
/// Represents an exception that is thrown when multiple implementations of <see cref="IDataProcessor" /> are found for a single record type.
/// </summary>
/// <remarks>
/// This exception ensures that the system has a one-to-one mapping between record types and data processor implementations, avoiding
/// ambiguity and conflicts during data processing.
/// </remarks>
public sealed class MultipleDataProcessorException : ApiException
{
	/// <summary>
	/// Initializes a new instance of the <see cref="MultipleDataProcessorException" /> class
	/// with a record type and optional status code.
	/// </summary>
	/// <param name="recordType"> The record type for which multiple data processors were found. </param>
	/// <param name="statusCode"> The HTTP status code for the exception. Defaults to 500. </param>
	/// <param name="innerException"> The inner exception, if any, that caused this exception. </param>
	/// <exception cref="ArgumentException">
	/// Thrown if <paramref name="recordType" /> is <c> null </c>, empty, or consists only of whitespace.
	/// </exception>
	public MultipleDataProcessorException(string recordType, int statusCode, Exception? innerException = null)
		: base(
			statusCode,
			$"Multiple {nameof(IDataProcessor)} implementations found for recordType {recordType}. Ensure that only one handler is registered per recordType.",
			innerException)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(recordType);

		RecordType = recordType;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="MultipleDataProcessorException" /> class
	/// with a record type, using the default status code of 500.
	/// </summary>
	/// <param name="recordType"> The record type for which multiple data processors were found. </param>
	/// <param name="innerException"> The inner exception, if any, that caused this exception. </param>
	/// <exception cref="ArgumentException">
	/// Thrown if <paramref name="recordType" /> is <c> null </c>, empty, or consists only of whitespace.
	/// </exception>
	public MultipleDataProcessorException(string recordType, Exception? innerException)
		: this(recordType, 500, innerException)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="MultipleDataProcessorException" /> class with default values.
	/// </summary>
	public MultipleDataProcessorException() : base()
	{
		RecordType = string.Empty;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="MultipleDataProcessorException" /> class with a status code, message, and inner exception.
	/// </summary>
	/// <param name="statusCode">The HTTP status code.</param>
	/// <param name="message">The error message.</param>
	/// <param name="innerException">The inner exception.</param>
	public MultipleDataProcessorException(int statusCode, string? message, Exception? innerException) : base(statusCode, message, innerException)
	{
		RecordType = string.Empty;
	}

	/// <summary>
	/// Gets the record type for which multiple processors were found.
	/// </summary>
	/// <value> A string representing the record type. </value>
	public string RecordType { get; }
}
