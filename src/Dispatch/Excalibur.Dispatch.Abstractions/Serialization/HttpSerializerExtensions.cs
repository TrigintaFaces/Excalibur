// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

namespace Excalibur.Dispatch.Abstractions.Serialization;

/// <summary>
/// Extension methods for <see cref="IHttpSerializer"/>.
/// </summary>
/// <remarks>
/// Provides generic <c>&lt;T&gt;</c> convenience overloads that delegate to the core
/// Type-based methods on <see cref="IHttpSerializer"/>.
/// </remarks>
public static class HttpSerializerExtensions
{
	/// <summary>
	/// JSON null literal bytes ("null").
	/// </summary>
	private static readonly byte[] JsonNullLiteral = "null"u8.ToArray();

	#region Generic Async Stream Overloads

	/// <summary>
	/// Serializes an object to a stream as JSON.
	/// </summary>
	/// <typeparam name="T">The type of the object to serialize.</typeparam>
	/// <param name="serializer">The HTTP serializer.</param>
	/// <param name="stream">The stream to write to.</param>
	/// <param name="value">The value to serialize.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	/// <exception cref="ArgumentNullException">Thrown when serializer or stream is null.</exception>
	/// <exception cref="SerializationException">Thrown when serialization fails.</exception>
	[RequiresUnreferencedCode("JSON serialization with runtime type may require unreferenced code")]
	[RequiresDynamicCode("JSON serialization with runtime type requires dynamic code generation")]
	public static async Task SerializeAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicFields)] T>(
		this IHttpSerializer serializer,
		Stream stream,
		T value,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(serializer);
		ArgumentNullException.ThrowIfNull(stream);

		if (value is null)
		{
			await stream.WriteAsync(JsonNullLiteral, cancellationToken).ConfigureAwait(false);
			return;
		}

		await serializer.SerializeAsync(stream, value, typeof(T), cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Deserializes JSON from a stream to an object.
	/// </summary>
	/// <typeparam name="T">The type to deserialize to.</typeparam>
	/// <param name="serializer">The HTTP serializer.</param>
	/// <param name="stream">The stream to read from.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A task representing the deserialized object, or null if the JSON is null.</returns>
	/// <exception cref="ArgumentNullException">Thrown when serializer or stream is null.</exception>
	/// <exception cref="SerializationException">Thrown when deserialization fails.</exception>
	[RequiresUnreferencedCode("JSON deserialization with runtime type may require unreferenced code")]
	[RequiresDynamicCode("JSON deserialization with runtime type requires dynamic code generation")]
	public static async Task<T?> DeserializeAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicFields)] T>(
		this IHttpSerializer serializer,
		Stream stream,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(serializer);
		ArgumentNullException.ThrowIfNull(stream);

		var result = await serializer.DeserializeAsync(stream, typeof(T), cancellationToken).ConfigureAwait(false);
		return (T?)result;
	}

	#endregion

	#region Generic Sync Overloads

	/// <summary>
	/// Serializes an object to a byte array as JSON.
	/// </summary>
	/// <typeparam name="T">The type of the object to serialize.</typeparam>
	/// <param name="serializer">The HTTP serializer.</param>
	/// <param name="value">The value to serialize.</param>
	/// <returns>A byte array containing the JSON representation.</returns>
	/// <exception cref="ArgumentNullException">Thrown when serializer is null.</exception>
	/// <exception cref="SerializationException">Thrown when serialization fails.</exception>
	[RequiresUnreferencedCode("JSON serialization with runtime type may require unreferenced code")]
	[RequiresDynamicCode("JSON serialization with runtime type requires dynamic code generation")]
	public static byte[] Serialize<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicFields)] T>(
		this IHttpSerializer serializer,
		T value)
	{
		ArgumentNullException.ThrowIfNull(serializer);

		if (value is null)
		{
			return (byte[])JsonNullLiteral.Clone();
		}

		return serializer.Serialize(value, typeof(T));
	}

	/// <summary>
	/// Deserializes JSON from a byte array to an object.
	/// </summary>
	/// <typeparam name="T">The type to deserialize to.</typeparam>
	/// <param name="serializer">The HTTP serializer.</param>
	/// <param name="data">The JSON bytes.</param>
	/// <returns>The deserialized object, or null if the JSON is null.</returns>
	/// <exception cref="ArgumentNullException">Thrown when serializer is null.</exception>
	/// <exception cref="SerializationException">Thrown when deserialization fails.</exception>
	[RequiresUnreferencedCode("JSON deserialization with runtime type may require unreferenced code")]
	[RequiresDynamicCode("JSON deserialization with runtime type requires dynamic code generation")]
	public static T? Deserialize<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicFields)] T>(
		this IHttpSerializer serializer,
		ReadOnlySpan<byte> data)
	{
		ArgumentNullException.ThrowIfNull(serializer);
		return (T?)serializer.Deserialize(data, typeof(T));
	}

	#endregion
}
