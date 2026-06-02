// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Elastic.Clients.Elasticsearch.Mapping;

namespace Excalibur.Data.ElasticSearch.Projections;

/// <summary>
/// Defines a convention for configuring Elasticsearch index mappings for projection types.
/// </summary>
/// <remarks>
/// <para>
/// Consumers implement this interface to customize how .NET projection types
/// are mapped to Elasticsearch field types. The framework provides
/// <see cref="DefaultIndexMappingConvention"/> which infers mappings from
/// CLR property types (string → keyword, int → long, etc.).
/// </para>
/// <para>
/// <b>Usage:</b>
/// <code>
/// services.AddElasticSearchProjectionStore&lt;OrderSummary&gt;(opts =&gt;
/// {
///     opts.IndexMappingConvention = new TextFieldConvention();
/// });
/// </code>
/// </para>
/// <para>
/// Register a custom convention to override default behavior globally, e.g.,
/// mapping all strings as <c>text</c> with a <c>keyword</c> sub-field,
/// or applying custom analyzers to specific property name patterns.
/// </para>
/// </remarks>
public interface IIndexMappingConvention
{
	/// <summary>
	/// Configures the Elasticsearch index mappings for the specified projection type.
	/// </summary>
	/// <param name="projectionType">The .NET projection type being mapped.</param>
	/// <param name="inferredProperties">
	/// The inferred property mappings from the framework's reflection-based mapping.
	/// Implementations may modify, augment, or replace these entirely.
	/// </param>
	/// <returns>
	/// The final <see cref="Properties"/> to use for index creation.
	/// Return <paramref name="inferredProperties"/> unchanged to accept defaults.
	/// </returns>
	Properties ConfigureMappings(Type projectionType, Properties inferredProperties);
}
