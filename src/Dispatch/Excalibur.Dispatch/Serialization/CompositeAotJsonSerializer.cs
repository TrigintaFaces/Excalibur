// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR
// AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.CloudEvents;

namespace Excalibur.Dispatch.Serialization;

/// <summary>
/// Composite AOT-compatible JSON serializer that can handle types from multiple contexts. This allows mixing message types from different
/// cloud providers while maintaining AOT compatibility.
/// </summary>
/// <remarks>
/// This serializer tries each registered context in order until it finds one that supports the type. For best performance, register
/// contexts in order of expected usage frequency.
/// </remarks>
public sealed class CompositeAotJsonSerializer : IMessageSerializer, IBinaryMessageSerializer, IDisposable
{
	private readonly JsonSerializerContext[] contexts;

	private readonly ConcurrentDictionary<Type, JsonSerializerContext> typeContextCache;

	private readonly ThreadLocal<ArrayBufferWriter<byte>> _threadLocalBufferWriter;

	/// <summary>
	/// Initializes a new instance of the <see cref="CompositeAotJsonSerializer" /> class.
	/// </summary>
	/// <param name="contexts"> The source-generated contexts to use, in priority order. </param>
	public CompositeAotJsonSerializer(params JsonSerializerContext[] contexts)
	{
		if (contexts == null || contexts.Length == 0)
		{
			throw new ArgumentException(Resources.CompositeAotJsonSerializer_AtLeastOneContextMustBeProvided, nameof(contexts));
		}

		this.contexts = contexts;
		typeContextCache = new ConcurrentDictionary<Type, JsonSerializerContext>();
		_threadLocalBufferWriter = new ThreadLocal<ArrayBufferWriter<byte>>(
			static () => new ArrayBufferWriter<byte>(4096),
			trackAllValues: true);
	}

	/// <inheritdoc />
	public string SerializerName => "CompositeAotJsonSerializer";

	/// <inheritdoc />
	public string SerializerVersion => "1.0.0";

	/// <inheritdoc />
	public string ContentType => "application/json";

	/// <inheritdoc />
	public bool SupportsCompression => true;

	/// <inheritdoc />
	public string Format => "JSON";

	/// <summary>
	/// Creates a composite serializer with all standard cloud provider contexts.
	/// </summary>
	public static CompositeAotJsonSerializer CreateWithAllProviders() =>
		new(
			CoreMessageJsonContext.Default,
			CloudEventJsonContext.Default
		);

	/// <inheritdoc />
	[RequiresUnreferencedCode("Serialization may require unreferenced code for type-specific handling.")]
	[RequiresDynamicCode("Serialization may require dynamic code generation for type-specific handling.")]
	public byte[] Serialize<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(T message)
	{
		var bufferWriter = _threadLocalBufferWriter.Value;
		bufferWriter.Clear();
		using var writer = new Utf8JsonWriter(bufferWriter);

		var context = GetContextForType(typeof(T));
		var typeInfo = GetTypeInfo<T>(context);
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
		var context = GetContextForType(typeof(T));
		var typeInfo = GetTypeInfo<T>(context);
		return JsonSerializer.Deserialize(ref reader, typeInfo)
			   ?? throw new InvalidOperationException(
				   Resources.Serialization_DeserializationResultedInNull);
	}

	/// <inheritdoc />
	public void Serialize<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(T message, IBufferWriter<byte> bufferWriter)
	{
		using var writer = new Utf8JsonWriter(bufferWriter);

		var context = GetContextForType(typeof(T));
		var typeInfo = GetTypeInfo<T>(context);
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
		var context = GetContextForType(typeof(T));
		var typeInfo = GetTypeInfo<T>(context);
		return JsonSerializer.Deserialize(ref reader, typeInfo)
			   ?? throw new InvalidOperationException(
				   Resources.Serialization_DeserializationResultedInNull);
	}

	/// <summary>
	/// Releases all resources used by the CompositeAotJsonSerializer.
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

	/// <summary>
	/// Gets the JsonTypeInfo for the specified type from the context.
	/// </summary>
	/// <exception cref="NotSupportedException"> </exception>
	private static JsonTypeInfo<T> GetTypeInfo<T>(JsonSerializerContext context)
	{
		if (context.GetTypeInfo(typeof(T)) is JsonTypeInfo<T> typeInfo)
		{
			return typeInfo;
		}

		throw new NotSupportedException(
			$"Type {typeof(T).Name} is not registered in any of the configured JsonSerializerContexts. " +
			$"Add [JsonSerializable(typeof({typeof(T).Name}))] to one of the contexts.");
	}

	/// <summary>
	/// Gets the appropriate context for the specified type.
	/// </summary>
	/// <exception cref="NotSupportedException">
	/// Thrown when the specified type is not registered in any of the configured <see cref="JsonSerializerContext"/> instances.
	/// </exception>
	private JsonSerializerContext GetContextForType(Type type)
	{
		// Check cache first
		if (typeContextCache.TryGetValue(type, out var cachedContext))
		{
			return cachedContext;
		}

		// Try each context to find one that supports this type
		foreach (var context in contexts)
		{
			try
			{
				var typeInfo = context.GetTypeInfo(type);
				if (typeInfo != null)
				{
					// Cache the result for future lookups
					_ = typeContextCache.TryAdd(type, context);
					return context;
				}
			}
			catch (NotSupportedException)
			{
				// Context doesn't support this type, try next
			}
			catch (InvalidOperationException)
			{
				// Context options misconfigured for this type, try next
			}
		}

		throw new NotSupportedException(
			$"Type {type.Name} is not registered in any of the configured JsonSerializerContexts. " +
			$"Add [JsonSerializable(typeof({type.Name}))] to one of the contexts.");
	}
}
