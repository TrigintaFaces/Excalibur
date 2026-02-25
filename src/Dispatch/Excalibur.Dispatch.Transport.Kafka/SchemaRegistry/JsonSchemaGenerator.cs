// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;

namespace Excalibur.Dispatch.Transport.Kafka;

/// <summary>
/// Generates JSON Schema from .NET types using System.Text.Json.
/// </summary>
/// <remarks>
/// <para>
/// This utility uses .NET 9's built-in JSON Schema generation via
/// <see cref="JsonSchemaExporterOptions"/> and <c>GetJsonSchemaAsNode</c>.
/// </para>
/// <para>
/// The default options use camelCase property naming for JSON convention compatibility
/// and no indentation for wire efficiency.
/// </para>
/// <para>
/// Custom schema annotations can be included by setting
/// <see cref="JsonSchemaOptions.IncludeAnnotations"/> to <see langword="true"/>.
/// </para>
/// </remarks>
public static class JsonSchemaGenerator
{
	private static readonly JsonSerializerOptions DefaultSerializerOptions = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		WriteIndented = false
	};

	private static readonly JsonSchemaExporterOptions DefaultExporterOptions = new()
	{
		TreatNullObliviousAsNonNullable = true
	};

	/// <summary>
	/// Generates a JSON Schema string for the specified type.
	/// </summary>
	/// <typeparam name="T">The type to generate a schema for.</typeparam>
	/// <returns>The JSON Schema as a string.</returns>
	public static string Generate<T>() => Generate(typeof(T), DefaultSerializerOptions);

	/// <summary>
	/// Generates a JSON Schema string for the specified type.
	/// </summary>
	/// <param name="type">The type to generate a schema for.</param>
	/// <returns>The JSON Schema as a string.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="type"/> is <see langword="null"/>.</exception>
	public static string Generate(Type type) => Generate(type, DefaultSerializerOptions);

	/// <summary>
	/// Generates a JSON Schema string for the specified type with custom options.
	/// </summary>
	/// <typeparam name="T">The type to generate a schema for.</typeparam>
	/// <param name="options">The schema generation options.</param>
	/// <returns>The JSON Schema as a string.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
	public static string Generate<T>(JsonSchemaOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);
		return Generate(typeof(T), options);
	}

	/// <summary>
	/// Generates a JSON Schema string for the specified type with custom options.
	/// </summary>
	/// <param name="type">The type to generate a schema for.</param>
	/// <param name="options">The schema generation options.</param>
	/// <returns>The JSON Schema as a string.</returns>
	/// <exception cref="ArgumentNullException">
	/// <paramref name="type"/> or <paramref name="options"/> is <see langword="null"/>.
	/// </exception>
	public static string Generate(Type type, JsonSchemaOptions options)
	{
		ArgumentNullException.ThrowIfNull(type);
		ArgumentNullException.ThrowIfNull(options);

		var serializerOptions = options.JsonSerializerOptions ?? DefaultSerializerOptions;
		var exporterOptions = CreateExporterOptions(options);

		var schemaNode = serializerOptions.GetJsonSchemaAsNode(type, exporterOptions);
		return schemaNode.ToJsonString();
	}

	/// <summary>
	/// Generates a JSON Schema string for the specified type using custom serializer options.
	/// </summary>
	/// <param name="type">The type to generate a schema for.</param>
	/// <param name="options">The JSON serializer options to use for schema generation.</param>
	/// <returns>The JSON Schema as a string.</returns>
	/// <exception cref="ArgumentNullException">
	/// <paramref name="type"/> or <paramref name="options"/> is <see langword="null"/>.
	/// </exception>
	public static string Generate(Type type, JsonSerializerOptions options)
	{
		ArgumentNullException.ThrowIfNull(type);
		ArgumentNullException.ThrowIfNull(options);

		var schemaNode = options.GetJsonSchemaAsNode(type, DefaultExporterOptions);
		return schemaNode.ToJsonString();
	}

	/// <summary>
	/// Generates a JSON Schema string for the specified type using custom serializer and exporter options.
	/// </summary>
	/// <param name="type">The type to generate a schema for.</param>
	/// <param name="serializerOptions">The JSON serializer options to use.</param>
	/// <param name="exporterOptions">The JSON Schema exporter options to use.</param>
	/// <returns>The JSON Schema as a string.</returns>
	/// <exception cref="ArgumentNullException">
	/// Any argument is <see langword="null"/>.
	/// </exception>
	public static string Generate(
		Type type,
		JsonSerializerOptions serializerOptions,
		JsonSchemaExporterOptions exporterOptions)
	{
		ArgumentNullException.ThrowIfNull(type);
		ArgumentNullException.ThrowIfNull(serializerOptions);
		ArgumentNullException.ThrowIfNull(exporterOptions);

		var schemaNode = serializerOptions.GetJsonSchemaAsNode(type, exporterOptions);
		return schemaNode.ToJsonString();
	}

	private static JsonSchemaExporterOptions CreateExporterOptions(JsonSchemaOptions options)
	{
		return new JsonSchemaExporterOptions
		{
			TreatNullObliviousAsNonNullable = options.TreatNullObliviousAsNonNullable,
			TransformSchemaNode = options.IncludeAnnotations
				? TransformWithAnnotations
				: null
		};
	}

	[RequiresDynamicCode("Calls System.Text.Json.Nodes.JsonArray.Add<T>(T)")]
	[RequiresUnreferencedCode("Calls System.Text.Json.Nodes.JsonArray.Add<T>(T)")]
	private static JsonNode TransformWithAnnotations(
		JsonSchemaExporterContext ctx,
		JsonNode node)
	{
		// Only process property-level annotations
		var attributeProvider = ctx.PropertyInfo?.AttributeProvider;
		if (attributeProvider == null || node is not JsonObject schemaObject)
		{
			return node;
		}

		// Process [SchemaDescription]
		var descAttr = attributeProvider.GetCustomAttributes(typeof(SchemaDescriptionAttribute), true)
			.OfType<SchemaDescriptionAttribute>()
			.FirstOrDefault();
		if (descAttr != null)
		{
			schemaObject["description"] = descAttr.Description;
		}

		// Process [SchemaDeprecated]
		var deprecatedAttr = attributeProvider.GetCustomAttributes(typeof(SchemaDeprecatedAttribute), true)
			.OfType<SchemaDeprecatedAttribute>()
			.FirstOrDefault();
		if (deprecatedAttr != null)
		{
			schemaObject["deprecated"] = true;
		}

		// Process [SchemaExample] (supports multiple)
		var exampleAttrs = attributeProvider.GetCustomAttributes(typeof(SchemaExampleAttribute), true)
			.OfType<SchemaExampleAttribute>()
			.ToList();
		if (exampleAttrs.Count > 0)
		{
			var examples = new JsonArray();
			foreach (var exampleAttr in exampleAttrs)
			{
				examples.Add(JsonValue.Create(exampleAttr.Example));
			}
			schemaObject["examples"] = examples;
		}

		return node;
	}
}
