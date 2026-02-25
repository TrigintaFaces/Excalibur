// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Buffers;
using System.Diagnostics.CodeAnalysis;

namespace Excalibur.Dispatch.Abstractions.Serialization;

/// <summary>
/// Provides an abstraction over JSON serialization with direct UTF-8 support for zero-copy operations.
/// </summary>
/// <remarks>
/// <para>
/// This interface defines 4 core methods for UTF-8 serialization/deserialization using runtime <see cref="Type"/>.
/// Generic <c>&lt;T&gt;</c> overloads and async variants are available as extension methods in
/// <see cref="Utf8JsonSerializerExtensions"/>.
/// </para>
/// <para>
/// Follows the <c>System.Text.Json.JsonSerializer</c> pattern: minimal core (serialize/deserialize with Type)
/// plus convenience overloads as static extension methods.
/// </para>
/// </remarks>
public interface IUtf8JsonSerializer : IJsonSerializer
{
	/// <summary>
	/// Serializes an object directly to UTF-8 bytes using the specified type.
	/// </summary>
	/// <param name="value"> The value to serialize. </param>
	/// <param name="type"> The runtime type of the value. </param>
	/// <returns> A byte array containing the UTF-8 JSON representation. </returns>
	byte[] SerializeToUtf8Bytes(object? value, Type type);

	/// <summary>
	/// Serializes an object directly to a buffer writer using the specified type.
	/// </summary>
	/// <param name="writer"> The buffer writer to write to. </param>
	/// <param name="value"> The value to serialize. </param>
	/// <param name="type"> The runtime type of the value. </param>
	void SerializeToUtf8(IBufferWriter<byte> writer, object? value, Type type);

	/// <summary>
	/// Deserializes UTF-8 bytes to an object of the specified type.
	/// </summary>
	/// <param name="utf8Json"> The UTF-8 JSON bytes. </param>
	/// <param name="type"> The target type. </param>
	/// <returns> The deserialized object. </returns>
	[RequiresUnreferencedCode("JSON deserialization with runtime type may require unreferenced code")]
	object? DeserializeFromUtf8(ReadOnlySpan<byte> utf8Json, Type type);

	/// <summary>
	/// Deserializes UTF-8 bytes to an object of the specified type.
	/// </summary>
	/// <param name="utf8Json"> The UTF-8 JSON bytes. </param>
	/// <param name="type"> The target type. </param>
	/// <returns> The deserialized object. </returns>
	[RequiresUnreferencedCode("JSON deserialization with runtime type may require unreferenced code")]
	object? DeserializeFromUtf8(ReadOnlyMemory<byte> utf8Json, Type type);
}
