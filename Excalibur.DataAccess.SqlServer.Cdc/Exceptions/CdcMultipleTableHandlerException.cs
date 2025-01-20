using Excalibur.Core.Exceptions;

namespace Excalibur.DataAccess.SqlServer.Cdc.Exceptions;

/// <summary>
///     Exception thrown when multiple implementations of <see cref="IDataChangeHandler" /> are found for a specified table.
/// </summary>
[Serializable]
public class CdcMultipleTableHandlerException : ApiException
{
	/// <summary>
	///     Initializes a new instance of the <see cref="CdcMultipleTableHandlerException" /> class.
	/// </summary>
	/// <param name="tableName"> The name of the table with multiple registered handlers. </param>
	/// <param name="statusCode"> The HTTP status code associated with the exception. Defaults to 500 if not specified. </param>
	/// <param name="message">
	///     A custom error message. Defaults to a message indicating that multiple handlers were found for the specified table.
	/// </param>
	/// <param name="innerException"> The inner exception that caused the current exception, if applicable. </param>
	/// <exception cref="ArgumentException"> Thrown if <paramref name="tableName" /> is null, empty, or consists only of whitespace. </exception>
	public CdcMultipleTableHandlerException(string tableName, int? statusCode = null, string? message = null,
		Exception? innerException = null)
		: base(statusCode ?? 500,
			message ??
			$"Multiple {nameof(IDataChangeHandler)} implementations found for table {tableName}. Ensure that only one handler is registered per table.",
			innerException)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(tableName, nameof(tableName));

		TableName = tableName;
	}

	/// <summary>
	///     Gets the name of the table for which multiple handlers were found.
	/// </summary>
	public string TableName { get; protected set; }
}
