// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.IndexManagement;
using Elastic.Clients.Elasticsearch.Mapping;

using Excalibur.Data.ElasticSearch.IndexManagement;

namespace Excalibur.Data.ElasticSearch.Internal;

/// <summary>
/// Default <see cref="IComponentTemplateStore"/> implementation that forwards
/// to <c>_inner.Cluster.*ComponentTemplate*</c> endpoints on a real
/// <see cref="ElasticsearchClient"/>. Split per ADR-142 §D7 "Surface area"
/// ≤5-method cap (S799 F1 remediation, OVERWATCH msg 1818); companion
/// adapter: <see cref="IndexTemplateStoreAdapter"/>.
/// </summary>
internal sealed class ComponentTemplateStoreAdapter : IComponentTemplateStore
{
	private readonly ElasticsearchClient _inner;

	/// <summary>
	/// Initializes a new instance of the <see cref="ComponentTemplateStoreAdapter"/> class.
	/// </summary>
	/// <param name="inner"> The underlying Elasticsearch client. </param>
	public ComponentTemplateStoreAdapter(ElasticsearchClient inner)
	{
		ArgumentNullException.ThrowIfNull(inner);
		_inner = inner;
	}

	/// <inheritdoc/>
	public async Task<IndexTemplateOperationResult> PutAsync(
		string templateName,
		ComponentTemplateConfiguration template,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(templateName);
		ArgumentNullException.ThrowIfNull(template);

		var settings = DeserializeOrNull<IndexSettings>(template.SettingsJson);
		var mappings = DeserializeOrNull<TypeMapping>(template.MappingsJson);

		var response = await _inner.Cluster
			.PutComponentTemplateAsync(
				templateName,
				r => r
					.Version(template.Version)
					.Template(t => t
						.Settings(settings)
						.Mappings(mappings))
					.Meta(meta =>
					{
						if (template.Metadata is not null)
						{
							foreach (var kvp in template.Metadata)
							{
								_ = meta.Add(kvp.Key, kvp.Value ?? new object());
							}
						}
					}),
				cancellationToken)
			.ConfigureAwait(false);

		return new IndexTemplateOperationResult(
			response.IsValidResponse,
			response.IsValidResponse ? null : response.DebugInformation);
	}

	[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Elastic SDK types are inherently reflection-based; this adapter layer already depends on them.")]
	[UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Elastic SDK types are inherently reflection-based; this adapter layer already depends on them.")]
	private static T? DeserializeOrNull<T>(JsonElement? element) where T : class =>
		element.HasValue ? JsonSerializer.Deserialize<T>(element.Value) : null;

	/// <inheritdoc/>
	public async Task<IndexTemplateOperationResult> DeleteAsync(
		string templateName,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(templateName);

		var response = await _inner.Cluster
			.DeleteComponentTemplateAsync(templateName, cancellationToken)
			.ConfigureAwait(false);

		return new IndexTemplateOperationResult(
			response.IsValidResponse,
			response.IsValidResponse ? null : response.DebugInformation);
	}
}
