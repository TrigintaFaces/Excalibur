// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Elastic.Clients.Elasticsearch;

using Microsoft.Extensions.Logging;

namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Base class for compliance framework reporters.
/// </summary>
internal abstract class ComplianceReporter(ElasticsearchClient elasticsearchClient, ILogger logger)
{
	protected readonly ElasticsearchClient ElasticsearchClient = elasticsearchClient;
	protected readonly ILogger Logger = logger;

	public abstract Task<ComplianceReport> GenerateReportAsync(
		DateTimeOffset startTime,
		DateTimeOffset endTime,
		CancellationToken cancellationToken);

	public abstract Task<ComplianceViolation?> CheckComplianceViolationAsync(
		SecurityAuditEvent auditEvent,
		CancellationToken cancellationToken);
}
