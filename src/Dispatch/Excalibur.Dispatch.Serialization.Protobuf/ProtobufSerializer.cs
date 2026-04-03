// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using System.Text;

using Excalibur.Dispatch.Abstractions.Serialization;

using Google.Protobuf;
using Google.Protobuf.Reflection;

namespace Excalibur.Dispatch.Serialization.Protobuf;

/// <summary>
/// Protocol Buffers implementation of <see cref="ISerializer"/>.
/// </summary>
/// <remarks>
/// <para>
/// Replaces ProtobufMessageSerializer. Opt-in serializer for Google Cloud Platform
/// and AWS interoperability.
/// </para>
/// <para>
/// <b>Serializer ID:</b> <see cref="SerializerIds.Protobuf"/> (4)
/// </para>
/// <para>
/// <b>Constraint:</b> Types must implement <see cref="IMessage"/>. Runtime checks
/// enforce this since <see cref="ISerializer"/> uses unconstrained generics.
/// </para>
/// </remarks>
[RequiresUnreferencedCode("ProtobufSerializer uses reflection (GetProperty) to access static Parser/Descriptor properties on Google.Protobuf generated types.")]
[RequiresDynamicCode("ProtobufSerializer uses reflection-based property access that may not be available in AOT scenarios.")]
public sealed class ProtobufSerializer : ISerializer
{
	private static readonly CompositeFormat TypeNotIMessageFormat =
		CompositeFormat.Parse("Type '{0}' does not implement IMessage. Protobuf serialization requires IMessage types.");

	private static readonly CompositeFormat NoParserFoundFormat =
		CompositeFormat.Parse("No static Parser property found on Protobuf type '{0}'.");

	private readonly ProtobufWireFormat _wireFormat;

	/// <summary>
	/// Initializes a new instance with default options.
	/// </summary>
	public ProtobufSerializer()
		: this(new ProtobufSerializationOptions())
	{
	}

	/// <summary>
	/// Initializes a new instance with the specified options.
	/// </summary>
	/// <param name="options">The Protobuf serialization options.</param>
	internal ProtobufSerializer(ProtobufSerializationOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);
		_wireFormat = options.WireFormat;
	}

	/// <inheritdoc />
	public string Name => "Protobuf";

	/// <inheritdoc />
	public string Version => typeof(IMessage).Assembly
		.GetName().Version?.ToString() ?? "Unknown";

	/// <inheritdoc />
	public string ContentType => _wireFormat == ProtobufWireFormat.Json
		? "application/json"
		: "application/x-protobuf";

	/// <inheritdoc />
	public void Serialize<T>(T value, IBufferWriter<byte> bufferWriter)
	{
		ArgumentNullException.ThrowIfNull(value);
		ArgumentNullException.ThrowIfNull(bufferWriter);

		if (value is not IMessage protoMessage)
		{
			throw new InvalidOperationException(
				string.Format(CultureInfo.InvariantCulture, TypeNotIMessageFormat, typeof(T).Name));
		}

		if (_wireFormat == ProtobufWireFormat.Json)
		{
			var json = JsonFormatter.Default.Format(protoMessage);
			var bytes = Encoding.UTF8.GetBytes(json);
			var span = bufferWriter.GetSpan(bytes.Length);
			bytes.CopyTo(span);
			bufferWriter.Advance(bytes.Length);
		}
		else
		{
			var bytes = protoMessage.ToByteArray();
			var span = bufferWriter.GetSpan(bytes.Length);
			bytes.CopyTo(span);
			bufferWriter.Advance(bytes.Length);
		}
	}

	/// <inheritdoc cref="ISerializer.Deserialize{T}"/>
	public T Deserialize<T>(ReadOnlySpan<byte> data)
	{
		if (!typeof(IMessage).IsAssignableFrom(typeof(T)))
		{
			throw new InvalidOperationException(
				string.Format(CultureInfo.InvariantCulture, TypeNotIMessageFormat, typeof(T).Name));
		}

		try
		{
			if (_wireFormat == ProtobufWireFormat.Json)
			{
				return DeserializeJson<T>(data);
			}

			return DeserializeBinary<T>(data.ToArray());
		}
		catch (SerializationException)
		{
			throw;
		}
		catch (Exception ex)
		{
			throw SerializationException.Wrap<T>("deserialize", ex);
		}
	}

	/// <inheritdoc />
	public byte[] SerializeObject(object value, Type type)
	{
		ArgumentNullException.ThrowIfNull(value);
		ArgumentNullException.ThrowIfNull(type);

		if (value is not IMessage protoMessage)
		{
			throw new InvalidOperationException(
				string.Format(CultureInfo.InvariantCulture, TypeNotIMessageFormat, type.Name));
		}

		if (_wireFormat == ProtobufWireFormat.Json)
		{
			var json = JsonFormatter.Default.Format(protoMessage);
			return Encoding.UTF8.GetBytes(json);
		}

		return protoMessage.ToByteArray();
	}

	/// <inheritdoc />
	public object DeserializeObject(ReadOnlySpan<byte> data, Type type)
	{
		ArgumentNullException.ThrowIfNull(type);

		if (!typeof(IMessage).IsAssignableFrom(type))
		{
			throw new InvalidOperationException(
				string.Format(CultureInfo.InvariantCulture, TypeNotIMessageFormat, type.Name));
		}

		if (_wireFormat == ProtobufWireFormat.Json)
		{
			var json = Encoding.UTF8.GetString(data);
			var descriptor = GetDescriptor(type);
			return JsonParser.Default.Parse(json, descriptor);
		}

		var parser = GetParser(type);
		return parser.ParseFrom(data.ToArray());
	}

	private static T DeserializeBinary<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T>(byte[] data)
	{
		var parser = GetParser(typeof(T));
		var result = parser.ParseFrom(data);
		return (T)result;
	}

	private static T DeserializeJson<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T>(ReadOnlySpan<byte> data)
	{
		var json = Encoding.UTF8.GetString(data);
		var descriptor = GetDescriptor(typeof(T));
		var result = JsonParser.Default.Parse(json, descriptor);
		return (T)(object)result;
	}

	private static MessageParser GetParser(
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] Type messageType)
	{
		var parserProperty = messageType.GetProperty("Parser", BindingFlags.Public | BindingFlags.Static);

		return parserProperty?.GetValue(null) as MessageParser
			?? throw new InvalidOperationException(
				string.Format(CultureInfo.InvariantCulture, NoParserFoundFormat, messageType.Name));
	}

	private static MessageDescriptor GetDescriptor(
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] Type messageType)
	{
		var descriptorProperty = messageType.GetProperty("Descriptor", BindingFlags.Public | BindingFlags.Static);

		return descriptorProperty?.GetValue(null) as MessageDescriptor
			?? throw new InvalidOperationException(
				string.Format(CultureInfo.InvariantCulture, NoParserFoundFormat, messageType.Name));
	}
}
