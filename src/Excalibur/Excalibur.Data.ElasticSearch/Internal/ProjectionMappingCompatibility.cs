// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Text.Json.Nodes;

namespace Excalibur.Data.ElasticSearch.Internal;

/// <summary>Checks required projection field semantics while permitting unrelated fields.</summary>
internal static class ProjectionMappingCompatibility
{
	internal static string? FindMismatch(JsonObject expectedProperties, JsonObject actualProperties) =>
		CompareProperties(expectedProperties, actualProperties, "properties");

	private static string? CompareProperties(JsonObject expected, JsonObject actual, string path)
	{
		foreach (var (name, required) in expected)
		{
			if (required is not JsonObject requiredField || actual[name] is not JsonObject storedField)
			{
				return $"{path}.{name} is missing";
			}

			var mismatch = CompareField(requiredField, storedField, $"{path}.{name}");
			if (mismatch is not null)
			{
				return mismatch;
			}
		}

		return null;
	}

	// Elasticsearch can omit explicitly declared defaults from GET _mapping.
	// Normalize only known defaults, never an explicitly different server value.
	private static bool IsOmittedServerDefault(JsonObject expected, JsonObject actual, string name, JsonNode? required)
	{
		if (actual.ContainsKey(name))
		{
			return false;
		}

		var type = expected["type"]?.GetValue<string>();
		JsonNode? defaultValue = name switch
		{
			"format" when type == "date" => JsonValue.Create("strict_date_optional_time||epoch_millis"),
			"format" when type == "date_nanos" => JsonValue.Create("strict_date_optional_time_nanos||epoch_millis"),
			"index" when type is not null and not "object" and not "nested" => JsonValue.Create(true),
			"doc_values" when type is "keyword" or "long" or "integer" or "short" or "byte" or "double" or "float"
				or "half_float" or "scaled_float" or "unsigned_long" or "boolean" or "date" or "date_nanos" or "ip" => JsonValue.Create(true),
			"enabled" when type is "object" or "nested" || expected.ContainsKey("properties") => JsonValue.Create(true),
			_ => null,
		};
		return defaultValue is not null && JsonNode.DeepEquals(required, defaultValue);
	}
	private static string? CompareField(JsonObject expected, JsonObject actual, string path)
	{
		foreach (var (name, required) in expected)
		{
			if (name is "properties" or "fields" && required is JsonObject requiredChildren)
			{
				if (actual[name] is not JsonObject storedChildren)
				{
					return $"{path}.{name} is missing";
				}

				var mismatch = CompareProperties(requiredChildren, storedChildren, $"{path}.{name}");
				if (mismatch is not null)
				{
					return mismatch;
				}
			}
			else if (!JsonNode.DeepEquals(required, actual[name])
				&& !IsOmittedServerDefault(expected, actual, name, required))
			{
				return $"{path}.{name} differs from the declared mapping";
			}
		}

		var type = expected["type"]?.GetValue<string>();
		if (type is "object" or "nested" || expected.ContainsKey("properties"))
		{
			if (!expected.ContainsKey("enabled") && actual["enabled"]?.GetValue<bool>() == false)
			{
				return $"{path}.enabled must be true";
			}
			return null;
		}

		if (!expected.ContainsKey("index") && actual["index"]?.GetValue<bool>() == false)
		{
			return $"{path}.index must be true";
		}
		if (type != "text" && !expected.ContainsKey("doc_values") && actual["doc_values"]?.GetValue<bool>() == false)
		{
			return $"{path}.doc_values must be true";
		}
		if (type == "keyword")
		{
			foreach (var parameter in new[] { "normalizer", "ignore_above" })
			{
				if (!expected.ContainsKey(parameter) && actual[parameter] is not null)
				{
					return $"{path}.{parameter} changes the declared exact-match semantics";
				}
			}
		}
		if (type == "text" && !expected.ContainsKey("analyzer")
			&& actual["analyzer"]?.GetValue<string>() is { } analyzer && analyzer != "standard")
		{
			return $"{path}.analyzer differs from the default standard analyzer";
		}

		return null;
	}
}
