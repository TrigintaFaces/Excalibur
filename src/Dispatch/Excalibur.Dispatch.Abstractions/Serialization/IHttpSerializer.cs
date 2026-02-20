// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

namespace Excalibur.Dispatch.Abstractions.Serialization;

/// <summary>
/// Provides serialization for HTTP request/response bodies.
/// </summary>
/// <remarks>
/// <para>
/// This interface defines 4 core Type-based methods for HTTP serialization.
/// Generic <c>&lt;T&gt;</c> convenience overloads are available as extension methods
/// in <see cref="HttpSerializerExtensions"/>.
/// </para>
/// <para>
/// This interface is specifically designed for HTTP serialization scenarios and is
/// separate from <see cref="IPayloadSerializer"/> which handles internal message storage.
/// HTTP serialization is always JSON for interoperability with external systems.
/// </para>
/// <para>
/// The Stream-based API is optimized for HTTP scenarios where data is read from or
/// written to network streams directly, avoiding intermediate byte array allocations.
/// </para>
/// </remarks>
public interface IHttpSerializer
{
	/// <summary>
	/// Serializes an object to a stream as JSON using runtime type information.
	/// </summary>
	/// <param name="stream">The stream to write to.</param>
	/// <param name="value">The value to serialize.</param>
	/// <param name="type">The runtime type of the value.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	/// <exception cref="ArgumentNullException">Thrown when stream, value, or type is null.</exception>
	/// <exception cref="SerializationException">Thrown when serialization fails.</exception>
	[RequiresUnreferencedCode("JSON serialization with runtime type may require unreferenced code")]
	[RequiresDynamicCode("JSON serialization with runtime type requires dynamic code generation")]
	Task SerializeAsync(
		Stream stream,
		object value,
		Type type,
		CancellationToken cancellationToken);

	/// <summary>
	/// Deserializes JSON from a stream to an object using runtime type information.
	/// </summary>
	/// <param name="stream">The stream to read from.</param>
	/// <param name="type">The target type.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A task representing the deserialized object, or null if the JSON is null.</returns>
	/// <exception cref="ArgumentNullException">Thrown when stream or type is null.</exception>
	/// <exception cref="SerializationException">Thrown when deserialization fails.</exception>
	[RequiresUnreferencedCode("JSON deserialization with runtime type may require unreferenced code")]
	[RequiresDynamicCode("JSON deserialization with runtime type requires dynamic code generation")]
	Task<object?> DeserializeAsync(
		Stream stream,
		Type type,
		CancellationToken cancellationToken);

	/// <summary>
	/// Serializes an object to a byte array as JSON using runtime type information.
	/// </summary>
	/// <param name="value">The value to serialize.</param>
	/// <param name="type">The runtime type of the value.</param>
	/// <returns>A byte array containing the JSON representation.</returns>
	/// <exception cref="ArgumentNullException">Thrown when value or type is null.</exception>
	/// <exception cref="SerializationException">Thrown when serialization fails.</exception>
	[RequiresUnreferencedCode("JSON serialization with runtime type may require unreferenced code")]
	[RequiresDynamicCode("JSON serialization with runtime type requires dynamic code generation")]
	byte[] Serialize(object value, Type type);

	/// <summary>
	/// Deserializes JSON from a byte array to an object using runtime type information.
	/// </summary>
	/// <param name="data">The JSON bytes.</param>
	/// <param name="type">The target type.</param>
	/// <returns>The deserialized object, or null if the JSON is null.</returns>
	/// <exception cref="ArgumentNullException">Thrown when type is null.</exception>
	/// <exception cref="SerializationException">Thrown when deserialization fails.</exception>
	[RequiresUnreferencedCode("JSON deserialization with runtime type may require unreferenced code")]
	[RequiresDynamicCode("JSON deserialization with runtime type requires dynamic code generation")]
	object? Deserialize(ReadOnlySpan<byte> data, Type type);
}
