using Excalibur.Core.Exceptions;

namespace Excalibur.DataAccess.SqlServer.Cdc.Exceptions;

/// <summary>
///     Exception thrown when no implementation of <see cref="IDataChangeHandler" /> is found for a specified table.
/// </summary>
[Serializable]
public class CdcMissingTableHandlerException : ApiException
{
	/// <summary>
	///     Initializes a new instance of the <see cref="CdcMissingTableHandlerException" /> class.
	/// </summary>
	/// <param name="tableName"> The name of the table for which a handler is missing. </param>
	/// <param name="statusCode"> The HTTP status code associated with the exception. Defaults to 500 if not specified. </param>
	/// <param name="message">
	///     The custom error message. Defaults to a message indicating that no handler is found for the specified table.
	/// </param>
	/// <param name="innerException"> The inner exception that caused the current exception, if applicable. </param>
	/// <exception cref="ArgumentException"> Thrown if <paramref name="tableName" /> is null, empty, or consists only of whitespace. </exception>
	public CdcMissingTableHandlerException(string tableName, int? statusCode = null, string? message = null,
		Exception? innerException = null)
		: base(statusCode ?? 500, message ?? $"No {nameof(IDataChangeHandler)} implementation found for table {tableName}.", innerException)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(tableName, nameof(tableName));

		TableName = tableName;
	}

	/// <summary>
	///     Gets the name of the table for which the handler is missing.
	/// </summary>
	public string TableName { get; protected set; }
}
