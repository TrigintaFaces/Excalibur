// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.IndexManagement;

namespace Excalibur.Data.ElasticSearch.Internal;

/// <summary>
/// Narrow internal seam over <see cref="Elastic.Clients.Elasticsearch.ElasticsearchClient"/>
/// index-template endpoints (<c>_inner.Indices.*IndexTemplate*</c>) used by
/// <see cref="IndexTemplateManager"/>. Companion seam:
/// <see cref="IComponentTemplateStore"/> covers the
/// <c>_inner.Cluster.*ComponentTemplate*</c> endpoints. Split along the SDK
/// sub-client boundary to keep each seam within a five-method surface.
/// </summary>
/// <remarks>
/// Naming: <c>IIndexTemplateStore</c> uses the <c>Store</c> domain-role
/// suffix. Split along the SDK sub-client
/// boundary (Indices vs Cluster) keeps each seam focused on a single domain
/// concept.
/// </remarks>
internal interface IIndexTemplateStore
{
	/// <summary>
	/// Creates or replaces an index template with the given name.
	/// </summary>
	Task<IndexTemplateOperationResult> PutAsync(
		string templateName,
		IndexTemplateConfiguration template,
		CancellationToken cancellationToken);

	/// <summary>
	/// Deletes the named index template.
	/// </summary>
	Task<IndexTemplateOperationResult> DeleteAsync(
		string templateName,
		CancellationToken cancellationToken);

	/// <summary>
	/// Returns <see langword="true"/> if the index template exists.
	/// </summary>
	Task<bool> ExistsAsync(string templateName, CancellationToken cancellationToken);

	/// <summary>
	/// Lists index templates matching the given name pattern (glob). The adapter
	/// projects the SDK response into framework-owned
	/// <see cref="IndexTemplateDescriptor"/> values so SDK types never cross
	/// this seam.
	/// </summary>
	Task<IReadOnlyList<IndexTemplateDescriptor>> ListAsync(
		string namePattern,
		CancellationToken cancellationToken);
}

/// <summary>
/// Result of an <see cref="IIndexTemplateStore"/> or
/// <see cref="IComponentTemplateStore"/> write operation. Domain shape — no
/// SDK types cross the seam on the result side.
/// </summary>
/// <param name="Success">Whether the operation was accepted by the store.</param>
/// <param name="ErrorDetails">Error diagnostics when <paramref name="Success"/> is false; otherwise null.</param>
internal readonly record struct IndexTemplateOperationResult(
	bool Success,
	string? ErrorDetails);
