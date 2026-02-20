// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using System.Text;

using Excalibur.Dispatch.Abstractions.Serialization;

using Google.Protobuf;

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Serialization.Protobuf;

/// <summary>
/// Protocol Buffers implementation of message serializer.
/// </summary>
/// <remarks>
/// This is an opt-in serializer package for Google Cloud Platform and AWS interoperability.
/// Per R0.14, R9.46: Protobuf is NOT used in Excalibur.Dispatch core; it is isolated in this opt-in package.
/// </remarks>
public sealed class ProtobufMessageSerializer : IMessageSerializer
{
	private static readonly CompositeFormat TypeNotIMessageSerializeFormat =
		CompositeFormat.Parse(Resources.ProtobufMessageSerializer_TypeNotIMessageSerialize);

	private static readonly CompositeFormat TypeNotIMessageDeserializeFormat =
		CompositeFormat.Parse(Resources.ProtobufMessageSerializer_TypeNotIMessageDeserialize);

	private static readonly CompositeFormat UnsupportedWireFormatFormat =
		CompositeFormat.Parse(Resources.ProtobufMessageSerializer_UnsupportedWireFormat);

	private static readonly CompositeFormat FailedToDeserializeFormat =
		CompositeFormat.Parse(Resources.ProtobufMessageSerializer_FailedToDeserialize);

	private static readonly CompositeFormat NoParserFoundFormat =
		CompositeFormat.Parse(Resources.ProtobufMessageSerializer_NoParserFound);

	private static readonly CompositeFormat ParseJsonNotFoundFormat =
		CompositeFormat.Parse(Resources.ProtobufMessageSerializer_ParseJsonNotFound);

	private readonly ProtobufSerializationOptions _options;

	/// <summary>
	/// Initializes a new instance of the <see cref="ProtobufMessageSerializer" /> class.
	/// </summary>
	/// <param name="options"> The Protocol Buffers serialization options. </param>
	public ProtobufMessageSerializer(IOptions<ProtobufSerializationOptions> options)
	{
		ArgumentNullException.ThrowIfNull(options);
		_options = options.Value;
	}

	/// <inheritdoc />
	public string SerializerName => "Protobuf";

	/// <inheritdoc />
	public string SerializerVersion => "1.0.0";

	/// <inheritdoc />
	[RequiresUnreferencedCode("Serialization may require unreferenced code for type-specific handling.")]
	[RequiresDynamicCode("Serialization may require dynamic code generation for type-specific handling.")]
	public byte[] Serialize<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(T message)
	{
		ArgumentNullException.ThrowIfNull(message);

		if (message is not IMessage protoMessage)
		{
			throw new InvalidOperationException(
				string.Format(
					CultureInfo.CurrentCulture,
					TypeNotIMessageSerializeFormat,
					typeof(T).Name));
		}

		return _options.WireFormat switch
		{
			ProtobufWireFormat.Binary => protoMessage.ToByteArray(),
			ProtobufWireFormat.Json => Encoding.UTF8.GetBytes(protoMessage.ToString() ?? string.Empty),
			_ => throw new InvalidOperationException(
				string.Format(
					CultureInfo.CurrentCulture,
					UnsupportedWireFormatFormat,
					_options.WireFormat)),
		};
	}

	/// <inheritdoc />
	[RequiresUnreferencedCode("Deserialization may require unreferenced code for type-specific handling.")]
	[RequiresDynamicCode("Deserialization may require dynamic code generation for type-specific handling.")]
	public T Deserialize<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(byte[] data)
	{
		ArgumentNullException.ThrowIfNull(data);

		if (data.Length == 0)
		{
			throw new ArgumentException(Resources.ProtobufMessageSerializer_DataEmpty, nameof(data));
		}

		// Protocol Buffers requires a specific parser for each message type This is a limitation - in practice, applications would need to
		// register parsers or use a registry pattern for different message types
		if (!typeof(IMessage).IsAssignableFrom(typeof(T)))
		{
			throw new InvalidOperationException(
				string.Format(
					CultureInfo.CurrentCulture,
					TypeNotIMessageDeserializeFormat,
					typeof(T).Name));
		}

		try
		{
			var result = _options.WireFormat switch
			{
				ProtobufWireFormat.Binary => DeserializeBinary<T>(data),
				ProtobufWireFormat.Json => DeserializeJson<T>(data),
				_ => throw new InvalidOperationException(
					string.Format(
						CultureInfo.CurrentCulture,
						UnsupportedWireFormatFormat,
						_options.WireFormat)),
			};

			return result ?? throw new InvalidOperationException(Resources.ProtobufMessageSerializer_DeserializedMessageNull);
		}
		catch (InvalidProtocolBufferException ex)
		{
			throw new InvalidOperationException(
				string.Format(
					CultureInfo.CurrentCulture,
					FailedToDeserializeFormat,
					ex.Message),
				ex);
		}
		catch (TargetInvocationException ex)
		{
			// DeserializeJson uses reflection (Invoke) which wraps exceptions in TargetInvocationException.
			// Unwrap the inner exception for a clearer error message.
			var inner = ex.InnerException ?? ex;
			throw new InvalidOperationException(
				string.Format(
					CultureInfo.CurrentCulture,
					FailedToDeserializeFormat,
					inner.Message),
				inner);
		}
	}

	private static T DeserializeBinary<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T>(byte[] data)
	{
		var messageType = typeof(T);
		var parserProperty =
			messageType.GetProperty("Parser", BindingFlags.Public | BindingFlags.Static);

		if (parserProperty?.GetValue(null) is not MessageParser parser)
		{
			throw new InvalidOperationException(
				string.Format(
					CultureInfo.CurrentCulture,
					NoParserFoundFormat,
					messageType.Name));
		}

		// Call ParseFrom directly via the base MessageParser API instead of through reflection.
		// This avoids TargetInvocationException wrapping and allows InvalidProtocolBufferException
		// to propagate directly to the caller's catch block.
		var result = parser.ParseFrom(data);
		return (T)result;
	}

	[UnconditionalSuppressMessage(
		"Trimming",
		"IL2075:'this' argument does not satisfy 'DynamicallyAccessedMemberTypes.PublicMethods' in call to target method",
		Justification = "Protobuf parser types are preserved and have required methods")]
	private static T DeserializeJson<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T>(byte[] data)
	{
		var json = Encoding.UTF8.GetString(data);
		var messageType = typeof(T);
		var parserProperty =
			messageType.GetProperty("Parser", BindingFlags.Public | BindingFlags.Static);

		if (parserProperty?.GetValue(null) is not MessageParser parser)
		{
			throw new InvalidOperationException(
				string.Format(
					CultureInfo.CurrentCulture,
					NoParserFoundFormat,
					messageType.Name));
		}

		var parseJsonMethod = parser.GetType().GetMethod("ParseJson", [typeof(string)]) ??
							  throw new InvalidOperationException(
								  string.Format(
									  CultureInfo.CurrentCulture,
									  ParseJsonNotFoundFormat,
									  messageType.Name));

		var result = parseJsonMethod.Invoke(parser, [json]);
		return (T)result!;
	}
}
