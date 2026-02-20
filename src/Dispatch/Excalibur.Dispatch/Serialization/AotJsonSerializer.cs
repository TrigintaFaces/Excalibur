// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR
// AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

using Excalibur.Dispatch.Abstractions.Serialization;

namespace Excalibur.Dispatch.Serialization;

/// <summary>
/// AOT-compatible JSON serializer using System.Text.Json source generation. This serializer eliminates reflection and provides zero ILLink warnings.
/// </summary>
/// <remarks>
/// This implementation uses source-generated JsonSerializerContext for:
/// - Zero reflection overhead
/// - Improved startup performance
/// - Reduced memory allocations
/// - Full AOT compatibility.
/// </remarks>
public sealed class AotJsonSerializer : IMessageSerializer, IBinaryMessageSerializer, IDisposable
{
	private readonly JsonSerializerContext context;

	private readonly ThreadLocal<ArrayBufferWriter<byte>> _threadLocalBufferWriter;

	/// <summary>
	/// Initializes a new instance of the <see cref="AotJsonSerializer" /> class.
	/// </summary>
	/// <param name="context"> The source-generated context to use. Defaults to CoreMessageJsonContext. </param>
	public AotJsonSerializer(JsonSerializerContext? context = null)
	{
		this.context = context ?? CoreMessageJsonContext.Instance;
		_threadLocalBufferWriter = new ThreadLocal<ArrayBufferWriter<byte>>(
			static () => new ArrayBufferWriter<byte>(4096),
			trackAllValues: true);
	}

	/// <inheritdoc />
	public string SerializerName => "AotJsonSerializer";

	/// <inheritdoc />
	public string SerializerVersion => "1.0.0";

	/// <inheritdoc />
	public string ContentType => "application/json";

	/// <inheritdoc />
	public bool SupportsCompression => true;

	/// <inheritdoc />
	public string Format => "JSON";

	/// <inheritdoc />
	[RequiresUnreferencedCode("Serialization may require unreferenced code for type-specific handling.")]
	[RequiresDynamicCode("Serialization may require dynamic code generation for type-specific handling.")]
	public byte[] Serialize<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(T message)
	{
		var bufferWriter = _threadLocalBufferWriter.Value;
		bufferWriter.Clear();
		using var writer = new Utf8JsonWriter(bufferWriter);

		var typeInfo = GetTypeInfo<T>();
		JsonSerializer.Serialize(writer, message, typeInfo);
		writer.Flush();

		return bufferWriter.WrittenMemory.ToArray();
	}

	/// <inheritdoc />
	[RequiresUnreferencedCode("Deserialization may require unreferenced code for type-specific handling.")]
	[RequiresDynamicCode("Deserialization may require dynamic code generation for type-specific handling.")]
	public T Deserialize<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(byte[] data)
	{
		if (data == null || data.Length == 0)
		{
			throw new ArgumentNullException(nameof(data));
		}

		var reader = new Utf8JsonReader(data);
		var typeInfo = GetTypeInfo<T>();
		return JsonSerializer.Deserialize(ref reader, typeInfo)
			   ?? throw new InvalidOperationException(
				   Resources.Serialization_DeserializationResultedInNull);
	}

	/// <inheritdoc />
	public void Serialize<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(T message, IBufferWriter<byte> bufferWriter)
	{
		using var writer = new Utf8JsonWriter(bufferWriter);

		var typeInfo = GetTypeInfo<T>();
		JsonSerializer.Serialize(writer, message, typeInfo);
		writer.Flush();
	}

	/// <inheritdoc />
	public T Deserialize<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(ReadOnlySpan<byte> data)
	{
		if (data.IsEmpty)
		{
			throw new ArgumentException(
				Resources.Serialization_DataCannotBeEmpty,
				nameof(data));
		}

		var reader = new Utf8JsonReader(data);
		var typeInfo = GetTypeInfo<T>();
		return JsonSerializer.Deserialize(ref reader, typeInfo)
			   ?? throw new InvalidOperationException(
				   Resources.Serialization_DeserializationResultedInNull);
	}

	/// <summary>
	/// Releases all resources used by the AotJsonSerializer.
	/// </summary>
	public void Dispose()
	{
		if (_threadLocalBufferWriter.IsValueCreated)
		{
			foreach (var bw in _threadLocalBufferWriter.Values)
			{
				bw?.Clear();
			}
		}

		_threadLocalBufferWriter.Dispose();
	}

	private JsonTypeInfo<T> GetTypeInfo<T>()
	{
		if (context.GetTypeInfo(typeof(T)) is JsonTypeInfo<T> typeInfo)
		{
			return typeInfo;
		}

		throw new NotSupportedException(
			$"Type {typeof(T).Name} is not registered in the JsonSerializerContext. " +
			$"Add [JsonSerializable(typeof({typeof(T).Name}))] to the context or use a custom context.");
	}
}
