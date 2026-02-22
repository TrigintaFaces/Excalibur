// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Excalibur.Dispatch.Caching;

/// <summary>
/// Custom JSON converter for <see cref="CachedValue"/> that handles polymorphic serialization
/// of the Value property using the TypeName field.
/// </summary>
/// <remarks>
/// <para>
/// <strong>AOT / Trimming Behavior:</strong> This converter uses runtime type-name resolution
/// and <see cref="JsonSerializer.Deserialize(string, Type, JsonSerializerOptions)"/> to reconstruct
/// typed values from their serialized form. In AOT or trimmed environments, type resolution may fail
/// if the target type is not preserved. When this occurs, the <see cref="CachedValue.Value"/> property
/// falls back to a <see cref="System.Text.Json.JsonElement"/> representation, preserving the raw JSON
/// data without throwing. Consumers should check for <see cref="System.Text.Json.JsonElement"/> as
/// a fallback type when running in AOT scenarios.
/// </para>
/// </remarks>
public sealed class CachedValueJsonConverter : JsonConverter<CachedValue>
{
	/// <summary>JSON property name for the <see cref="CachedValue.ShouldCache"/> field.</summary>
	internal const string ShouldCachePropertyName = "ShouldCache";

	/// <summary>JSON property name for the <see cref="CachedValue.HasExecuted"/> field.</summary>
	internal const string HasExecutedPropertyName = "HasExecuted";

	/// <summary>JSON property name for the <see cref="CachedValue.TypeName"/> field.</summary>
	internal const string TypeNamePropertyName = "TypeName";

	/// <summary>JSON property name for the <see cref="CachedValue.Value"/> field.</summary>
	internal const string ValuePropertyName = "Value";

	/// <inheritdoc />
	[UnconditionalSuppressMessage(
		"Trimming",
		"IL2026:Members annotated with RequiresUnreferencedCodeAttribute may break with trimming",
		Justification = "Cached value serialization is optional and guarded with runtime type checks.")]
	[UnconditionalSuppressMessage(
		"Aot",
		"IL3050:Calling members annotated with RequiresDynamicCodeAttribute may break functionality when AOT compiling.",
		Justification = "Cached value serialization uses System.Text.Json for diagnostics.")]
	public override CachedValue? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		if (reader.TokenType != JsonTokenType.StartObject)
		{
			throw new JsonException(Resources.CachedValueJsonConverter_ExpectedStartObjectToken);
		}

		object? value = null;
		var shouldCache = false;
		var hasExecuted = false;
		string? typeName = null;

		while (reader.Read())
		{
			if (reader.TokenType == JsonTokenType.EndObject)
			{
				break;
			}

			if (reader.TokenType != JsonTokenType.PropertyName)
			{
				throw new JsonException(Resources.CachedValueJsonConverter_ExpectedPropertyNameToken);
			}

			var propertyName = reader.GetString();
			_ = reader.Read();

			switch (propertyName)
			{
				case ShouldCachePropertyName:
					shouldCache = reader.GetBoolean();
					break;
				case HasExecutedPropertyName:
					hasExecuted = reader.GetBoolean();
					break;
				case TypeNamePropertyName:
					typeName = reader.GetString();
					break;
				case ValuePropertyName:
					if (reader.TokenType == JsonTokenType.Null)
					{
						value = null;
					}
					else
					{
						// Store as JsonElement for now, will deserialize after reading TypeName
						value = JsonSerializer.Deserialize<JsonElement>(ref reader, options);
					}

					break;
				default:
					reader.Skip();
					break;
			}
		}

		// Attempt to deserialize Value to the correct type using TypeName.
		// In AOT/trimmed environments, type resolution or Deserialize may fail;
		// the value gracefully falls back to the JsonElement representation.
		if (value is JsonElement element && !string.IsNullOrEmpty(typeName))
		{
			var targetType = ResolveTypeByName(typeName);
			if (targetType != null)
			{
				try
				{
					value = JsonSerializer.Deserialize(element.GetRawText(), targetType, options);
				}
				catch (NotSupportedException)
				{
					// AOT/trimmed: serializer metadata unavailable for targetType.
					// Retain the JsonElement so callers can inspect the raw JSON.
				}
			}
		}

		return new CachedValue { Value = value, ShouldCache = shouldCache, HasExecuted = hasExecuted, TypeName = typeName };
	}

	/// <inheritdoc />
	[UnconditionalSuppressMessage(
		"Trimming",
		"IL2026:Members annotated with RequiresUnreferencedCodeAttribute may break with trimming",
		Justification = "Cached value serialization is optional and guarded with runtime type checks.")]
	[UnconditionalSuppressMessage(
		"Aot",
		"IL3050:Calling members annotated with RequiresDynamicCodeAttribute may break functionality when AOT compiling.",
		Justification = "Cached value serialization uses System.Text.Json for diagnostics.")]
	public override void Write(Utf8JsonWriter writer, CachedValue value, JsonSerializerOptions options)
	{
		writer.WriteStartObject();

		writer.WriteBoolean(ShouldCachePropertyName, value.ShouldCache);
		writer.WriteBoolean(HasExecutedPropertyName, value.HasExecuted);

		if (value.TypeName != null)
		{
			writer.WriteString(TypeNamePropertyName, value.TypeName);
		}

		writer.WritePropertyName(ValuePropertyName);
		if (value.Value == null)
		{
			writer.WriteNullValue();
		}
		else
		{
			JsonSerializer.Serialize(writer, value.Value, value.Value.GetType(), options);
		}

		writer.WriteEndObject();
	}

	private static Type? ResolveTypeByName(string typeName)
	{
		foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
		{
			var resolved = assembly.GetType(typeName, throwOnError: false, ignoreCase: false);
			if (resolved != null)
			{
				return resolved;
			}
		}

		var assemblySeparator = typeName.IndexOf(',', StringComparison.Ordinal);
		if (assemblySeparator <= 0)
		{
			return null;
		}

		var simpleTypeName = typeName[..assemblySeparator];
		foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
		{
			var resolved = assembly.GetType(simpleTypeName, throwOnError: false, ignoreCase: false);
			if (resolved != null)
			{
				return resolved;
			}
		}

		return null;
	}
}
