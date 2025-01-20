using Excalibur.Core.Exceptions;

namespace Excalibur.DataAccess.DataProcessing.Exceptions;

/// <summary>
///     Represents an exception that is thrown when multiple implementations of <see cref="IDataProcessor" /> are found for a single record type.
/// </summary>
/// <remarks>
///     This exception ensures that the system has a one-to-one mapping between record types and data processor implementations, avoiding
///     ambiguity and conflicts during data processing.
/// </remarks>
[Serializable]
public class MultipleDataProcessorException : ApiException
{
	/// <summary>
	///     Initializes a new instance of the <see cref="MultipleDataProcessorException" /> class.
	/// </summary>
	/// <param name="recordType"> The record type for which multiple data processors were found. </param>
	/// <param name="statusCode"> The HTTP status code for the exception. Defaults to 500 if not provided. </param>
	/// <param name="message">
	///     The custom message for the exception. If not provided, a default message is generated using the record type.
	/// </param>
	/// <param name="innerException"> The inner exception, if any, that caused this exception. </param>
	/// <exception cref="ArgumentException">
	///     Thrown if <paramref name="recordType" /> is <c> null </c>, empty, or consists only of whitespace.
	/// </exception>
	public MultipleDataProcessorException(string recordType, int? statusCode = null, string? message = null,
		Exception? innerException = null)
		: base(statusCode ?? 500,
			message ??
			$"Multiple {nameof(IDataProcessor)} implementations found for recordType {recordType}. Ensure that only one handler is registered per recordType.",
			innerException)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(recordType);

		RecordType = recordType;
	}

	/// <summary>
	///     Gets the record type for which multiple processors were found.
	/// </summary>
	/// <value> A string representing the record type. </value>
	public string RecordType { get; protected set; }
}
