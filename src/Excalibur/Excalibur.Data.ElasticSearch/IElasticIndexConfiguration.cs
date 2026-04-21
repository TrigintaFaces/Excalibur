// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Elastic.Clients.Elasticsearch.Mapping;

namespace Excalibur.Data.ElasticSearch;

/// <summary>
/// Configures the Elasticsearch index mapping for a document type.
/// </summary>
/// <remarks>
/// <para>
/// Implement this interface on your projection or document class to provide
/// explicit field mappings instead of relying on Elasticsearch dynamic mapping.
/// This follows the same pattern as EF Core's <c>IEntityTypeConfiguration&lt;T&gt;</c>.
/// </para>
/// <para>
/// When implemented, both <c>ElasticSearchProjectionStore&lt;T&gt;</c> and
/// <c>ElasticRepositoryBase&lt;T&gt;</c> will use these mappings during index creation.
/// </para>
/// <para>
/// <b>Three-tier mapping strategy:</b>
/// </para>
/// <list type="number">
/// <item>
/// <b>Explicit</b> (best): Implement this interface for full control over field types,
/// analyzers, and index settings.
/// </item>
/// <item>
/// <b>Inferred</b> (good default): When this interface is not implemented, the framework
/// reflects over public properties and maps them to appropriate Elasticsearch types
/// (keyword for strings/Guids, long/double for numerics, date for DateTime, etc.).
/// </item>
/// <item>
/// <b>Dynamic</b> (fallback): If both explicit and inferred mapping are bypassed,
/// Elasticsearch uses its own dynamic mapping rules, which can produce incorrect
/// types (e.g., mapping a numeric string as <c>long</c> instead of <c>keyword</c>).
/// </item>
/// </list>
/// </remarks>
/// <example>
/// <code>
/// public sealed class OrderSearchProjection : IElasticIndexConfiguration&lt;OrderSearchProjection&gt;
/// {
///     public Guid OrderId { get; set; }
///     public string CustomerName { get; set; }
///     public string Status { get; set; }
///     public decimal TotalAmount { get; set; }
///     public DateTime OrderDate { get; set; }
///     public List&lt;string&gt; Tags { get; set; }
///
///     public static Properties ConfigureIndex()
///     {
///         return new Properties
///         {
///             { "orderId", new KeywordProperty() },
///             { "customerName", new TextProperty
///                 {
///                     Fields = new Properties { { "keyword", new KeywordProperty { IgnoreAbove = 256 } } }
///                 }
///             },
///             { "status", new KeywordProperty() },
///             { "totalAmount", new DoubleNumberProperty() },
///             { "orderDate", new DateProperty() },
///             { "tags", new KeywordProperty() }
///         };
///     }
/// }
/// </code>
/// </example>
/// <typeparam name="TSelf">The document type that this configuration applies to.</typeparam>
public interface IElasticIndexConfiguration<TSelf> where TSelf : class
{
	/// <summary>
	/// Returns the Elasticsearch index field mappings for this document type.
	/// </summary>
	/// <returns>
	/// An Elasticsearch <see cref="Properties"/> dictionary mapping JSON field names
	/// to their property types. Use Elasticsearch mapping types such as:
	/// <see cref="KeywordProperty"/> for exact-match strings (IDs, codes, statuses),
	/// <see cref="TextProperty"/> for full-text searchable strings,
	/// <see cref="LongNumberProperty"/> or <see cref="DoubleNumberProperty"/> for numeric fields,
	/// <see cref="DateProperty"/> for date/time fields,
	/// <see cref="BooleanProperty"/> for boolean fields,
	/// <see cref="NestedProperty"/> for complex nested objects that need independent querying.
	/// </returns>
	static abstract Properties ConfigureIndex();
}
