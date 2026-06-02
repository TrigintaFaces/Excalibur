// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Elastic.Clients.Elasticsearch.Mapping;

namespace Excalibur.Data.ElasticSearch.Projections;

/// <summary>
/// Default implementation of <see cref="IIndexMappingConvention"/> that returns
/// the framework's inferred mappings unchanged.
/// </summary>
/// <remarks>
/// <para>
/// This convention applies the standard CLR-to-Elasticsearch type mapping:
/// <list type="bullet">
/// <item><c>string</c>, <c>Guid</c>, enums → <c>keyword</c></item>
/// <item><c>int</c>, <c>long</c>, etc. → <c>long</c></item>
/// <item><c>float</c>, <c>double</c>, <c>decimal</c> → <c>double</c></item>
/// <item><c>DateTime</c>, <c>DateTimeOffset</c> → <c>date</c></item>
/// <item><c>bool</c> → <c>boolean</c></item>
/// </list>
/// </para>
/// <para>
/// This is the convention used when no custom convention is specified in
/// <see cref="ElasticSearchProjectionStoreOptions.IndexMappingConvention"/>.
/// </para>
/// </remarks>
public sealed class DefaultIndexMappingConvention : IIndexMappingConvention
{
	/// <summary>
	/// Gets the singleton instance of the default convention.
	/// </summary>
	public static DefaultIndexMappingConvention Instance { get; } = new();

	/// <inheritdoc/>
	public Properties ConfigureMappings(Type projectionType, Properties inferredProperties)
	{
		ArgumentNullException.ThrowIfNull(projectionType);
		ArgumentNullException.ThrowIfNull(inferredProperties);

		// Default convention: accept the inferred mappings as-is
		return inferredProperties;
	}
}
