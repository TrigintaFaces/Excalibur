// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Core.Bulk;
using Elastic.Clients.Elasticsearch.IndexManagement;
using Elastic.Clients.Elasticsearch.Mapping;

using Excalibur.Data.ElasticSearch.Security;

namespace Excalibur.Data.ElasticSearch.Internal;

/// <summary>
/// Default <see cref="ISecurityAuditStore"/> implementation that forwards to
/// a real <see cref="ElasticsearchClient"/>. This adapter is intentionally
/// the only place in the framework's security-audit pipeline that touches
/// live Elasticsearch SDK call sites (<c>Indices.ExistsIndexTemplateAsync</c>,
/// <c>Indices.PutIndexTemplateAsync</c>, <c>BulkAsync</c>) — tests
/// substitute at the <see cref="ISecurityAuditStore"/> seam, never at the
/// SDK types directly (ADR-142 §D7, S799 <c>bd-iqlx2p</c>).
/// </summary>
internal sealed class SecurityAuditStoreAdapter : ISecurityAuditStore
{
	private const string AuditIndexTemplateName = "security-audit-template";
	private const string AuditIndexPattern = "security-audit-*";
	private const string AuditIndexPrefix = "security-audit";

	private readonly ElasticsearchClient _inner;

	/// <summary>
	/// Initializes a new instance of the <see cref="SecurityAuditStoreAdapter"/> class.
	/// </summary>
	/// <param name="inner"> The underlying Elasticsearch client. </param>
	public SecurityAuditStoreAdapter(ElasticsearchClient inner)
	{
		ArgumentNullException.ThrowIfNull(inner);
		_inner = inner;
	}

	/// <inheritdoc/>
	public async Task<bool> EnsureAuditIndexTemplateAsync(CancellationToken cancellationToken)
	{
		var existsResponse = await _inner.Indices
			.ExistsIndexTemplateAsync(AuditIndexTemplateName, cancellationToken)
			.ConfigureAwait(false);

		if (existsResponse.Exists)
		{
			return false;
		}

		var template = BuildAuditIndexTemplateRequest();
		_ = await _inner.Indices
			.PutIndexTemplateAsync(template, cancellationToken)
			.ConfigureAwait(false);

		return true;
	}

	/// <inheritdoc/>
	public async Task<AuditBulkAppendResult> BulkAppendEventsAsync(
		IReadOnlyList<SecurityAuditEvent> events,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(events);

		var indexName = $"{AuditIndexPrefix}-{DateTimeOffset.UtcNow:yyyy-MM}";
		var bulkRequest = new BulkRequest(indexName)
		{
			Operations = new List<IBulkOperation>(),
		};

		foreach (var auditEvent in events)
		{
			bulkRequest.Operations.Add(
				new BulkIndexOperation<SecurityAuditEvent>(auditEvent) { Id = auditEvent.EventId });
		}

		var response = await _inner
			.BulkAsync(bulkRequest, cancellationToken)
			.ConfigureAwait(false);

		return new AuditBulkAppendResult(
			Success: response.IsValidResponse,
			ErrorDetails: response.IsValidResponse ? null : response.DebugInformation,
			AppendedCount: events.Count);
	}

	private static PutIndexTemplateRequest BuildAuditIndexTemplateRequest()
	{
		return new PutIndexTemplateRequest(AuditIndexTemplateName)
		{
			IndexPatterns = new[] { AuditIndexPattern },
			Template = new IndexTemplateMapping
			{
				Settings = new IndexSettings
				{
					NumberOfShards = 1,
					NumberOfReplicas = 1,
				},
				Mappings = new TypeMapping
				{
					Properties = new Properties
					{
						["eventId"] = new KeywordProperty(),
						["timestamp"] = new DateProperty { Format = "strict_date_time" },
						["eventType"] = new KeywordProperty(),
						["severity"] = new KeywordProperty(),
						["source"] = new KeywordProperty(),
						["userId"] = new KeywordProperty(),
						["sourceIpAddress"] = new IpProperty(),
						["userAgent"] = new TextProperty(),
						["details"] = new ObjectProperty(),
						["integrityHash"] = new KeywordProperty(),
					},
				},
			},
		};
	}
}
