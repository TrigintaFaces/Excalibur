// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Data.DataProcessing.Exceptions;

/// <summary>
/// Represents an exception that occurs when an invalid <see cref="IDataProcessor" /> implementation is detected.
/// </summary>
[Serializable]
public sealed class InvalidDataProcessorException : ApiException
{
	/// <summary>
	/// The default HTTP status code for this exception.
	/// </summary>
	public const int DefaultStatusCode = 500;

	/// <summary>
	/// The default error message for this exception.
	/// </summary>
	public const string DefaultMessage = $"{nameof(IDataProcessor)} implementation found but a record type could not be ascertained.";

	/// <summary>
	/// Initializes a new instance of the <see cref="InvalidDataProcessorException" /> class.
	/// </summary>
	/// <param name="processorType"> The type of the processor causing the exception. </param>
	/// <param name="message"> A custom error message. Defaults to the <see cref="DefaultMessage" /> if not provided. </param>
	/// <param name="innerException"> The inner exception causing this exception. </param>
	public InvalidDataProcessorException(Type? processorType = null, string? message = null, Exception? innerException = null)
		: base(DefaultStatusCode, message ?? GenerateDefaultMessage(processorType), innerException)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="InvalidDataProcessorException" /> class with a custom status code.
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

	public InvalidDataProcessorException() : base()
	{
	}

	public InvalidDataProcessorException(string message) : base(message)
	{
	}

	public InvalidDataProcessorException(string message, Exception? innerException) : base(message, innerException)
	{
	}

	public InvalidDataProcessorException(int statusCode, string? message, Exception? innerException) : base(statusCode, message, innerException)
	{
	}

	/// <summary>
	/// Generates a default error message using the provided processor type.
	/// </summary>
	/// <param name="processorType"> The processor type causing the exception. </param>
	/// <returns> A detailed error message. </returns>
	private static string GenerateDefaultMessage(Type? processorType) =>
		processorType != null
			? $"{nameof(IDataProcessor)} implementation '{processorType.FullName}' is invalid: record type could not be ascertained."
			: DefaultMessage;
}
