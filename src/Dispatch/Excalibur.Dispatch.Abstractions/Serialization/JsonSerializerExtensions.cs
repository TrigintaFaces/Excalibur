// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

namespace Excalibur.Dispatch.Abstractions.Serialization;

/// <summary>
/// Extension methods for <see cref="IJsonSerializer" />.
/// </summary>
public static class JsonSerializerExtensions
{
	/// <summary>
	/// Serializes the specified value using the inferred generic type.
	/// </summary>
	/// <param name="serializer"> The serializer instance. </param>
	/// <param name="value"> The value to serialize. </param>
	/// <typeparam name="T"> The type of the value. </typeparam>
	/// <returns> A JSON string representation of the value. </returns>
	[RequiresUnreferencedCode("Generic type serialization may require types that cannot be statically analyzed")]
	[RequiresDynamicCode("Generic type serialization requires runtime code generation")]
	public static string Serialize<T>(this IJsonSerializer serializer, T value)
	{
		ArgumentNullException.ThrowIfNull(serializer);

		return serializer.Serialize(value, typeof(T));
	}

	/// <summary>
	/// Deserializes the provided JSON string into the specified type.
	/// </summary>
	/// <param name="serializer"> The serializer instance. </param>
	/// <param name="json"> The JSON string. </param>
	/// <typeparam name="T"> The target type. </typeparam>
	/// <returns> The deserialized value. </returns>
	[UnconditionalSuppressMessage(
		"AOT",
		"IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
		Justification =
			"This extension method delegates to IJsonSerializer which handles AOT compatibility through source generation or type registration.")]
	[RequiresDynamicCode("Generic type deserialization requires runtime code generation")]
	public static T? Deserialize<T>(this IJsonSerializer serializer, string json)
	{
		ArgumentNullException.ThrowIfNull(serializer);

		return (T?)serializer.Deserialize(json, typeof(T));
	}

	/// <summary>
	/// Serializes the specified value using the inferred generic type.
	/// </summary>
	/// <param name="serializer"> The serializer instance. </param>
	/// <param name="value"> The value to serialize. </param>
	/// <typeparam name="T"> The type of the value. </typeparam>
	/// <returns> A JSON string representation of the value. </returns>
	[UnconditionalSuppressMessage(
		"AOT",
		"IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
		Justification =
			"This extension method delegates to IJsonSerializer which handles AOT compatibility through source generation or type registration.")]
	[RequiresDynamicCode("Generic type serialization requires runtime code generation")]
	public static Task<string> SerializeAsync<T>(this IJsonSerializer serializer, T value)
	{
		ArgumentNullException.ThrowIfNull(serializer);

		return serializer.SerializeAsync(value, typeof(T));
	}

	/// <summary>
	/// Deserializes the provided JSON string into the specified type.
	/// </summary>
	/// <param name="serializer"> The serializer instance. </param>
	/// <param name="json"> The JSON string. </param>
	/// <typeparam name="T"> The target type. </typeparam>
	/// <returns> The deserialized value. </returns>
	[UnconditionalSuppressMessage(
		"AOT",
		"IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
		Justification =
			"This extension method delegates to IJsonSerializer which handles AOT compatibility through source generation or type registration.")]
	[RequiresDynamicCode("Generic type deserialization requires runtime code generation")]
	public static async Task<T?> DeserializeAsync<T>(this IJsonSerializer serializer, string json)
	{
		ArgumentNullException.ThrowIfNull(serializer);

		return (T?)await serializer.DeserializeAsync(json, typeof(T)).ConfigureAwait(false);
	}
}
