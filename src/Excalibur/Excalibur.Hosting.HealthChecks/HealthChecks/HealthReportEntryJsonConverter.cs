// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Excalibur.Hosting.HealthChecks;

/// <summary>
/// Converts <see cref="HealthReportEntry" /> objects to JSON representation.
/// </summary>
/// <remarks>
/// This converter handles the serialization of all properties in <see cref="HealthReportEntry" />, including nested data and tags. The
/// converter respects the <see cref="JsonSerializerOptions.PropertyNamingPolicy" /> for property names.
/// </remarks>
internal sealed class HealthReportEntryJsonConverter : JsonConverter<HealthReportEntry>
{
	/// <inheritdoc />
	public override HealthReportEntry Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => default;

	/// <inheritdoc />
	/// <remarks> Writes a <see cref="HealthReportEntry" /> to JSON format. </remarks>
	[RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(Utf8JsonWriter, TValue, JsonSerializerOptions)")]
	[RequiresDynamicCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(Utf8JsonWriter, TValue, JsonSerializerOptions)")]
	public override void Write(Utf8JsonWriter writer, HealthReportEntry value, JsonSerializerOptions options)
	{
		ArgumentNullException.ThrowIfNull(writer);

		writer.WriteStartObject();

		// Serialize the "Data" property
		writer.WritePropertyName(ConvertPropertyName(nameof(value.Data), options));
		JsonSerializer.Serialize(writer, value.Data, options);

		// Serialize the "Description" property
		writer.WriteString(
			ConvertPropertyName(nameof(value.Description), options),
			value.Description ?? value.Exception?.GetType().Name);

		// Serialize the "Duration" property
		writer.WriteString(
			ConvertPropertyName(nameof(value.Duration), options),
			value.Duration.ToString());

		// Serialize the "Exception" property
		writer.WriteString(
			ConvertPropertyName(nameof(value.Exception), options),
			value.Exception?.Message);

		// Serialize the "Status" property
		writer.WriteString(
			ConvertPropertyName(nameof(value.Status), options),
			value.Status.ToString());

		// Serialize the "Tags" property
		writer.WritePropertyName(ConvertPropertyName(nameof(value.Tags), options));
		JsonSerializer.Serialize(writer, value.Tags, options);

		writer.WriteEndObject();
	}

	/// <summary>
	/// Converts a property name to respect the specified <see cref="JsonSerializerOptions.PropertyNamingPolicy" />.
	/// </summary>
	/// <param name="propertyName"> The original property name. </param>
	/// <param name="options"> The serializer options containing the naming policy. </param>
	/// <returns> The converted property name, or the original name if no naming policy is set. </returns>
	private static string ConvertPropertyName(string propertyName, JsonSerializerOptions? options) =>
		options?.PropertyNamingPolicy?.ConvertName(propertyName) ?? propertyName;
}
