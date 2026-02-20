// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Buffers;
using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Text;


namespace Excalibur.Dispatch.Abstractions.Serialization;

/// <summary>
/// Extension methods for <see cref="IUtf8JsonSerializer" />.
/// </summary>
/// <remarks>
/// Provides generic <c>&lt;T&gt;</c> overloads, async variants, and convenience helpers that delegate to the
/// 4 core <see cref="IUtf8JsonSerializer"/> methods.
/// </remarks>
public static class Utf8JsonSerializerExtensions
{
	#region Generic Sync Overloads (moved from interface)

	/// <summary>
	/// Serializes an object directly to UTF-8 bytes.
	/// </summary>
	/// <typeparam name="T"> The type of the value to serialize. </typeparam>
	/// <param name="serializer"> The UTF-8 JSON serializer. </param>
	/// <param name="value"> The value to serialize. </param>
	/// <returns> A byte array containing the UTF-8 JSON representation. </returns>
	[RequiresUnreferencedCode("JSON serialization may require unreferenced code for type-specific handling.")]
	[RequiresDynamicCode("JSON serialization may require dynamic code generation for type-specific handling.")]
	public static byte[] SerializeToUtf8Bytes<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(
		this IUtf8JsonSerializer serializer, T value)
	{
		ArgumentNullException.ThrowIfNull(serializer);
		return serializer.SerializeToUtf8Bytes(value, typeof(T));
	}

	/// <summary>
	/// Serializes an object directly to a buffer writer.
	/// </summary>
	/// <typeparam name="T"> The type of the value to serialize. </typeparam>
	/// <param name="serializer"> The UTF-8 JSON serializer. </param>
	/// <param name="writer"> The buffer writer to write to. </param>
	/// <param name="value"> The value to serialize. </param>
	public static void SerializeToUtf8<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(
		this IUtf8JsonSerializer serializer, IBufferWriter<byte> writer, T value)
	{
		ArgumentNullException.ThrowIfNull(serializer);
		serializer.SerializeToUtf8(writer, value, typeof(T));
	}

	/// <summary>
	/// Deserializes UTF-8 bytes to an object.
	/// </summary>
	/// <typeparam name="T"> The target type. </typeparam>
	/// <param name="serializer"> The UTF-8 JSON serializer. </param>
	/// <param name="utf8Json"> The UTF-8 JSON bytes. </param>
	/// <returns> The deserialized object. </returns>
	[RequiresUnreferencedCode("JSON deserialization may require unreferenced code for type-specific handling.")]
	public static T? DeserializeFromUtf8<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(
		this IUtf8JsonSerializer serializer, ReadOnlySpan<byte> utf8Json)
	{
		ArgumentNullException.ThrowIfNull(serializer);
		return (T?)serializer.DeserializeFromUtf8(utf8Json, typeof(T));
	}

	/// <summary>
	/// Deserializes UTF-8 bytes to an object.
	/// </summary>
	/// <typeparam name="T"> The target type. </typeparam>
	/// <param name="serializer"> The UTF-8 JSON serializer. </param>
	/// <param name="utf8Json"> The UTF-8 JSON bytes. </param>
	/// <returns> The deserialized object. </returns>
	[RequiresUnreferencedCode("JSON deserialization may require unreferenced code for type-specific handling.")]
	public static T? DeserializeFromUtf8<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(
		this IUtf8JsonSerializer serializer, ReadOnlyMemory<byte> utf8Json)
	{
		ArgumentNullException.ThrowIfNull(serializer);
		return (T?)serializer.DeserializeFromUtf8(utf8Json, typeof(T));
	}

	#endregion

	#region Async Overloads (moved from interface)

	/// <summary>
	/// Asynchronously serializes an object directly to UTF-8 bytes using the specified type.
	/// </summary>
	/// <param name="serializer"> The UTF-8 JSON serializer. </param>
	/// <param name="value"> The value to serialize. </param>
	/// <param name="type"> The runtime type of the value. </param>
	/// <returns> A byte array containing the UTF-8 JSON representation. </returns>
	[RequiresUnreferencedCode("JSON serialization with runtime type may require unreferenced code")]
	public static Task<byte[]> SerializeToUtf8BytesAsync(this IUtf8JsonSerializer serializer, object? value, Type type)
	{
		ArgumentNullException.ThrowIfNull(serializer);
		return Task.FromResult(serializer.SerializeToUtf8Bytes(value, type));
	}

	/// <summary>
	/// Asynchronously serializes an object directly to UTF-8 bytes using the specified type.
	/// </summary>
	/// <param name="serializer"> The UTF-8 JSON serializer. </param>
	/// <param name="value"> The value to serialize. </param>
	/// <param name="type"> The runtime type of the value. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A byte array containing the UTF-8 JSON representation. </returns>
	[RequiresUnreferencedCode("JSON serialization with runtime type may require unreferenced code")]
	public static Task<byte[]> SerializeToUtf8BytesAsync(
		this IUtf8JsonSerializer serializer, object? value, Type type, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(serializer);
		cancellationToken.ThrowIfCancellationRequested();
		return Task.FromResult(serializer.SerializeToUtf8Bytes(value, type));
	}

	/// <summary>
	/// Asynchronously serializes an object directly to UTF-8 bytes.
	/// </summary>
	/// <typeparam name="T"> The type of the value to serialize. </typeparam>
	/// <param name="serializer"> The UTF-8 JSON serializer. </param>
	/// <param name="value"> The value to serialize. </param>
	/// <returns> A byte array containing the UTF-8 JSON representation. </returns>
	[RequiresUnreferencedCode("JSON serialization may require unreferenced code for type-specific handling.")]
	[RequiresDynamicCode("JSON serialization may require dynamic code generation for type-specific handling.")]
	public static Task<byte[]> SerializeToUtf8BytesAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(
		this IUtf8JsonSerializer serializer, T value)
	{
		ArgumentNullException.ThrowIfNull(serializer);
		return Task.FromResult(serializer.SerializeToUtf8Bytes(value, typeof(T)));
	}

	/// <summary>
	/// Asynchronously serializes an object directly to UTF-8 bytes.
	/// </summary>
	/// <typeparam name="T"> The type of the value to serialize. </typeparam>
	/// <param name="serializer"> The UTF-8 JSON serializer. </param>
	/// <param name="value"> The value to serialize. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A byte array containing the UTF-8 JSON representation. </returns>
	[RequiresUnreferencedCode("JSON serialization may require unreferenced code for type-specific handling.")]
	[RequiresDynamicCode("JSON serialization may require dynamic code generation for type-specific handling.")]
	public static Task<byte[]> SerializeToUtf8BytesAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(
		this IUtf8JsonSerializer serializer, T value, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(serializer);
		cancellationToken.ThrowIfCancellationRequested();
		return Task.FromResult(serializer.SerializeToUtf8Bytes(value, typeof(T)));
	}

	/// <summary>
	/// Asynchronously deserializes UTF-8 bytes to an object of the specified type.
	/// </summary>
	/// <param name="serializer"> The UTF-8 JSON serializer. </param>
	/// <param name="utf8Json"> The UTF-8 JSON bytes. </param>
	/// <param name="type"> The target type. </param>
	/// <returns> The deserialized object. </returns>
	[RequiresUnreferencedCode("JSON deserialization with runtime type may require unreferenced code")]
	public static Task<object?> DeserializeFromUtf8Async(
		this IUtf8JsonSerializer serializer, ReadOnlyMemory<byte> utf8Json, Type type)
	{
		ArgumentNullException.ThrowIfNull(serializer);
		return Task.FromResult(serializer.DeserializeFromUtf8(utf8Json, type));
	}

	/// <summary>
	/// Asynchronously deserializes UTF-8 bytes to an object of the specified type.
	/// </summary>
	/// <param name="serializer"> The UTF-8 JSON serializer. </param>
	/// <param name="utf8Json"> The UTF-8 JSON bytes. </param>
	/// <param name="type"> The target type. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> The deserialized object. </returns>
	[RequiresUnreferencedCode("JSON deserialization with runtime type may require unreferenced code")]
	public static Task<object?> DeserializeFromUtf8Async(
		this IUtf8JsonSerializer serializer, ReadOnlyMemory<byte> utf8Json, Type type, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(serializer);
		cancellationToken.ThrowIfCancellationRequested();
		return Task.FromResult(serializer.DeserializeFromUtf8(utf8Json, type));
	}

	/// <summary>
	/// Asynchronously deserializes UTF-8 bytes to an object.
	/// </summary>
	/// <typeparam name="T"> The target type. </typeparam>
	/// <param name="serializer"> The UTF-8 JSON serializer. </param>
	/// <param name="utf8Json"> The UTF-8 JSON bytes. </param>
	/// <returns> The deserialized object. </returns>
	[RequiresUnreferencedCode("JSON deserialization may require unreferenced code for type-specific handling.")]
	public static Task<T?> DeserializeFromUtf8Async<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(
		this IUtf8JsonSerializer serializer, ReadOnlyMemory<byte> utf8Json)
	{
		ArgumentNullException.ThrowIfNull(serializer);
		return Task.FromResult((T?)serializer.DeserializeFromUtf8(utf8Json, typeof(T)));
	}

	/// <summary>
	/// Asynchronously deserializes UTF-8 bytes to an object.
	/// </summary>
	/// <typeparam name="T"> The target type. </typeparam>
	/// <param name="serializer"> The UTF-8 JSON serializer. </param>
	/// <param name="utf8Json"> The UTF-8 JSON bytes. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> The deserialized object. </returns>
	[RequiresUnreferencedCode("JSON deserialization may require unreferenced code for type-specific handling.")]
	public static Task<T?> DeserializeFromUtf8Async<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(
		this IUtf8JsonSerializer serializer, ReadOnlyMemory<byte> utf8Json, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(serializer);
		cancellationToken.ThrowIfCancellationRequested();
		return Task.FromResult((T?)serializer.DeserializeFromUtf8(utf8Json, typeof(T)));
	}

	#endregion

	#region Convenience Helpers

	/// <summary>
	/// Serializes an object to a pooled UTF-8 buffer for zero-allocation scenarios.
	/// </summary>
	/// <typeparam name="T"> The type of the value to serialize. </typeparam>
	/// <param name="serializer"> The UTF-8 JSON serializer. </param>
	/// <param name="value"> The value to serialize. </param>
	/// <param name="bufferManager"> The buffer manager to use for pooling. </param>
	/// <returns> A pooled buffer containing the UTF-8 JSON. Must be disposed after use. </returns>
	public static PooledBuffer SerializeToPooledUtf8Buffer<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(
		this IUtf8JsonSerializer serializer, T value,
		IPooledBufferService bufferManager)
	{
		ArgumentNullException.ThrowIfNull(serializer);
		ArgumentNullException.ThrowIfNull(bufferManager);

		// Estimate size based on type
		var estimatedSize = EstimateSerializedSize<T>();
		var bufferWriter = new ArrayBufferWriter<byte>(estimatedSize);

		serializer.SerializeToUtf8(bufferWriter, value, typeof(T));

		// Rent a buffer and copy the data
		var pooledBuffer = bufferManager.RentBuffer(bufferWriter.WrittenCount);
		bufferWriter.WrittenSpan.CopyTo(pooledBuffer.Memory.Span);

		return (PooledBuffer)pooledBuffer;
	}

	/// <summary>
	/// Serializes an object to a pooled UTF-8 buffer for zero-allocation scenarios.
	/// </summary>
	/// <param name="serializer"> The UTF-8 JSON serializer. </param>
	/// <param name="value"> The value to serialize. </param>
	/// <param name="type"> The runtime type of the value. </param>
	/// <param name="bufferManager"> The buffer manager to use for pooling. </param>
	/// <returns> A pooled buffer containing the UTF-8 JSON. Must be disposed after use. </returns>
	public static PooledBuffer SerializeToPooledUtf8Buffer(this IUtf8JsonSerializer serializer, object? value, Type type,
		IPooledBufferService bufferManager)
	{
		ArgumentNullException.ThrowIfNull(serializer);
		ArgumentNullException.ThrowIfNull(type);
		ArgumentNullException.ThrowIfNull(bufferManager);

		// Estimate size based on type
		var estimatedSize = type.IsValueType ? 256 : 1024;
		var bufferWriter = new ArrayBufferWriter<byte>(estimatedSize);

		serializer.SerializeToUtf8(bufferWriter, value, type);

		// Rent a buffer and copy the data
		var pooledBuffer = bufferManager.RentBuffer(bufferWriter.WrittenCount);
		bufferWriter.WrittenSpan.CopyTo(pooledBuffer.Memory.Span);

		return (PooledBuffer)pooledBuffer;
	}

	/// <summary>
	/// Converts a string to UTF-8 bytes and deserializes it.
	/// </summary>
	/// <typeparam name="T"> The target type. </typeparam>
	/// <param name="serializer"> The UTF-8 JSON serializer. </param>
	/// <param name="json"> The JSON string. </param>
	/// <returns> The deserialized object. </returns>
	[RequiresUnreferencedCode("JSON deserialization may require unreferenced code for type-specific handling.")]
	public static T? DeserializeFromString<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(
		this IUtf8JsonSerializer serializer, string json)
	{
		ArgumentNullException.ThrowIfNull(serializer);
		ArgumentException.ThrowIfNullOrWhiteSpace(json);

		var utf8Bytes = Encoding.UTF8.GetBytes(json);
		return (T?)serializer.DeserializeFromUtf8((ReadOnlySpan<byte>)utf8Bytes, typeof(T));
	}

	/// <summary>
	/// Converts a string to UTF-8 bytes and deserializes it.
	/// </summary>
	/// <param name="serializer"> The UTF-8 JSON serializer. </param>
	/// <param name="json"> The JSON string. </param>
	/// <param name="type"> The target type. </param>
	/// <returns> The deserialized object. </returns>
	[RequiresUnreferencedCode("JSON deserialization with runtime type may require unreferenced code")]
	public static object? DeserializeFromString(this IUtf8JsonSerializer serializer, string json, Type type)
	{
		ArgumentNullException.ThrowIfNull(serializer);
		ArgumentException.ThrowIfNullOrWhiteSpace(json);
		ArgumentNullException.ThrowIfNull(type);

		var utf8Bytes = Encoding.UTF8.GetBytes(json);
		return serializer.DeserializeFromUtf8((ReadOnlySpan<byte>)utf8Bytes, type);
	}

	/// <summary>
	/// Serializes an object to a UTF-8 string.
	/// </summary>
	/// <typeparam name="T"> The type of the value. </typeparam>
	/// <param name="serializer"> The UTF-8 JSON serializer. </param>
	/// <param name="value"> The value to serialize. </param>
	/// <returns> A JSON string. </returns>
	[RequiresUnreferencedCode("JSON serialization may require unreferenced code for type-specific handling.")]
	[RequiresDynamicCode("JSON serialization may require dynamic code generation for type-specific handling.")]
	public static string SerializeToString<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(
			this IUtf8JsonSerializer serializer, T value)
	{
		ArgumentNullException.ThrowIfNull(serializer);

		var utf8Bytes = serializer.SerializeToUtf8Bytes(value, typeof(T));
		return Encoding.UTF8.GetString(utf8Bytes);
	}

	/// <summary>
	/// Serializes an object to a UTF-8 string.
	/// </summary>
	/// <param name="serializer"> The UTF-8 JSON serializer. </param>
	/// <param name="value"> The value to serialize. </param>
	/// <param name="type"> The runtime type of the value. </param>
	/// <returns> A JSON string. </returns>
	public static string SerializeToString(this IUtf8JsonSerializer serializer, object? value, Type type)
	{
		ArgumentNullException.ThrowIfNull(serializer);
		ArgumentNullException.ThrowIfNull(type);

		var utf8Bytes = serializer.SerializeToUtf8Bytes(value, type);
		return Encoding.UTF8.GetString(utf8Bytes);
	}

	#endregion

	/// <summary>
	/// Estimates the serialized size for a given type.
	/// </summary>
	/// <typeparam name="T"> The type to estimate the serialized size for. </typeparam>
	private static int EstimateSerializedSize<T>()
	{
		var type = typeof(T);

		// Simple heuristics based on type
		if (type.IsPrimitive || type == typeof(string) || type == typeof(DateTime) || type == typeof(DateTimeOffset) ||
			type == typeof(Guid))
		{
			return 128;
		}

		if (type.IsValueType)
		{
			return 256;
		}

		if (type.IsArray || typeof(IEnumerable).IsAssignableFrom(type))
		{
			return 4096;
		}

		return 1024;
	}
}
