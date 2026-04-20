// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.IndexManagement;
using Elastic.Clients.Elasticsearch.Mapping;

using Excalibur.Data.ElasticSearch.IndexManagement;

namespace Excalibur.Data.ElasticSearch.Internal;

/// <summary>
/// Default <see cref="IIndexTemplateStore"/> implementation that forwards to
/// <c>_inner.Indices.*IndexTemplate*</c> endpoints on a real
/// <see cref="ElasticsearchClient"/>. Split per ADR-142 §D7 "Surface area"
/// ≤5-method cap (S799 F1 remediation, OVERWATCH msg 1818); companion
/// adapter: <see cref="ComponentTemplateStoreAdapter"/>.
/// </summary>
internal sealed class IndexTemplateStoreAdapter : IIndexTemplateStore
{
	private readonly ElasticsearchClient _inner;

	/// <summary>
	/// Initializes a new instance of the <see cref="IndexTemplateStoreAdapter"/> class.
	/// </summary>
	/// <param name="inner"> The underlying Elasticsearch client. </param>
	public IndexTemplateStoreAdapter(ElasticsearchClient inner)
	{
		ArgumentNullException.ThrowIfNull(inner);
		_inner = inner;
	}

	/// <inheritdoc/>
	public async Task<IndexTemplateOperationResult> PutAsync(
		string templateName,
		IndexTemplateConfiguration template,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(templateName);
		ArgumentNullException.ThrowIfNull(template);

		var request = new PutIndexTemplateRequest(templateName)
		{
			IndexPatterns = template.IndexPatterns.Select(static p => (IndexName)p).ToArray(),
			Priority = template.Priority,
			Version = template.Version,
			Template = new IndexTemplateMapping
			{
				Settings = template.Template,
				Mappings = template.Mappings,
			},
			ComposedOf = template.ComposedOf?.Select(static x => (Name)x).ToList(),
			Meta = template.Metadata?.ToDictionary(static k => k.Key, static k => k.Value!),
		};

		if (template.DataStream is not null)
		{
			request.DataStream = new DataStreamVisibility { Hidden = template.DataStream.Hidden };
		}

		var response = await _inner.Indices
			.PutIndexTemplateAsync(request, cancellationToken)
			.ConfigureAwait(false);

		return new IndexTemplateOperationResult(
			response.IsValidResponse,
			response.IsValidResponse ? null : response.DebugInformation);
	}

	/// <inheritdoc/>
	public async Task<IndexTemplateOperationResult> DeleteAsync(
		string templateName,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(templateName);

		var response = await _inner.Indices
			.DeleteIndexTemplateAsync(templateName, cancellationToken)
			.ConfigureAwait(false);

		return new IndexTemplateOperationResult(
			response.IsValidResponse,
			response.IsValidResponse ? null : response.DebugInformation);
	}

	/// <inheritdoc/>
	public async Task<bool> ExistsAsync(string templateName, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(templateName);

		var response = await _inner.Indices
			.ExistsIndexTemplateAsync(templateName, cancellationToken)
			.ConfigureAwait(false);

		return response.IsValidResponse;
	}

	/// <inheritdoc/>
	public async Task<IReadOnlyList<IndexTemplateDescriptor>> ListAsync(
		string namePattern,
		CancellationToken cancellationToken)
	{
		var request = new GetIndexTemplateRequest(namePattern ?? "*");
		var response = await _inner.Indices
			.GetIndexTemplateAsync(request, cancellationToken)
			.ConfigureAwait(false);

		if (!response.IsValidResponse || response.IndexTemplates is null)
		{
			return [];
		}

		var result = new List<IndexTemplateDescriptor>(response.IndexTemplates.Count);
		foreach (var item in response.IndexTemplates)
		{
			result.Add(Project(item));
		}

		return result;
	}

	private static IndexTemplateDescriptor Project(IndexTemplateItem item)
	{
		var name = item.Name?.ToString() ?? string.Empty;
		var inner = item.IndexTemplate;

		var patterns = inner?.IndexPatterns is { } ip
			? ip.Select(static p => p?.ToString() ?? string.Empty).ToArray()
			: Array.Empty<string>();

		IReadOnlyList<string>? composedOf = null;
		if (inner?.ComposedOf is { } co && co.Count > 0)
		{
			composedOf = co.Select(static n => n?.ToString() ?? string.Empty).ToArray();
		}

		IReadOnlyDictionary<string, string>? metadata = null;
		if (inner?.Meta is { } meta && meta.Count > 0)
		{
			var dict = new Dictionary<string, string>(meta.Count, StringComparer.Ordinal);
			foreach (var kvp in meta)
			{
				dict[kvp.Key] = kvp.Value?.ToString() ?? string.Empty;
			}
			metadata = dict;
		}

		return new IndexTemplateDescriptor
		{
			Name = name,
			IndexPatterns = patterns,
			Priority = inner?.Priority is { } p ? checked((int)p) : null,
			Version = inner?.Version,
			ComposedOf = composedOf,
			Metadata = metadata,
		};
	}
}
