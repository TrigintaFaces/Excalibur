// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

namespace Excalibur.Dispatch.Abstractions.Serialization;

/// <summary>
/// Provides an abstraction over JSON serialization.
/// </summary>
public interface IJsonSerializer
{
	/// <summary>
	/// Serializes an object to a JSON string using the specified type.
	/// </summary>
	/// <param name="value"> The value to serialize. </param>
	/// <param name="type"> The runtime type of the value. </param>
	/// <returns> A JSON string representation of the value. </returns>
	[RequiresUnreferencedCode("JSON serialization with runtime type may require unreferenced code")]
	[RequiresDynamicCode("JSON serialization with runtime type requires dynamic code generation")]
	string Serialize(object value, Type type);

	/// <summary>
	/// Deserializes the provided JSON string to an object of the specified type.
	/// </summary>
	/// <param name="json"> The JSON string. </param>
	/// <param name="type"> The target type. </param>
	/// <returns> The deserialized object. </returns>
	[RequiresUnreferencedCode("JSON deserialization with runtime type may require unreferenced code")]
	[RequiresDynamicCode("JSON deserialization with runtime type requires dynamic code generation")]
	object? Deserialize(string json, Type type);

	/// <summary>
	/// Serializes an object to a JSON string using the specified type.
	/// </summary>
	/// <param name="value"> The value to serialize. </param>
	/// <param name="type"> The runtime type of the value. </param>
	/// <returns> A JSON string representation of the value. </returns>
	/// <remarks>
	/// Default implementation delegates to <see cref="Serialize"/>. Override only if the serializer
	/// supports native async serialization (e.g., stream-based).
	/// </remarks>
	[RequiresUnreferencedCode("JSON serialization with runtime type may require unreferenced code")]
	[RequiresDynamicCode("JSON serialization with runtime type requires dynamic code generation")]
	Task<string> SerializeAsync(object value, Type type) => Task.FromResult(Serialize(value, type));

	/// <summary>
	/// Deserializes the provided JSON string to an object of the specified type.
	/// </summary>
	/// <param name="json"> The JSON string. </param>
	/// <param name="type"> The target type. </param>
	/// <returns> The deserialized object. </returns>
	/// <remarks>
	/// Default implementation delegates to <see cref="Deserialize"/>. Override only if the serializer
	/// supports native async deserialization (e.g., stream-based).
	/// </remarks>
	[RequiresUnreferencedCode("JSON deserialization with runtime type may require unreferenced code")]
	[RequiresDynamicCode("JSON deserialization with runtime type requires dynamic code generation")]
	Task<object?> DeserializeAsync(string json, Type type) => Task.FromResult(Deserialize(json, type));
}
