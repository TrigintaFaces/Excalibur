// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;

using SerializationOperationType = Excalibur.Dispatch.Abstractions.Serialization.SerializationOperation;

namespace Excalibur.Dispatch.Exceptions;

/// <summary>
/// Dispatch-specific serialization exception that extends <see cref="DispatchException"/>.
/// </summary>
/// <remarks>
/// <para>
/// This exception provides Dispatch-specific error handling features on top of the base
/// <see cref="Abstractions.Serialization.SerializationException"/>. It adds:
/// </para>
/// <list type="bullet">
///   <item>Error code categorization</item>
///   <item>Distributed tracing support (TraceId, SpanId, CorrelationId)</item>
///   <item>Fluent configuration API (WithContext, WithCorrelationId, etc.)</item>
///   <item>Extended problem details support</item>
/// </list>
/// <para>
/// For code in the Dispatch package or consumers who need DispatchException features,
/// use this class. For code in Excalibur.Dispatch.Abstractions or serializer packages that only
/// need basic serialization exception functionality, use
/// <see cref="Abstractions.Serialization.SerializationException"/>.
/// </para>
/// </remarks>
[Serializable]
public sealed class DispatchSerializationException : DispatchException
{
	/// <summary>
	/// Initializes a new instance of the <see cref="DispatchSerializationException" /> class.
	/// </summary>
	public DispatchSerializationException()
		: base(ErrorCodes.SerializationFailed, ErrorMessages.SerializationErrorOccurred) =>
		StatusCode = 400;

	/// <summary>
	/// Initializes a new instance of the <see cref="SerializationException" /> class with a specified error message.
	/// </summary>
	/// <param name="message">The error message that explains the reason for the exception.</param>
	public DispatchSerializationException(string message)
		: base(ErrorCodes.SerializationFailed, message) =>
		StatusCode = 400;

	/// <summary>
	/// Initializes a new instance of the <see cref="DispatchSerializationException"/> class
	/// with a specified error message and a reference to the inner exception.
	/// </summary>
	/// <param name="message">The message that describes the error.</param>
	/// <param name="innerException">The exception that is the cause of the current exception.</param>
	public DispatchSerializationException(string message, Exception? innerException)
		: base(ErrorCodes.SerializationFailed, message, innerException) =>
		StatusCode = 400;

	/// <summary>
	/// Initializes a new instance of the <see cref="DispatchSerializationException"/> class
	/// with a specified error code and message.
	/// </summary>
	/// <param name="errorCode">The error code associated with this exception.</param>
	/// <param name="message">The error message that explains the reason for the exception.</param>
	public DispatchSerializationException(string errorCode, string message)
		: base(errorCode, message) =>
		StatusCode = 400;

	/// <summary>
	/// Initializes a new instance of the <see cref="DispatchSerializationException"/> class
	/// with a specified error code, message, and inner exception.
	/// </summary>
	/// <param name="errorCode">The error code associated with this exception.</param>
	/// <param name="message">The error message that explains the reason for the exception.</param>
	/// <param name="innerException">The exception that is the cause of the current exception.</param>
	public DispatchSerializationException(string errorCode, string message, Exception? innerException)
		: base(errorCode, message, innerException) =>
		StatusCode = 400;

	/// <summary>
	/// Gets or sets the serializer ID associated with this exception, if applicable.
	/// </summary>
	/// <remarks>
	/// This property is set when the exception is related to a specific serializer,
	/// such as when an unknown serializer ID is encountered in a payload.
	/// </remarks>
	public byte? SerializerId { get; init; }

	/// <summary>
	/// Gets or sets the type that was being serialized or deserialized when the exception occurred.
	/// </summary>
	public Type? TargetType { get; init; }

	/// <summary>
	/// Gets or sets the name of the serializer that threw the exception.
	/// </summary>
	/// <remarks>
	/// When provided, the value must not be empty or whitespace-only.
	/// Use <see langword="null"/> to indicate an unknown serializer.
	/// </remarks>
	/// <exception cref="ArgumentException">Thrown when set to an empty or whitespace-only string.</exception>
	public string? SerializerName
	{
		get => _serializerName;
		init
		{
			if (value is not null && string.IsNullOrWhiteSpace(value))
			{
				throw new ArgumentException("SerializerName must not be empty or whitespace when provided.", nameof(SerializerName));
			}

			_serializerName = value;
		}
	}

	/// <summary>
	/// Gets or sets the operation being performed when the exception occurred.
	/// </summary>
	public SerializationOperationType Operation { get; init; }

	private readonly string? _serializerName;
}
