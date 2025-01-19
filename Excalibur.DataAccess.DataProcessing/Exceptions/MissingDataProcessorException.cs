using Excalibur.Exceptions;

namespace Excalibur.DataAccess.DataProcessing.Exceptions;

/// <summary>
///     Represents an exception that is thrown when no <see cref="IDataProcessor" /> implementation is found for the specified record type.
/// </summary>
/// <remarks>
///     This exception is designed to handle cases where a record type cannot be processed due to the absence of a corresponding data
///     processor implementation in the system.
/// </remarks>
[Serializable]
public class MissingDataProcessorException : ApiException
{
	/// <summary>
	///     Initializes a new instance of the <see cref="MissingDataProcessorException" /> class.
	/// </summary>
	/// <param name="recordType"> The record type for which the data processor is missing. </param>
	/// <param name="statusCode"> The HTTP status code for the exception. Defaults to 500 if not provided. </param>
	/// <param name="message">
	///     The custom message for the exception. If not provided, a default message is generated using the record type.
	/// </param>
	/// <param name="innerException"> The inner exception, if any, that caused this exception. </param>
	/// <exception cref="ArgumentException">
	///     Thrown if <paramref name="recordType" /> is <c> null </c>, empty, or consists only of whitespace.
	/// </exception>
	public MissingDataProcessorException(string recordType, int? statusCode = null, string? message = null,
		Exception? innerException = null)
		: base(statusCode ?? 500, message ?? $"No {nameof(IDataProcessor)} implementation found for recordType {recordType}.",
			innerException)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(recordType);

		RecordType = recordType;
	}

	/// <summary>
	///     Gets the record type for which the data processor was not found.
	/// </summary>
	/// <value> A string representing the record type. </value>
	public string RecordType { get; protected set; }
}
