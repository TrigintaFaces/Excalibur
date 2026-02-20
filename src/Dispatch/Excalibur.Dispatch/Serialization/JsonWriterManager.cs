// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Text.Encodings.Web;
using System.Text.Json;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Buffers;

namespace Excalibur.Dispatch.Serialization;

/// <summary>
/// Manages pooled JSON writers for message serialization with integrated buffer management.
/// </summary>
public sealed class JsonWriterManager
{
	private readonly JsonWriterOptions _defaultOptions;

	/// <summary>
	/// Initializes a new instance of the <see cref="JsonWriterManager" /> class.
	/// </summary>
	/// <param name="writerPool"> The JSON writer pool. If null, a default pool is created. </param>
	/// <param name="bufferManager"> The buffer manager. If null, a default manager is created. </param>
	/// <param name="defaultOptions"> The default JSON writer options. </param>
	public JsonWriterManager(
		IUtf8JsonWriterPool? writerPool = null,
		IPooledBufferService? bufferManager = null,
		JsonWriterOptions? defaultOptions = null)
	{
		WriterPool = writerPool ?? new Utf8JsonWriterPool(maxPoolSize: 100, defaultOptions: defaultOptions);
		BufferManager = bufferManager ?? new PooledBufferService(useShared: true, clearBuffersByDefault: true);
		_defaultOptions = defaultOptions ?? new JsonWriterOptions
		{
			Indented = false,
			SkipValidation = false,
			Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
		};
	}

	/// <summary>
	/// Gets the writer pool being used.
	/// </summary>
	/// <value>The current <see cref="WriterPool"/> value.</value>
	public IUtf8JsonWriterPool WriterPool { get; }

	/// <summary>
	/// Gets the buffer manager being used.
	/// </summary>
	/// <value>The current <see cref="BufferManager"/> value.</value>
	public IPooledBufferService BufferManager { get; }

	/// <summary>
	/// Creates a pooled JSON writer with a pooled buffer.
	/// </summary>
	/// <param name="initialBufferSize"> The initial buffer size. Default is 1024 bytes. </param>
	/// <param name="options"> Optional JSON writer options. </param>
	/// <returns> A <see cref="PooledJsonWriterContext" /> that manages both the writer and buffer. </returns>
	public PooledJsonWriterContext CreateWriter(int initialBufferSize = 1024, JsonWriterOptions? options = null)
	{
		var bufferWriter = new ArrayBufferWriter<byte>(initialBufferSize);
		var writer = WriterPool.Rent(bufferWriter, options ?? _defaultOptions);

		return new PooledJsonWriterContext(WriterPool, writer, bufferWriter);
	}

	/// <summary>
	/// Serializes an object to a pooled buffer using a pooled JSON writer.
	/// </summary>
	/// <typeparam name="T"> The type of object to serialize. </typeparam>
	/// <param name="value"> The value to serialize. </param>
	/// <param name="options"> Optional JSON serializer options. </param>
	/// <param name="writerOptions"> Optional JSON writer options. </param>
	/// <returns> A <see cref="PooledBuffer" /> containing the serialized data. </returns>
	[RequiresUnreferencedCode(
		"Generic JSON serialization may require types that are not statically referenced and could be removed during trimming.")]
	[RequiresDynamicCode("Generic JSON serialization requires runtime code generation for type-specific serialization logic.")]
	public PooledBuffer SerializeToPooledBuffer<T>(T value, JsonSerializerOptions? options = null, JsonWriterOptions? writerOptions = null)
	{
		// Estimate buffer size based on type
		var estimatedSize = EstimateSerializedSize<T>();
		var bufferWriter = new ArrayBufferWriter<byte>(estimatedSize);

		using (var pooledWriter = WriterPool.RentWriter(bufferWriter, writerOptions ?? _defaultOptions))
		{
			JsonSerializer.Serialize(pooledWriter.Writer, value, options);
			pooledWriter.Flush();
		}

		// Get the written data
		var writtenMemory = bufferWriter.WrittenMemory;

		// Rent a buffer from the pool and copy the data
		var pooledBuffer = BufferManager.RentBuffer(writtenMemory.Length);
		writtenMemory.CopyTo(pooledBuffer.Memory);

		return (PooledBuffer)pooledBuffer;
	}

	/// <summary>
	/// Executes a serialization action with a pooled writer and buffer.
	/// </summary>
	/// <param name="action"> The action to execute with the writer. </param>
	/// <param name="initialBufferSize"> The initial buffer size. </param>
	/// <param name="options"> Optional JSON writer options. </param>
	/// <returns> A <see cref="PooledBuffer" /> containing the written data. </returns>
	public PooledBuffer WithPooledWriter(Action<Utf8JsonWriter> action, int initialBufferSize = 1024, JsonWriterOptions? options = null)
	{
		ArgumentNullException.ThrowIfNull(action);

		using var context = CreateWriter(initialBufferSize, options);
		action(context.Writer);
		context.Writer.Flush();

		return context.GetPooledBuffer(BufferManager);
	}

	/// <summary>
	/// Executes a serialization function with a pooled writer and buffer.
	/// </summary>
	/// <typeparam name="T"> The return type of the function. </typeparam>
	/// <param name="func"> The function to execute with the writer. </param>
	/// <param name="initialBufferSize"> The initial buffer size. </param>
	/// <param name="options"> Optional JSON writer options. </param>
	/// <returns> A tuple containing the result and a <see cref="PooledBuffer" /> with the written data. </returns>
	public (T Result, PooledBuffer Buffer) WithPooledWriter<T>(Func<Utf8JsonWriter, T> func, int initialBufferSize = 1024,
		JsonWriterOptions? options = null)
	{
		ArgumentNullException.ThrowIfNull(func);

		using var context = CreateWriter(initialBufferSize, options);
		var result = func(context.Writer);
		context.Writer.Flush();

		var buffer = context.GetPooledBuffer(BufferManager);
		return (result, buffer);
	}

	/// <summary>
	/// Estimates the serialized size for a given type to pre-allocate an appropriately sized buffer.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This is a static heuristic that trades accuracy for zero-allocation overhead. It does NOT
	/// inspect the actual object graph or property count. The estimate is intentionally conservative
	/// (overestimates) to avoid costly <see cref="ArrayBufferWriter{T}"/> resizes during serialization.
	/// </para>
	/// <para>
	/// Trade-offs:
	/// <list type="bullet">
	/// <item><description>Primitives/strings: 128 bytes -- covers most scalar JSON tokens with minimal waste.</description></item>
	/// <item><description>Value types: 256 bytes -- structs typically serialize to small JSON objects.</description></item>
	/// <item><description>Collections: 4096 bytes -- arrays/lists vary widely; 4KB is a reasonable starting point
	/// that avoids resize for small-to-medium collections while keeping allocation bounded.</description></item>
	/// <item><description>Reference types: 1024 bytes -- a general default for DTOs/POCOs; the ArrayBufferWriter
	/// will grow automatically if the actual payload exceeds this estimate.</description></item>
	/// </list>
	/// </para>
	/// <para>
	/// Future improvement: consider a per-type adaptive estimate using a ConcurrentDictionary to track
	/// average serialized sizes at runtime, capped at 1024 entries to bound memory.
	/// </para>
	/// </remarks>
	private static int EstimateSerializedSize<T>()
	{
		var type = typeof(T);

		// Primitives, strings, and common scalar types need minimal buffer space
		if (type.IsPrimitive || type == typeof(string) || type == typeof(DateTime) ||
			type == typeof(DateTimeOffset) || type == typeof(Guid))
		{
			return 128;
		}

		// Value types (structs) are typically compact
		if (type.IsValueType)
		{
			return 256;
		}

		// Collections can be large; start with a reasonable default to avoid early resizes
		if (type.IsArray || typeof(System.Collections.IEnumerable).IsAssignableFrom(type))
		{
			return 4096;
		}

		// General reference types (DTOs, POCOs, domain objects)
		return 1024;
	}
}
