// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR
// AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Buffers;
using Excalibur.Dispatch.Caching;
using Excalibur.Dispatch.Delivery.Registry;

namespace Excalibur.Dispatch.Serialization;

/// <summary>
/// Zero-allocation JSON serializer that minimizes memory allocations in hot paths. Uses pooled buffers and writers for all operations.
/// </summary>
/// <remarks>
/// This serializer is designed for high-throughput scenarios where memory allocations must be minimized. It uses the following strategies:
/// - Pooled Utf8JsonWriter instances
/// - Pooled byte buffers for serialization
/// - Direct UTF-8 operations to avoid string allocations
/// - Struct-based return types for zero-allocation results.
/// </remarks>
public sealed class DispatchJsonSerializer : IUtf8JsonSerializer, IMessageSerializer, IDisposable
{
	private readonly JsonSerializerOptions _options;
	private readonly DispatchJsonContext? _jsonContext;
	private readonly IUtf8JsonWriterPool _writerPool;
	private readonly IPooledBufferService _bufferManager;
	private readonly ThreadLocal<ArrayBufferWriter<byte>> _threadLocalBufferWriter;

	/// <summary>
	/// Initializes a new instance of the <see cref="DispatchJsonSerializer" /> class.
	/// </summary>
	/// <param name="configure"> Optional configuration action for JsonSerializerOptions. </param>
	/// <param name="jsonContext"> Optional JSON context for AOT serialization. </param>
	/// <param name="writerPool"> Optional UTF-8 JSON writer pool for performance optimization. </param>
	/// <param name="bufferManager"> Optional buffer manager for memory pooling. </param>
	[RequiresDynamicCode(
		"JSON serialization with JsonSerializerOptions and converters requires dynamic code generation for type inspection and serialization logic.")]
	public DispatchJsonSerializer(
		Action<JsonSerializerOptions>? configure = null,
		DispatchJsonContext? jsonContext = null,
		IUtf8JsonWriterPool? writerPool = null,
		IPooledBufferService? bufferManager = null)
	{
		_options = new JsonSerializerOptions
		{
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
			PropertyNameCaseInsensitive = true,
			WriteIndented = false,
			DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
			Converters = { new JsonStringEnumConverter() },

			// Optimize for speed
			DefaultBufferSize = 16384,
			MaxDepth = 64,
		};

		// Apply user configuration
		configure?.Invoke(_options);

		// Use the provided context or use the default instance
		_jsonContext = jsonContext ?? DispatchJsonContext.Default;

		// Add the context to the options for AOT support
		_options.TypeInfoResolver = _jsonContext;

		// Initialize pools
		_writerPool = writerPool ?? new Utf8JsonWriterPool(
			maxPoolSize: 256,
			threadLocalCacheSize: 4,
			enableAdaptiveSizing: true,
			enableTelemetry: true);
		_bufferManager = bufferManager ?? new PooledBufferService(useShared: true, clearBuffersByDefault: true);

		// Thread-local buffer writers to avoid contention
		_threadLocalBufferWriter = new ThreadLocal<ArrayBufferWriter<byte>>(
			static () => new ArrayBufferWriter<byte>(4096),
			trackAllValues: true);

		Telemetry = new SerializerTelemetry();
	}

	/// <summary>
	/// Gets telemetry information about serializer operations.
	/// </summary>
	/// <value> The current <see cref="Telemetry" /> value. </value>
	public SerializerTelemetry Telemetry { get; }

	/// <summary>
	/// Gets the name of the serializer.
	/// </summary>
	/// <value> The current <see cref="SerializerName" /> value. </value>
	public string SerializerName => "DispatchJsonSerializer";

	/// <summary>
	/// Gets the version of the serializer format.
	/// </summary>
	/// <value> The current <see cref="SerializerVersion" /> value. </value>
	public string SerializerVersion => "2.0.0";

	#region Internal Pooled Operations

	/// <summary>
	/// Serializes an object directly to a pooled buffer with zero allocations.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	[RequiresUnreferencedCode(
		"Generic JSON serialization may require types that are not statically referenced and could be removed during trimming.")]
	[RequiresDynamicCode("Generic JSON serialization requires runtime code generation for type-specific serialization logic.")]
	public PooledSerializationResult SerializeToPooledBuffer<T>(T value)
	{
		Telemetry.IncrementSerializationCount();

		if (EqualityComparer<T?>.Default.Equals(value, default(T?)))
		{
			var nullBuffer = _bufferManager.RentBuffer(4);
			"null"u8.CopyTo(nullBuffer.Buffer);
			return new PooledSerializationResult((PooledBuffer)nullBuffer, 4);
		}

		var bufferWriter = _threadLocalBufferWriter.Value;
		bufferWriter.Clear();

		using (var pooledWriter = _writerPool.RentWriter(bufferWriter))
		{
			JsonSerializer.Serialize(pooledWriter.Writer, value, _options);
			pooledWriter.Flush();
		}

		var writtenSpan = bufferWriter.WrittenSpan;
		var pooledBuffer = _bufferManager.RentBuffer(writtenSpan.Length);
		writtenSpan.CopyTo(pooledBuffer.Buffer);

		Telemetry.AddBytesWritten(writtenSpan.Length);

		return new PooledSerializationResult((PooledBuffer)pooledBuffer, writtenSpan.Length);
	}

	/// <summary>
	/// Serializes an object directly to a pooled buffer with zero allocations.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	[UnconditionalSuppressMessage(
		"AOT",
		"IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
		Justification =
			"This method handles runtime type serialization for messages. In AOT scenarios, message types are registered via MessageTypeRegistry or source generation.")]
	[UnconditionalSuppressMessage(
		"AOT",
		"IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
		Justification = "JSON serialization with runtime types uses pre-registered types from MessageTypeRegistry for AOT compatibility.")]
	public PooledSerializationResult SerializeToPooledBuffer(object? value, Type type)
	{
		Telemetry.IncrementSerializationCount();

		if (value == null)
		{
			var nullBuffer = _bufferManager.RentBuffer(4);
			"null"u8.CopyTo(nullBuffer.Buffer);
			return new PooledSerializationResult((PooledBuffer)nullBuffer, 4);
		}

		ArgumentNullException.ThrowIfNull(type);

		var bufferWriter = _threadLocalBufferWriter.Value;
		bufferWriter.Clear();

		using (var pooledWriter = _writerPool.RentWriter(bufferWriter))
		{
			JsonSerializer.Serialize(pooledWriter.Writer, value, type, _options);
			pooledWriter.Flush();
		}

		var writtenSpan = bufferWriter.WrittenSpan;
		var pooledBuffer = _bufferManager.RentBuffer(writtenSpan.Length);
		writtenSpan.CopyTo(pooledBuffer.Buffer);

		Telemetry.AddBytesWritten(writtenSpan.Length);

		return new PooledSerializationResult((PooledBuffer)pooledBuffer, writtenSpan.Length);
	}

	/// <summary>
	/// Serializes directly to the provided buffer writer with zero intermediate allocations.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	[UnconditionalSuppressMessage(
		"AOT",
		"IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
		Justification =
			"Runtime type serialization is handled through registered types in MessageTypeRegistry or DispatchJsonContext for AOT scenarios.")]
	[UnconditionalSuppressMessage(
		"AOT",
		"IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
		Justification = "The serializer uses pre-configured JsonSerializerOptions with TypeInfoResolver for AOT compatibility.")]
	public void SerializeToWriter(IBufferWriter<byte> writer, object? value, Type type)
	{
		ArgumentNullException.ThrowIfNull(writer);
		ArgumentNullException.ThrowIfNull(type);
		Telemetry.IncrementSerializationCount();

		if (value == null)
		{
			writer.Write("null"u8);
			Telemetry.AddBytesWritten(4);
			return;
		}

		using (var pooledWriter = _writerPool.RentWriter(writer))
		{
			var startBytes = pooledWriter.BytesWritten;
			JsonSerializer.Serialize(pooledWriter.Writer, value, type, _options);
			pooledWriter.Flush();
			Telemetry.AddBytesWritten(pooledWriter.BytesWritten - startBytes);
		}
	}

	/// <summary>
	/// Deserializes directly from UTF-8 bytes with minimal allocations.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	[RequiresUnreferencedCode("JSON deserialization with runtime type may require unreferenced code")]
	[UnconditionalSuppressMessage(
		"AOT",
		"IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
		Justification =
			"Runtime type deserialization uses MessageTypeRegistry for type resolution in AOT scenarios. Types are registered at startup.")]
	[UnconditionalSuppressMessage(
		"AOT",
		"IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
		Justification = "The deserializer uses DispatchJsonContext with pre-registered types for AOT compatibility.")]
	public object? DeserializeFromBytes(ReadOnlySpan<byte> utf8Json, Type type)
	{
		ArgumentNullException.ThrowIfNull(type);
		Telemetry.IncrementDeserializationCount();
		Telemetry.AddBytesRead(utf8Json.Length);

		if (utf8Json.IsEmpty)
		{
			return null;
		}

		// Check for type resolution if needed
		if (type == typeof(string) && utf8Json.Length > 0 && utf8Json[0] != '"')
		{
			var typeNameString = Utf8StringCache.Shared.GetString(utf8Json);
			var resolvedType = MessageTypeRegistry.GetType(typeNameString);
			if (resolvedType != null)
			{
				type = resolvedType;
			}
		}

		return JsonSerializer.Deserialize(utf8Json, type, _options);
	}

	/// <summary>
	/// Deserializes directly from a stream with minimal allocations.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	[UnconditionalSuppressMessage(
		"AOT",
		"IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
		Justification =
			"Generic async deserialization preserves type T through the async state machine. The type is known at compile time.")]
	[UnconditionalSuppressMessage(
		"AOT",
		"IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
		Justification = "Async deserialization with generic type T is AOT-compatible through source-generated contexts.")]
	public async ValueTask<T?> DeserializeFromStreamAsync<T>(Stream utf8Json, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(utf8Json);
		Telemetry.IncrementDeserializationCount();

		var result = await JsonSerializer.DeserializeAsync<T>(utf8Json, _options, cancellationToken).ConfigureAwait(false);

		if (utf8Json.CanSeek)
		{
			Telemetry.AddBytesRead(utf8Json.Position);
		}

		return result;
	}

	/// <summary>
	/// Deserializes directly from a stream with minimal allocations.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	[RequiresUnreferencedCode("JSON deserialization with runtime type may require unreferenced code")]
	[UnconditionalSuppressMessage(
		"AOT",
		"IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
		Justification =
			"Async runtime type deserialization uses registered types from MessageTypeRegistry or DispatchJsonContext for AOT scenarios.")]
	[UnconditionalSuppressMessage(
		"AOT",
		"IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
		Justification = "The async deserializer uses pre-configured JsonSerializerOptions with TypeInfoResolver for AOT compatibility.")]
	public async ValueTask<object?> DeserializeFromStreamAsync(Stream utf8Json, Type type, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(utf8Json);
		ArgumentNullException.ThrowIfNull(type);
		Telemetry.IncrementDeserializationCount();

		var result = await JsonSerializer.DeserializeAsync(utf8Json, type, _options, cancellationToken).ConfigureAwait(false);

		if (utf8Json.CanSeek)
		{
			Telemetry.AddBytesRead(utf8Json.Position);
		}

		return result;
	}

	#endregion

	#region IJsonSerializer Implementation

	/// <summary>
	/// Deserializes a JSON string to an object of type T.
	/// </summary>
	/// <typeparam name="T"> The type to deserialize to. </typeparam>
	/// <param name="json"> The JSON string to deserialize. </param>
	/// <returns> The deserialized object of type T, or null if deserialization fails. </returns>
	/// <exception cref="ArgumentException"> Thrown when json is null or whitespace. </exception>
	[UnconditionalSuppressMessage(
		"AOT",
		"IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access",
		Justification = "DeserializeFromBytes delegates to STJ with registered types for AOT compatibility.")]
	public T? Deserialize<T>(string json)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(json);
		var bytes = Utf8StringCache.Shared.GetBytes(json);
		return (T?)DeserializeFromBytes(bytes, typeof(T));
	}

	/// <summary>
	/// Deserializes a JSON string to an object of the specified type.
	/// </summary>
	/// <param name="json"> The JSON string to deserialize. </param>
	/// <param name="type"> The target type for deserialization. </param>
	/// <returns> The deserialized object, or null if deserialization fails. </returns>
	/// <exception cref="ArgumentException"> Thrown when json is null or whitespace. </exception>
	/// <exception cref="ArgumentNullException"> Thrown when type is null. </exception>
	[RequiresUnreferencedCode("JSON deserialization with runtime type may require unreferenced code")]
	[RequiresDynamicCode("JSON deserialization with runtime type requires dynamic code generation")]
	public object? Deserialize(string json, Type type)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(json);
		ArgumentNullException.ThrowIfNull(type);
		var bytes = Utf8StringCache.Shared.GetBytes(json);
		return DeserializeFromBytes(bytes, type);
	}

	/// <summary>
	/// Serializes an object to a JSON string.
	/// </summary>
	/// <typeparam name="T"> The type of object to serialize. </typeparam>
	/// <param name="value"> The object to serialize. </param>
	/// <returns> A JSON string representation of the object. </returns>
	[RequiresUnreferencedCode("Uses reflection which may break with AOT compilation")]
	[RequiresDynamicCode("Uses dynamic code generation which requires JIT compilation")]
	public string Serialize<T>(T value)
	{
		using var result = SerializeToPooledBuffer(value);
		return Utf8StringCache.Shared.GetString(result.WrittenSpan);
	}

	/// <summary>
	/// Serializes an object to a JSON string using the specified type.
	/// </summary>
	/// <param name="value"> The object to serialize. </param>
	/// <param name="type"> The type to use for serialization. </param>
	/// <returns> A JSON string representation of the object. </returns>
	[RequiresUnreferencedCode("JSON serialization with runtime type may require unreferenced code")]
	[RequiresDynamicCode("JSON serialization with runtime type requires dynamic code generation")]
	public string Serialize(object? value, Type type)
	{
		using var result = SerializeToPooledBuffer(value, type);
		return Utf8StringCache.Shared.GetString(result.WrittenSpan);
	}

	/// <summary>
	/// Serializes an object to a JsonElement for further manipulation.
	/// </summary>
	/// <typeparam name="T"> The type of object to serialize. </typeparam>
	/// <param name="value"> The object to serialize. </param>
	/// <returns> A JsonElement representing the serialized object. </returns>
	[RequiresUnreferencedCode("Uses reflection which may break with AOT compilation")]
	[RequiresDynamicCode("Uses dynamic code generation which requires JIT compilation")]
	public JsonElement SerializeToElement<T>(T value)
	{
		using var result = SerializeToPooledBuffer(value);
		return JsonDocument.Parse(result.WrittenMemory).RootElement.Clone();
	}

	/// <inheritdoc/>
	[RequiresUnreferencedCode("JSON serialization with runtime type may require unreferenced code")]
	[RequiresDynamicCode("JSON serialization with runtime type requires dynamic code generation")]
	public Task<string> SerializeAsync(object value, Type type) => Task.FromResult(Serialize(value, type));

	/// <inheritdoc/>
	[RequiresUnreferencedCode("JSON deserialization with runtime type may require unreferenced code")]
	[RequiresDynamicCode("JSON deserialization with runtime type requires dynamic code generation")]
	public Task<object?> DeserializeAsync(string json, Type type) => Task.FromResult(Deserialize(json, type));

	#endregion

	#region IUtf8JsonSerializer Core Implementation (4 methods)

	/// <inheritdoc/>
	public byte[] SerializeToUtf8Bytes(object? value, Type type)
	{
		using var result = SerializeToPooledBuffer(value, type);
		return result.WrittenMemory.ToArray();
	}

	/// <inheritdoc/>
	public void SerializeToUtf8(IBufferWriter<byte> writer, object? value, Type type) => SerializeToWriter(writer, value, type);

	/// <inheritdoc/>
	[RequiresUnreferencedCode("JSON deserialization with runtime type may require unreferenced code")]
	public object? DeserializeFromUtf8(ReadOnlySpan<byte> utf8Json, Type type) => DeserializeFromBytes(utf8Json, type);

	/// <inheritdoc/>
	[RequiresUnreferencedCode("JSON deserialization with runtime type may require unreferenced code")]
	public object? DeserializeFromUtf8(ReadOnlyMemory<byte> utf8Json, Type type) => DeserializeFromBytes(utf8Json.Span, type);

	#endregion

	#region IMessageSerializer Implementation

	/// <inheritdoc/>
	[RequiresUnreferencedCode("Serialization may require unreferenced code for type-specific handling.")]
	[RequiresDynamicCode("Serialization may require dynamic code generation for type-specific handling.")]
	byte[] IMessageSerializer.Serialize<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(T message) =>
		SerializeToUtf8Bytes(message, typeof(T));

	/// <inheritdoc/>
	[RequiresUnreferencedCode("Deserialization may require unreferenced code for type-specific handling.")]
	[RequiresDynamicCode("Deserialization may require dynamic code generation for type-specific handling.")]
	T IMessageSerializer.Deserialize<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(byte[] data)
	{
		ArgumentNullException.ThrowIfNull(data);
		var result = (T?)DeserializeFromUtf8(data.AsSpan(), typeof(T));
		return result ?? throw new InvalidOperationException(
			string.Format(
				CultureInfo.InvariantCulture,
				Resources.Serialization_FailedToDeserializeToType,
				typeof(T).Name));
	}

	#endregion

	/// <summary>
	/// Releases all resources used by the DispatchJsonSerializer.
	/// </summary>
	public void Dispose()
	{
		if (_threadLocalBufferWriter.IsValueCreated)
		{
			foreach (var bufferWriter in _threadLocalBufferWriter.Values)
			{
				bufferWriter?.Clear();
			}
		}

		_threadLocalBufferWriter.Dispose();
	}
}
