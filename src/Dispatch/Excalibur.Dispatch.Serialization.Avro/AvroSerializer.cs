// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Buffers;
using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;

using Avro;
using Avro.IO;
using Avro.Specific;

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
[RequiresUnreferencedCode(
	"Apache.Avro uses runtime schema compilation. AvroSerializer uses Activator.CreateInstance for ISpecificRecord deserialization.")]
[RequiresDynamicCode(
	"Apache.Avro uses runtime schema compilation. AvroSerializer uses Activator.CreateInstance which requires dynamic code generation.")]
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

	// --- Avro single-object-encoding wire format (skew detection / fail-closed) ------------------------
	//
	// Every payload is framed with the Apache Avro single-object-encoding header (the STANDARD, not a
	// bespoke format): a 2-byte marker (0xC3 0x01) followed by the 8-byte little-endian CRC-64-AVRO
	// (Rabin) fingerprint of the WRITER schema. Avro binary is not self-describing, so on read this
	// fingerprint is what lets us detect a writer/reader schema skew. On a mismatch with no resolvable
	// writer schema we FAIL CLOSED (throw SchemaMismatchException) instead of positionally decoding
	// against the wrong schema — which would silently corrupt field values (AC-F4 "no silent corruption
	// on version skew"). The fingerprint prefix is also the shared substrate for future writer-schema
	// resolution (real schema evolution): the wire format does not change when that lands.

	private const byte SingleObjectMarker0 = 0xC3;
	private const byte SingleObjectMarker1 = 0x01;

	/// <summary>Length of the single-object-encoding header: 2-byte marker + 8-byte fingerprint.</summary>
	private const int HeaderLength = 10;

	/// <summary>
	/// Writes the Avro single-object-encoding header (marker + 8-byte LE writer-schema fingerprint)
	/// into <paramref name="destination"/>, which MUST be at least <see cref="HeaderLength"/> bytes.
	/// </summary>
	private static void WriteHeader(Schema writerSchema, Span<byte> destination)
	{
		destination[0] = SingleObjectMarker0;
		destination[1] = SingleObjectMarker1;
		var fingerprint = SchemaNormalization.ParsingFingerprint64(writerSchema);
		BinaryPrimitives.WriteInt64LittleEndian(destination.Slice(2, sizeof(long)), fingerprint);
	}

	/// <summary>
	/// Validates the single-object-encoding header on <paramref name="data"/> against the reader schema
	/// and returns the Avro payload (the bytes after the header). Fails closed: throws
	/// <see cref="SchemaMismatchException"/> if the header is missing/invalid, or if the writer-schema
	/// fingerprint does not match the reader's and no writer schema can be resolved.
	/// </summary>
	/// <remarks>
	/// The fingerprint-mismatch branch is the seam where writer-schema resolution (real schema
	/// evolution) will plug in: resolve the writer schema by fingerprint via the serializer registry,
	/// then decode with <c>SpecificDatumReader(writerSchema, readerSchema)</c>. Until that capability is
	/// committed, a mismatch fails closed rather than mis-decoding.
	/// </remarks>
	private static ReadOnlySpan<byte> ValidatePayload(ReadOnlySpan<byte> data, Schema readerSchema, Type type)
	{
		if (data.Length < HeaderLength || data[0] != SingleObjectMarker0 || data[1] != SingleObjectMarker1)
		{
			throw SchemaMismatchException.MissingHeader(type);
		}

		var writerFingerprint = BinaryPrimitives.ReadInt64LittleEndian(data.Slice(2, sizeof(long)));
		var readerFingerprint = SchemaNormalization.ParsingFingerprint64(readerSchema);

		if (writerFingerprint != readerFingerprint)
		{
			// Skew detected. Writer-schema resolution would be attempted here (registry lookup by
			// fingerprint) before failing; absent that capability, fail closed — never positional-decode.
			throw SchemaMismatchException.Skew(type, writerFingerprint, readerFingerprint);
		}

		return data[HeaderLength..];
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
			var span = bufferWriter.GetSpan(HeaderLength + bytes.Length);
			WriteHeader(record.Schema, span);
			bytes.CopyTo(span[HeaderLength..]);
			bufferWriter.Advance(HeaderLength + bytes.Length);
		}
		catch (Exception ex) when (ex is not SerializationException)
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
			var instance = (ISpecificRecord)Activator.CreateInstance<T>()!;
#pragma warning restore RS0030
			// Fail-closed skew detection: validate the writer-schema fingerprint before decoding.
			var payload = ValidatePayload(data, instance.Schema, typeof(T));
			var reader = new SpecificDatumReader<ISpecificRecord>(instance.Schema, instance.Schema);
			using var stream = new MemoryStream(payload.ToArray());
			var decoder = new BinaryDecoder(stream);
			var result = reader.Read(instance, decoder);
			return (T)result;
		}
		catch (SchemaMismatchException)
		{
			// Fail-closed signal — never wrap/swallow into a generic serialization error.
			throw;
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

			var payload = stream.ToArray();
			var result = new byte[HeaderLength + payload.Length];
			WriteHeader(record.Schema, result);
			payload.CopyTo(result, HeaderLength);
			return result;
		}
		catch (Exception ex) when (ex is not SerializationException)
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
			// Fail-closed skew detection: validate the writer-schema fingerprint before decoding.
			var payload = ValidatePayload(data, instance.Schema, type);
			var reader = new SpecificDatumReader<ISpecificRecord>(instance.Schema, instance.Schema);
			using var stream = new MemoryStream(payload.ToArray());
			var decoder = new BinaryDecoder(stream);
			return reader.Read(instance, decoder);
		}
		catch (SchemaMismatchException)
		{
			// Fail-closed signal — never wrap/swallow into a generic serialization error.
			throw;
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
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
		Type type)
	{
		return (ISpecificRecord)Activator.CreateInstance(type)!;
	}

#pragma warning restore RS0030
}
