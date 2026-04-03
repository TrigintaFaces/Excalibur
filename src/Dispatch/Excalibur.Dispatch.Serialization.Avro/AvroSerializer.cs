// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Buffers;
using System.Diagnostics.CodeAnalysis;

using Avro;
using Avro.IO;
using Avro.Specific;

using Excalibur.Dispatch.Abstractions.Serialization;

namespace Excalibur.Dispatch.Serialization.Avro;

/// <summary>
/// Apache Avro implementation of <see cref="ISerializer"/>.
/// </summary>
/// <remarks>
/// <para>
/// Schema-based binary serializer optimized for streaming and Kafka scenarios.
/// Types must implement <see cref="ISpecificRecord"/> for Avro schema support.
/// </para>
/// <para>
/// <b>Serializer ID:</b> <see cref="SerializerIds.Avro"/> (5)
/// </para>
/// <para>
/// <b>Constraint:</b> Types must implement <see cref="ISpecificRecord"/>. Runtime checks
/// enforce this since <see cref="ISerializer"/> uses unconstrained generics.
/// </para>
/// </remarks>
[RequiresUnreferencedCode("Apache.Avro uses runtime schema compilation. AvroSerializer uses Activator.CreateInstance for ISpecificRecord deserialization.")]
[RequiresDynamicCode("Apache.Avro uses runtime schema compilation. AvroSerializer uses Activator.CreateInstance which requires dynamic code generation.")]
public sealed class AvroSerializer : ISerializer
{
	private readonly int _bufferSize;

	/// <summary>
	/// Initializes a new instance with default options.
	/// </summary>
	public AvroSerializer()
		: this(new AvroSerializationOptions())
	{
	}

	/// <summary>
	/// Initializes a new instance with the specified options.
	/// </summary>
	/// <param name="options">The Avro serialization options.</param>
	internal AvroSerializer(AvroSerializationOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);
		_bufferSize = options.BufferSize;
	}

	/// <inheritdoc />
	public string Name => "Avro";

	/// <inheritdoc />
	public string Version => typeof(Schema).Assembly
		.GetName().Version?.ToString() ?? "Unknown";

	/// <inheritdoc />
	public string ContentType => "avro/binary";

	/// <inheritdoc />
	public void Serialize<T>(T value, IBufferWriter<byte> bufferWriter)
	{
		ArgumentNullException.ThrowIfNull(value);
		ArgumentNullException.ThrowIfNull(bufferWriter);

		if (value is not ISpecificRecord record)
		{
			throw new InvalidOperationException(
				$"Type '{typeof(T).Name}' does not implement ISpecificRecord. Avro serialization requires ISpecificRecord types.");
		}

		try
		{
			using var stream = new MemoryStream(_bufferSize);
			var writer = new SpecificDatumWriter<ISpecificRecord>(record.Schema);
			var encoder = new BinaryEncoder(stream);
			writer.Write(record, encoder);
			encoder.Flush();

			var bytes = stream.ToArray();
			var span = bufferWriter.GetSpan(bytes.Length);
			bytes.CopyTo(span);
			bufferWriter.Advance(bytes.Length);
		}
		catch (AvroException ex)
		{
			throw SerializationException.Wrap<T>("serialize", ex);
		}
	}

	/// <inheritdoc cref="ISerializer.Deserialize{T}"/>
	public T Deserialize<T>(ReadOnlySpan<byte> data)
	{
		if (!typeof(ISpecificRecord).IsAssignableFrom(typeof(T)))
		{
			throw new InvalidOperationException(
				$"Type '{typeof(T).Name}' does not implement ISpecificRecord. Avro serialization requires ISpecificRecord types.");
		}

		try
		{
#pragma warning disable RS0030 // Activator.CreateInstance<T>() is required for Avro deserialization (ISpecificRecord requires instance creation)
			var instance = (ISpecificRecord)(object)Activator.CreateInstance<T>()!;
#pragma warning restore RS0030
			var reader = new SpecificDatumReader<ISpecificRecord>(instance.Schema, instance.Schema);
			using var stream = new MemoryStream(data.ToArray());
			var decoder = new BinaryDecoder(stream);
			var result = reader.Read(instance, decoder);
			return (T)result;
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

		if (value is not ISpecificRecord record)
		{
			throw new InvalidOperationException(
				$"Type '{type.Name}' does not implement ISpecificRecord. Avro serialization requires ISpecificRecord types.");
		}

		try
		{
			using var stream = new MemoryStream(_bufferSize);
			var writer = new SpecificDatumWriter<ISpecificRecord>(record.Schema);
			var encoder = new BinaryEncoder(stream);
			writer.Write(record, encoder);
			encoder.Flush();
			return stream.ToArray();
		}
		catch (AvroException ex)
		{
			throw SerializationException.WrapObject(type, "serialize", ex);
		}
	}

	/// <inheritdoc />
	public object DeserializeObject(ReadOnlySpan<byte> data, Type type)
	{
		ArgumentNullException.ThrowIfNull(type);

		if (!typeof(ISpecificRecord).IsAssignableFrom(type))
		{
			throw new InvalidOperationException(
				$"Type '{type.Name}' does not implement ISpecificRecord. Avro serialization requires ISpecificRecord types.");
		}

		try
		{
			var instance = CreateInstance(type);
			var reader = new SpecificDatumReader<ISpecificRecord>(instance.Schema, instance.Schema);
			using var stream = new MemoryStream(data.ToArray());
			var decoder = new BinaryDecoder(stream);
			return reader.Read(instance, decoder);
		}
		catch (SerializationException)
		{
			throw;
		}
		catch (Exception ex)
		{
			throw SerializationException.WrapObject(type, "deserialize", ex);
		}
	}

#pragma warning disable RS0030 // Activator.CreateInstance(Type) is required for runtime-typed Avro deserialization
	private static ISpecificRecord CreateInstance(
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] Type type)
	{
		return (ISpecificRecord)Activator.CreateInstance(type)!;
	}
#pragma warning restore RS0030
}
