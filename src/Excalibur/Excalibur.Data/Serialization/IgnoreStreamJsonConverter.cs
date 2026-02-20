// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Text.Json;
using System.Text.Json.Serialization;

namespace Excalibur.Data.Serialization;

/// <summary>
/// A custom JSON converter that ignores properties of type <see cref="Stream" /> during JSON serialization.
/// </summary>
/// <remarks>
/// This converter provides customization for JSON serialization using System.Text.Json. It excludes all properties that are assignable to
/// the <see cref="Stream" /> type, ensuring such properties are not serialized. This is particularly useful when serializing objects that
/// contain streams, such as file handlers or data streams, which are not serializable.
/// </remarks>
internal sealed class IgnoreStreamJsonConverter : JsonConverter<Stream>
{
	/// <summary>
	/// Determines whether the specified type can be converted.
	/// </summary>
	/// <param name="typeToConvert"> The type to check for conversion support. </param>
	/// <returns> True if the type is assignable to Stream; otherwise, false. </returns>
	public override bool CanConvert(Type typeToConvert) => typeof(Stream).IsAssignableFrom(typeToConvert);

	/// <summary>
	/// Reads and converts the JSON to a Stream. Returns <see langword="null"/> because Stream properties
	/// cannot be meaningfully deserialized from JSON.
	/// </summary>
	/// <param name="reader"> The reader to read from. </param>
	/// <param name="typeToConvert"> The type of object to convert. </param>
	/// <param name="options"> The serializer options. </param>
	/// <returns> Always <see langword="null"/> since streams cannot be deserialized from JSON. </returns>
	public override Stream? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		// Skip over whatever JSON value is present (null, string, object, etc.)
		reader.Skip();
		return null;
	}

	/// <summary>
	/// Writes the Stream value to JSON. This implementation skips writing the value, effectively ignoring the stream property.
	/// </summary>
	/// <param name="writer"> The writer to write to. </param>
	/// <param name="value"> The Stream value to write. </param>
	/// <param name="options"> The serializer options. </param>
	public override void Write(Utf8JsonWriter writer, Stream value, JsonSerializerOptions options) =>

		// Skip writing stream properties - effectively ignoring them during serialization
		writer.WriteNullValue();
}
