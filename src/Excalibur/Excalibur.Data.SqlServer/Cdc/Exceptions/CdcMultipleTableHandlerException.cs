// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Data.SqlServer.Cdc;

/// <summary>
/// Exception thrown when multiple implementations of <see cref="IDataChangeHandler" /> are found for a specified table.
/// </summary>
[Serializable]
public class CdcMultipleTableHandlerException : ApiException
{
	/// <summary>
	/// Initializes a new instance of the <see cref="CdcMultipleTableHandlerException" /> class.
	/// </summary>
	/// <param name="tableName"> The name of the table with multiple registered handlers. </param>
	/// <param name="statusCode"> The HTTP status code associated with the exception. Defaults to 500 if not specified. </param>
	/// <param name="message">
	/// A custom error message. Defaults to a message indicating that multiple handlers were found for the specified table.
	/// </param>
	/// <param name="innerException"> The inner exception that caused the current exception, if applicable. </param>
	/// <exception cref="ArgumentException"> Thrown if <paramref name="tableName" /> is null, empty, or consists only of whitespace. </exception>
	public CdcMultipleTableHandlerException(
		string tableName,
		int? statusCode = null,
		string? message = null,
		Exception? innerException = null)
		: base(
			statusCode ?? 500,
			message
			??
			$"Multiple {nameof(IDataChangeHandler)} implementations found for table {tableName}. Ensure that only one handler is registered per table.",
			innerException)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(tableName);

		TableName = tableName;
	}

	public CdcMultipleTableHandlerException() : base()
	{
	}

	public CdcMultipleTableHandlerException(string message) : base(message)
	{
	}

	public CdcMultipleTableHandlerException(string message, Exception? innerException) : base(message, innerException)
	{
	}

	public CdcMultipleTableHandlerException(int statusCode, string? message, Exception? innerException) : base(statusCode, message, innerException)
	{
	}

	/// <summary>
	/// Gets or sets the name of the table for which multiple handlers were found.
	/// </summary>
	/// <value>
	/// The name of the table for which multiple handlers were found.
	/// </value>
	public string TableName { get; protected set; }
}
