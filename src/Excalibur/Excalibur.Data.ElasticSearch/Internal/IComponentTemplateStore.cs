// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.IndexManagement;

namespace Excalibur.Data.ElasticSearch.Internal;

/// <summary>
/// Narrow internal seam over <see cref="Elastic.Clients.Elasticsearch.ElasticsearchClient"/>
/// component-template endpoints (<c>_inner.Cluster.*ComponentTemplate*</c>)
/// used by <see cref="IndexTemplateManager"/>. Companion seam:
/// <see cref="IIndexTemplateStore"/> covers the
/// <c>_inner.Indices.*IndexTemplate*</c> endpoints. Split per ADR-142 §D7
/// "Surface area" ≤5-method cap (S799 F1 remediation, OVERWATCH msg 1818).
/// </summary>
/// <remarks>
/// Naming: <c>IComponentTemplateStore</c> uses the <c>Store</c> domain-role
/// suffix per COMPASS msg 1799 precedent. Splitting along the SDK sub-client
/// boundary (Indices vs Cluster) is a natural seam alignment since index
/// templates and component templates are conceptually distinct Elasticsearch
/// administration concerns.
/// </remarks>
internal interface IComponentTemplateStore
{
	/// <summary>
	/// Creates or replaces a component template with the given name.
	/// </summary>
	Task<IndexTemplateOperationResult> PutAsync(
		string templateName,
		ComponentTemplateConfiguration template,
		CancellationToken cancellationToken);

	/// <summary>
	/// Deletes the named component template.
	/// </summary>
	Task<IndexTemplateOperationResult> DeleteAsync(
		string templateName,
		CancellationToken cancellationToken);
}
