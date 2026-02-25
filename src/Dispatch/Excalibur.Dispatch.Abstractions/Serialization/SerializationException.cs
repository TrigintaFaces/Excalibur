// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0



namespace Excalibur.Dispatch.Abstractions.Serialization;

/// <summary>
/// The operation being performed when a serialization exception occurred.
/// </summary>
public enum SerializationOperation
{
	/// <summary>
	/// The operation is unknown or not applicable.
	/// </summary>
	None = 0,

	/// <summary>
	/// A serialization (write) operation.
	/// </summary>
	Serialize,

	/// <summary>
	/// A deserialization (read) operation.
	/// </summary>
	Deserialize
}

/// <summary>
/// Exception thrown when serialization or deserialization operations fail.
/// </summary>
/// <remarks>
/// <para>
/// This exception is thrown by <see cref="IPayloadSerializer"/> and serializer implementations
/// when they encounter errors during serialization or deserialization operations.
/// </para>
/// <para>
/// This exception extends <see cref="ApiException"/> to provide API-compatible error responses
/// with RFC 7807 problem details support.
/// </para>
/// <para>
/// Common scenarios that may cause this exception:
/// </para>
/// <list type="bullet">
///   <item>Unknown or unregistered serializer ID in payload magic byte</item>
///   <item>Corrupt or invalid payload data</item>
///   <item>Type mismatch during deserialization</item>
///   <item>Missing required serializer attributes on types</item>
///   <item>Null values where not supported</item>
/// </list>
/// </remarks>
[Serializable]
public sealed class SerializationException : ApiException
{
	private const int SerializationStatusCode = 400;

	/// <summary>
	/// Initializes a new instance of the <see cref="SerializationException"/> class.
	/// </summary>
	public SerializationException()
		: base(ErrorMessages.SerializationErrorOccurred)
	{
		StatusCode = SerializationStatusCode;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="SerializationException"/> class
	/// with a specified error message.
	/// </summary>
	/// <param name="message">The message that describes the error.</param>
	public SerializationException(string message)
		: base(message)
	{
		StatusCode = SerializationStatusCode;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="SerializationException"/> class
	/// with a specified error message and a reference to the inner exception.
	/// </summary>
	/// <param name="message">The message that describes the error.</param>
	/// <param name="innerException">The exception that is the cause of the current exception.</param>
	public SerializationException(string message, Exception innerException)
		: base(message, innerException)
	{
		StatusCode = SerializationStatusCode;
	}

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
	public string? SerializerName { get; init; }

	/// <summary>
	/// Gets or sets the operation being performed when the exception occurred.
	/// </summary>
	public SerializationOperation Operation { get; init; }

	/// <summary>
	/// Creates a <see cref="SerializationException"/> for an unknown serializer ID.
	/// </summary>
	/// <param name="serializerId">The unknown serializer ID.</param>
	/// <param name="registeredSerializers">Description of registered serializers for the error message.</param>
	/// <returns>A new <see cref="SerializationException"/> instance.</returns>
	public static SerializationException UnknownSerializerId(byte serializerId, string? registeredSerializers = null)
	{
		var message = $"Unknown serializer ID: 0x{serializerId:X2}.";
		if (!string.IsNullOrEmpty(registeredSerializers))
		{
			message += $" Registered serializers: {registeredSerializers}";
		}

		return new SerializationException(message) { SerializerId = serializerId };
	}

	/// <summary>
	/// Creates a <see cref="SerializationException"/> for an empty payload.
	/// </summary>
	/// <returns>A new <see cref="SerializationException"/> instance.</returns>
	public static SerializationException EmptyPayload()
		=> new(ErrorMessages.CannotDeserializeEmptyPayload);

	/// <summary>
	/// Creates a <see cref="SerializationException"/> for a null deserialization result.
	/// </summary>
	/// <typeparam name="T">The type that was being deserialized.</typeparam>
	/// <returns>A new <see cref="SerializationException"/> instance.</returns>
	public static SerializationException NullResult<T>()
		=> new($"Deserialization returned null for type '{typeof(T).FullName}'.") { TargetType = typeof(T) };

	/// <summary>
	/// Creates a <see cref="SerializationException"/> for a null deserialization result with runtime type.
	/// </summary>
	/// <param name="type">The type that was being deserialized.</param>
	/// <returns>A new <see cref="SerializationException"/> instance.</returns>
	public static SerializationException NullResultForType(Type type)
		=> new($"Deserialization returned null for type '{type.FullName}'.") { TargetType = type };

	/// <summary>
	/// Creates a <see cref="SerializationException"/> wrapping an inner exception.
	/// </summary>
	/// <typeparam name="T">The type that was being serialized or deserialized.</typeparam>
	/// <param name="operation">The operation being performed ("serialization" or "deserialization").</param>
	/// <param name="innerException">The inner exception.</param>
	/// <returns>A new <see cref="SerializationException"/> instance.</returns>
	public static SerializationException Wrap<T>(string operation, Exception innerException)
		=> new($"Failed to {operation} type '{typeof(T).FullName}': {innerException.Message}", innerException)
		{
			TargetType = typeof(T)
		};

	/// <summary>
	/// Creates a <see cref="SerializationException"/> wrapping an inner exception for runtime type serialization.
	/// </summary>
	/// <param name="type">The runtime type that was being serialized.</param>
	/// <param name="operation">The operation being performed ("serialize" or "deserialize").</param>
	/// <param name="innerException">The inner exception.</param>
	/// <returns>A new <see cref="SerializationException"/> instance.</returns>
	public static SerializationException WrapObject(Type type, string operation, Exception innerException)
		=> new($"Failed to {operation} type '{type.FullName}': {innerException.Message}", innerException)
		{
			TargetType = type
		};

	/// <summary>
	/// Creates a <see cref="SerializationException"/> for an unknown payload format.
	/// </summary>
	/// <remarks>
	/// This is thrown when the payload format cannot be identified through hybrid detection:
	/// not our magic bytes, not Confluent format, and not raw JSON.
	/// </remarks>
	/// <param name="firstByte">The first byte of the unknown payload.</param>
	/// <returns>A new <see cref="SerializationException"/> instance.</returns>
	public static SerializationException UnknownFormat(byte firstByte)
		=> new($"Unknown payload format. First byte: 0x{firstByte:X2}. " +
			   $"Expected: Magic byte (0x01-0xFE), Confluent (0x00), or JSON (0x7B/0x5B).")
		{
			SerializerId = firstByte
		};

	/// <summary>
	/// Creates a <see cref="SerializationException"/> for an unsupported payload format.
	/// </summary>
	/// <remarks>
	/// This is thrown when the format is recognized but not supported,
	/// such as Confluent Schema Registry with Avro/Protobuf payloads.
	/// </remarks>
	/// <param name="formatName">The name of the unsupported format.</param>
	/// <param name="suggestion">Suggestion for how to handle this format.</param>
	/// <returns>A new <see cref="SerializationException"/> instance.</returns>
	public static SerializationException FormatNotSupported(string formatName, string? suggestion = null)
	{
		var message = $"Format '{formatName}' is detected but not supported.";
		if (!string.IsNullOrEmpty(suggestion))
		{
			message += $" {suggestion}";
		}

		return new SerializationException(message);
	}

	/// <summary>
	/// Creates a <see cref="SerializationException"/> for a serialization failure with full context.
	/// </summary>
	/// <param name="targetType">The type being serialized.</param>
	/// <param name="serializerId">The ID of the serializer.</param>
	/// <param name="serializerName">The name of the serializer.</param>
	/// <param name="innerException">The inner exception that caused the failure.</param>
	/// <returns>A new <see cref="SerializationException"/> instance.</returns>
	public static SerializationException SerializationFailed(
		Type targetType,
		byte serializerId,
		string serializerName,
		Exception innerException)
		=> new($"Failed to serialize type '{targetType.FullName}' using {serializerName}: {innerException.Message}", innerException)
		{
			TargetType = targetType,
			SerializerId = serializerId,
			SerializerName = serializerName,
			Operation = SerializationOperation.Serialize
		};

	/// <summary>
	/// Creates a <see cref="SerializationException"/> for a deserialization failure with full context.
	/// </summary>
	/// <param name="targetType">The type being deserialized.</param>
	/// <param name="serializerId">The ID of the serializer.</param>
	/// <param name="serializerName">The name of the serializer.</param>
	/// <param name="innerException">The inner exception that caused the failure.</param>
	/// <returns>A new <see cref="SerializationException"/> instance.</returns>
	public static SerializationException DeserializationFailed(
		Type targetType,
		byte serializerId,
		string serializerName,
		Exception innerException)
		=> new($"Failed to deserialize to type '{targetType.FullName}' using {serializerName}: {innerException.Message}", innerException)
		{
			TargetType = targetType,
			SerializerId = serializerId,
			SerializerName = serializerName,
			Operation = SerializationOperation.Deserialize
		};
}
