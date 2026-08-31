// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Excalibur.Dispatch.Caching;

/// <summary>
/// AOT-compatible JSON converter for <see cref="CachedValue"/> that resolves types
/// via <see cref="JsonSerializerContext"/> instead of runtime assembly scanning.
/// </summary>
/// <remarks>
/// <para>
/// This converter is the AOT-safe alternative to <see cref="CachedValueJsonConverter"/>.
/// It resolves <see cref="CachedValue.TypeName"/> to a <see cref="JsonTypeInfo"/> from
/// the provided <see cref="JsonSerializerContext"/>, eliminating <c>AppDomain.GetAssemblies()</c>
/// and <c>Assembly.GetType()</c> calls.
/// </para>
/// <para>
/// Types not registered in the context fall back to <see cref="JsonElement"/> representation,
/// matching the existing fallback behavior of <see cref="CachedValueJsonConverter"/>.
/// </para>
/// </remarks>
internal sealed class AotCachedValueJsonConverter : JsonConverter<CachedValue>
{
	private readonly IReadOnlyDictionary<string, JsonTypeInfo> _typeMap;

	/// <summary>
	/// Initializes a new instance of the <see cref="AotCachedValueJsonConverter"/> class.
	/// </summary>
	/// <param name="context">The source-generated JSON serializer context containing cacheable type metadata.</param>
	public AotCachedValueJsonConverter(JsonSerializerContext context)
		: this(context, null)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="AotCachedValueJsonConverter"/> class
	/// with additional consumer-defined cacheable types.
	/// </summary>
	/// <param name="context">The source-generated JSON serializer context containing cacheable type metadata.</param>
	/// <param name="additionalTypes">
	/// Optional additional types to register beyond the common built-in set.
	/// Types not present in the <paramref name="context"/> are silently ignored.
	/// </param>
	public AotCachedValueJsonConverter(JsonSerializerContext context, IEnumerable<Type>? additionalTypes)
	{
		ArgumentNullException.ThrowIfNull(context);
		_typeMap = BuildTypeMap(context, additionalTypes);
	}

	/// <inheritdoc />
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
		string? actionTypeName = null;

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
				case CachedValueJsonConverter.ShouldCachePropertyName:
					shouldCache = reader.GetBoolean();
					break;
				case CachedValueJsonConverter.HasExecutedPropertyName:
					hasExecuted = reader.GetBoolean();
					break;
				case CachedValueJsonConverter.TypeNamePropertyName:
					typeName = reader.GetString();
					break;
				case CachedValueJsonConverter.ActionTypeNamePropertyName:
					actionTypeName = reader.GetString();
					break;
				case CachedValueJsonConverter.ValuePropertyName:
					// JsonElement.ParseValue reads the value straight off the reader. The generic
					// JsonSerializer.Deserialize<JsonElement> overload reaches the same result through the
					// reflection-based converter resolver, which ILC cannot prove safe.
					value = reader.TokenType == JsonTokenType.Null
							? null
							: JsonElement.ParseValue(ref reader);
					break;
				default:
					reader.Skip();
					break;
			}
		}

		// Resolve via JsonSerializerContext type map instead of assembly scanning
		if (value is JsonElement element && !string.IsNullOrEmpty(typeName))
		{
			if (_typeMap.TryGetValue(typeName, out var typeInfo))
			{
				try
				{
					value = JsonSerializer.Deserialize(element.GetRawText(), typeInfo);
				}
				catch (NotSupportedException)
				{
					// Retain JsonElement fallback
				}
			}
			// else: type not registered in context -- value stays as JsonElement (graceful fallback)
		}

		return new CachedValue
		{
			Value = value,
			ShouldCache = shouldCache,
			HasExecuted = hasExecuted,
			TypeName = typeName,
			ActionTypeName = actionTypeName,
		};
	}

	/// <inheritdoc />
	[System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "The type map is consulted first and the reflection-based fallback runs only for a type the supplied " +
		"context does not carry; registering the cacheable types with the context removes that path entirely.")]
	[System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Fallback path uses JsonSerializer.Serialize with runtime options. AOT consumers should register all types in the context.")]
	public override void Write(Utf8JsonWriter writer, CachedValue value, JsonSerializerOptions options)
	{
		writer.WriteStartObject();

		writer.WriteBoolean(CachedValueJsonConverter.ShouldCachePropertyName, value.ShouldCache);
		writer.WriteBoolean(CachedValueJsonConverter.HasExecutedPropertyName, value.HasExecuted);

		if (value.TypeName != null)
		{
			writer.WriteString(CachedValueJsonConverter.TypeNamePropertyName, value.TypeName);
		}

		if (value.ActionTypeName != null)
		{
			writer.WriteString(CachedValueJsonConverter.ActionTypeNamePropertyName, value.ActionTypeName);
		}

		writer.WritePropertyName(CachedValueJsonConverter.ValuePropertyName);
		if (value.Value is null)
		{
			writer.WriteNullValue();
		}
		else
		{
			var valueType = value.Value.GetType();
			var typeInfo = _typeMap.GetValueOrDefault(valueType.FullName ?? valueType.Name);
			if (typeInfo is not null)
			{
				JsonSerializer.Serialize(writer, value.Value, typeInfo);
			}
			else
			{
				// Fallback: write as JsonElement if available, otherwise raw object
				JsonSerializer.Serialize(writer, value.Value, options);
			}
		}

		writer.WriteEndObject();
	}

	private static Dictionary<string, JsonTypeInfo> BuildTypeMap(JsonSerializerContext context, IEnumerable<Type>? additionalTypes)
	{
		var map = new Dictionary<string, JsonTypeInfo>(StringComparer.Ordinal);

		// The context's Options.TypeInfoResolver provides all registered types.
		// We iterate known .NET types and map their full names to their JsonTypeInfo.
		if (context.Options.TypeInfoResolver is not null)
		{
			// Try common value types and registered types from the context
			foreach (var type in GetCommonCacheableTypes())
			{
				var typeInfo = context.GetTypeInfo(type);
				if (typeInfo is not null && type.FullName is not null)
				{
					map[type.FullName] = typeInfo;
				}
			}

			// Register consumer-provided additional types
			if (additionalTypes is not null)
			{
				foreach (var type in additionalTypes)
				{
					if (type.FullName is not null && !map.ContainsKey(type.FullName))
					{
						var typeInfo = context.GetTypeInfo(type);
						if (typeInfo is not null)
						{
							map[type.FullName] = typeInfo;
						}
					}
				}
			}
		}

		return map;
	}

	private static IEnumerable<Type> GetCommonCacheableTypes()
	{
		yield return typeof(string);
		yield return typeof(int);
		yield return typeof(long);
		yield return typeof(double);
		yield return typeof(decimal);
		yield return typeof(bool);
		yield return typeof(DateTime);
		yield return typeof(DateTimeOffset);
		yield return typeof(Guid);
		yield return typeof(byte[]);
	}
}
