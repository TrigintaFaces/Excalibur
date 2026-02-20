// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Text.Json;
using System.Text.Json.Nodes;

namespace Excalibur.Dispatch.Transport.Kafka;

/// <summary>
/// Validates JSON Schema structure for Confluent Schema Registry compatibility.
/// </summary>
/// <remarks>
/// <para>
/// This validator performs lightweight local validation to catch obvious errors
/// before making a network call to the Schema Registry.
/// </para>
/// <para>
/// Validated checks include:
/// </para>
/// <list type="bullet">
///   <item><description>Valid JSON syntax</description></item>
///   <item><description>Root object contains <c>type</c> property</description></item>
///   <item><description><c>type</c> value is a valid JSON Schema type</description></item>
/// </list>
/// </remarks>
public sealed class JsonSchemaValidator : ISchemaValidator
{
	private static readonly HashSet<string> ValidTypes = new(StringComparer.OrdinalIgnoreCase)
	{
		"object",
		"array",
		"string",
		"number",
		"integer",
		"boolean",
		"null"
	};

	/// <inheritdoc/>
	public SchemaValidationResult ValidateStructure(string schema)
	{
		if (string.IsNullOrWhiteSpace(schema))
		{
			return SchemaValidationResult.Failure("Schema cannot be null or empty");
		}

		JsonNode? rootNode;
		try
		{
			rootNode = JsonNode.Parse(schema);
		}
		catch (JsonException ex)
		{
			return SchemaValidationResult.Failure($"Invalid JSON syntax: {ex.Message}");
		}

		if (rootNode is not JsonObject rootObject)
		{
			return SchemaValidationResult.Failure("Schema must be a JSON object");
		}

		var errors = new List<string>();

		// Check for 'type' property
		if (!rootObject.TryGetPropertyValue("type", out var typeNode))
		{
			errors.Add("Schema must contain a 'type' property");
		}
		else if (typeNode is JsonValue typeValue)
		{
			var typeString = typeValue.GetValue<string>();
			if (!ValidTypes.Contains(typeString))
			{
				errors.Add($"Invalid schema type: '{typeString}'. Valid types are: {string.Join(", ", ValidTypes)}");
			}
		}
		else if (typeNode is JsonArray typeArray)
		{
			// Union types are valid (e.g., ["string", "null"])
			foreach (var item in typeArray)
			{
				if (item is JsonValue itemValue)
				{
					var itemString = itemValue.GetValue<string>();
					if (!ValidTypes.Contains(itemString))
					{
						errors.Add($"Invalid schema type in array: '{itemString}'");
					}
				}
			}
		}

		// For object types, validate properties structure if present
		if (rootObject.TryGetPropertyValue("properties", out var propsNode) && propsNode is not JsonObject)
		{
			errors.Add("'properties' must be a JSON object");
		}

		// For array types, validate items structure if present
		if (rootObject.TryGetPropertyValue("items", out var itemsNode) && itemsNode is not JsonObject && itemsNode is not JsonArray)
		{
			errors.Add("'items' must be a JSON object or array");
		}

		return errors.Count == 0
			? SchemaValidationResult.Success
			: SchemaValidationResult.Failure(errors);
	}
}
