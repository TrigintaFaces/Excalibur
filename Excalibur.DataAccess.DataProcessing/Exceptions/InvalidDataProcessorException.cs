using Excalibur.Core.Exceptions;

namespace Excalibur.DataAccess.DataProcessing.Exceptions;

/// <summary>
///     Represents an exception that occurs when an invalid <see cref="IDataProcessor" /> implementation is detected.
/// </summary>
[Serializable]
public class InvalidDataProcessorException : ApiException
{
	/// <summary>
	///     The default HTTP status code for this exception.
	/// </summary>
	public const int DefaultStatusCode = 500;

	/// <summary>
	///     The default error message for this exception.
	/// </summary>
	public const string DefaultMessage = $"{nameof(IDataProcessor)} implementation found but a record type could not be ascertained.";

	/// <summary>
	///     Initializes a new instance of the <see cref="InvalidDataProcessorException" /> class.
	/// </summary>
	/// <param name="processorType"> The type of the processor causing the exception. </param>
	/// <param name="message"> A custom error message. Defaults to the <see cref="DefaultMessage" /> if not provided. </param>
	/// <param name="innerException"> The inner exception causing this exception. </param>
	public InvalidDataProcessorException(Type? processorType = null, string? message = null, Exception? innerException = null)
		: base(DefaultStatusCode, message ?? GenerateDefaultMessage(processorType), innerException)
	{
	}

	/// <summary>
	///     Initializes a new instance of the <see cref="InvalidDataProcessorException" /> class with a custom status code.
	/// </summary>
	/// <param name="statusCode"> The HTTP status code associated with the exception. </param>
	/// <param name="processorType"> The type of the processor causing the exception. </param>
	/// <param name="message"> A custom error message. Defaults to the <see cref="DefaultMessage" /> if not provided. </param>
	/// <param name="innerException"> The inner exception causing this exception. </param>
	public InvalidDataProcessorException(int statusCode, Type? processorType = null, string? message = null,
		Exception? innerException = null)
		: base(statusCode, message ?? GenerateDefaultMessage(processorType), innerException)
	{
	}

	/// <summary>
	///     Generates a default error message using the provided processor type.
	/// </summary>
	/// <param name="processorType"> The processor type causing the exception. </param>
	/// <returns> A detailed error message. </returns>
	private static string GenerateDefaultMessage(Type? processorType) =>
		processorType != null
			? $"{nameof(IDataProcessor)} implementation '{processorType.FullName}' is invalid: record type could not be ascertained."
			: DefaultMessage;
}
