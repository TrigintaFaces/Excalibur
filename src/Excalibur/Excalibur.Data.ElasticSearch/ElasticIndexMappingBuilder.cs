// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.Json.Serialization;

using Elastic.Clients.Elasticsearch.Mapping;

namespace Excalibur.Data.ElasticSearch;

/// <summary>
/// Builds Elasticsearch index field mappings from .NET types using a three-tier strategy:
/// explicit (<see cref="IElasticIndexConfiguration{TSelf}"/>), reflection-inferred, or
/// dynamic (no mappings — Elasticsearch guesses).
/// </summary>
/// <remarks>
/// <para>
/// This builder is used by both <c>ElasticSearchProjectionStore&lt;T&gt;</c> and
/// <c>ElasticRepositoryBase&lt;T&gt;</c> during index creation. It encapsulates the
/// mapping logic so both paths produce consistent, correct field types.
/// </para>
/// <para>
/// <b>Reflection-inferred mapping rules:</b>
/// </para>
/// <list type="bullet">
/// <item><c>string</c>, <c>Guid</c>, enums → <c>keyword</c> (exact match, sortable, aggregatable)</item>
/// <item><c>int</c>, <c>short</c>, <c>byte</c>, <c>long</c> → <c>long</c></item>
/// <item><c>float</c>, <c>double</c>, <c>decimal</c> → <c>double</c></item>
/// <item><c>DateTime</c>, <c>DateTimeOffset</c>, <c>DateOnly</c> → <c>date</c></item>
/// <item><c>bool</c> → <c>boolean</c></item>
/// <item><c>List&lt;string&gt;</c>, <c>string[]</c> → <c>keyword</c> (ES handles arrays natively)</item>
/// <item>Complex nested types → skipped (ES dynamic mapping for sub-fields)</item>
/// </list>
/// </remarks>
internal static class ElasticIndexMappingBuilder
{
	/// <summary>
	/// Builds the complete mapping properties for a document type using the three-tier strategy:
	/// explicit → inferred → dynamic.
	/// </summary>
	/// <typeparam name="TDocument">The document type.</typeparam>
	/// <returns>
	/// An Elasticsearch <see cref="Properties"/> dictionary. May be empty if the type has
	/// no recognizable properties (falling through to Elasticsearch dynamic mapping).
	/// </returns>
	public static Properties BuildMappingProperties<
		[DynamicallyAccessedMembers(
			DynamicallyAccessedMemberTypes.PublicProperties |
			DynamicallyAccessedMemberTypes.Interfaces)] TDocument>()
		where TDocument : class
	{
		// Tier 1: Explicit mapping via IElasticIndexConfiguration<TDocument>
		if (TryBuildExplicitMapping<TDocument>(out var explicitProperties))
		{
			return explicitProperties;
		}

		// Tier 2: Reflection-inferred mapping from public properties
		return BuildInferredMapping<TDocument>();
	}

	/// <summary>
	/// Attempts to build explicit mapping from <see cref="IElasticIndexConfiguration{TSelf}"/>.
	/// </summary>
	private static bool TryBuildExplicitMapping<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] TDocument>(
		out Properties properties)
		where TDocument : class
	{
		properties = new Properties();

		if (!typeof(IElasticIndexConfiguration<TDocument>).IsAssignableFrom(typeof(TDocument)))
		{
			return false;
		}

		// Invoke the static abstract ConfigureIndex() via a constrained trampoline.
		// We use MakeGenericMethod because TDocument is not constrained to
		// IElasticIndexConfiguration<TDocument> at this call site.
		// This reflection cost is acceptable — index creation happens once per app lifetime.
#pragma warning disable IL2060, IL3050 // MakeGenericMethod — safe because we verified assignability above
		var trampoline = typeof(ElasticIndexMappingBuilder)
			.GetMethod(nameof(InvokeConfigureIndex), BindingFlags.NonPublic | BindingFlags.Static)!
			.MakeGenericMethod(typeof(TDocument));
#pragma warning restore IL2060, IL3050

		properties = (Properties)trampoline.Invoke(null, null)!;
		return true;
	}

	/// <summary>
	/// Constrained trampoline that invokes the static abstract
	/// <see cref="IElasticIndexConfiguration{TSelf}.ConfigureIndex"/> method.
	/// </summary>
	private static Properties InvokeConfigureIndex<TDocument>()
		where TDocument : class, IElasticIndexConfiguration<TDocument>
	{
		return TDocument.ConfigureIndex();
	}

	/// <summary>
	/// Builds Elasticsearch field mappings by reflecting over the public properties
	/// of the document type. This is the "inferred" tier — better than dynamic mapping
	/// but less precise than explicit <see cref="IElasticIndexConfiguration{TSelf}"/>.
	/// </summary>
	/// <typeparam name="TDocument">The document type to reflect.</typeparam>
	/// <returns>
	/// An Elasticsearch <see cref="Properties"/> dictionary mapping JSON field names
	/// to their inferred Elasticsearch property types.
	/// </returns>
	public static Properties BuildInferredMapping<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TDocument>()
		where TDocument : class
	{
		var properties = new Properties();

		foreach (var property in typeof(TDocument).GetProperties(BindingFlags.Public | BindingFlags.Instance))
		{
			if (!property.CanRead)
			{
				continue;
			}

			var jsonName =
				property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ??
				ToCamelCase(property.Name);

			var esProperty = MapToElasticProperty(property.PropertyType);

			if (esProperty is not null)
			{
				properties[jsonName] = esProperty;
			}
			// If null, let ES dynamic mapping handle it (complex/unknown types)
		}

		return properties;
	}

	/// <summary>
	/// Maps a .NET type to the appropriate Elasticsearch property type.
	/// </summary>
	private static IProperty? MapToElasticProperty(Type propertyType)
	{
		var type = Nullable.GetUnderlyingType(propertyType) ?? propertyType;

		// String-like types → keyword (exact match, sortable, aggregatable)
		// Consumers wanting full-text search should use IElasticIndexConfiguration
		// to explicitly declare text fields with analyzers.
		if (type == typeof(string) || type == typeof(Guid))
		{
			return new KeywordProperty();
		}

		if (type.IsEnum)
		{
			return new KeywordProperty();
		}

		// Date types → date
		if (type == typeof(DateTime) || type == typeof(DateTimeOffset) || type == typeof(DateOnly))
		{
			return new DateProperty();
		}

		// Boolean → boolean
		if (type == typeof(bool))
		{
			return new BooleanProperty();
		}

		// Integer numeric types → long
		if (type == typeof(byte) || type == typeof(sbyte) ||
			type == typeof(short) || type == typeof(ushort) ||
			type == typeof(int) || type == typeof(uint) ||
			type == typeof(long) || type == typeof(ulong))
		{
			return new LongNumberProperty();
		}

		// Floating-point numeric types → double (covers decimal precision needs)
		if (type == typeof(float) || type == typeof(double) || type == typeof(decimal))
		{
			return new DoubleNumberProperty();
		}

		// Collection of strings → keyword (ES handles arrays natively)
		if (IsStringCollection(type))
		{
			return new KeywordProperty();
		}

		// Complex types (nested objects, dictionaries, etc.) → let ES infer via object mapping
		// Consumers needing nested queries should use IElasticIndexConfiguration
		return null;
	}

	/// <summary>
	/// Checks if a type is a collection of strings (List&lt;string&gt;, string[], IEnumerable&lt;string&gt;, etc.).
	/// </summary>
	private static bool IsStringCollection(Type type)
	{
		if (type == typeof(string[]) || type == typeof(List<string>))
		{
			return true;
		}

		if (type.IsGenericType)
		{
			var elementType = type.GetGenericArguments().FirstOrDefault();

			if (elementType == typeof(string))
			{
				var genericDef = type.GetGenericTypeDefinition();
				return genericDef == typeof(IEnumerable<>) ||
					   genericDef == typeof(ICollection<>) ||
					   genericDef == typeof(IList<>) ||
					   genericDef == typeof(IReadOnlyList<>) ||
					   genericDef == typeof(IReadOnlyCollection<>) ||
					   genericDef == typeof(List<>) ||
					   genericDef == typeof(HashSet<>);
			}
		}

		return false;
	}

	/// <summary>
	/// Converts a PascalCase property name to camelCase for JSON serialization.
	/// </summary>
	private static string ToCamelCase(string value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return value;
		}

		if (value.Length == 1)
		{
			return value.ToLowerInvariant();
		}

		return string.Concat(char.ToLowerInvariant(value[0]).ToString(), value.AsSpan(1).ToString());
	}
}
