// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Metrics;

using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;

using Excalibur.Data.ElasticSearch.Diagnostics;

using Microsoft.Extensions.Logging;

namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Handles audit event searching, report generation, and compliance reporting.
/// </summary>
/// <remarks>
/// <para>
/// Extracted from <see cref="SecurityAuditor"/> following SRP. Contains all
/// read/query operations: audit reports, compliance reports, and event search.
/// </para>
/// </remarks>
internal sealed class SecurityAuditQueryService
{
	private static readonly Histogram<double> ReportDurationHistogram = AuditTelemetryConstants.Meter.CreateHistogram<double>(
		AuditTelemetryConstants.MetricNames.ReportDuration,
		"ms",
		"Duration of audit report generation in milliseconds");

	private readonly ElasticsearchClient _elasticsearchClient;
	private readonly AuditOptions _configuration;
	private readonly Dictionary<ComplianceFramework, ComplianceReporter> _complianceReporters;
	private readonly ILogger _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="SecurityAuditQueryService"/> class.
	/// </summary>
	internal SecurityAuditQueryService(
		ElasticsearchClient elasticsearchClient,
		AuditOptions configuration,
		Dictionary<ComplianceFramework, ComplianceReporter> complianceReporters,
		ILogger logger)
	{
		_elasticsearchClient = elasticsearchClient;
		_configuration = configuration;
		_complianceReporters = complianceReporters;
		_logger = logger;
	}

	/// <summary>
	/// Generates security recommendations based on audit events.
	/// </summary>
	internal static List<string> GenerateSecurityRecommendations(IList<SecurityAuditEvent> events)
	{
		var recommendations = new List<string>();

		// Analyze authentication failures
		var authFailures =
			events.Count(static e => e is { EventType: SecurityEventType.Authentication, Severity: >= SecurityEventSeverity.Medium });
		if (authFailures > 10)
		{
			recommendations.Add("High number of authentication failures detected. Consider implementing account lockout policies.");
		}

		// Analyze data access patterns
		var dataAccessCount = events.Count(static e => e.EventType == SecurityEventType.DataAccess);
		if (dataAccessCount > 1000)
		{
			recommendations.Add("High volume of data access events. Consider implementing data access monitoring and alerting.");
		}

		// Analyze critical events
		var criticalEvents = events.Count(static e => e.Severity == SecurityEventSeverity.Critical);
		if (criticalEvents > 0)
		{
			recommendations.Add($"{criticalEvents} critical security events detected. Immediate investigation required.");
		}

		// Analyze unique IP addresses
		var uniqueIps = events.Select(static e => e.SourceIpAddress).Where(static ip => !string.IsNullOrEmpty(ip))
			.Distinct(StringComparer.Ordinal).Count();
		if (uniqueIps > 100)
		{
			recommendations.Add(
				"High number of unique source IP addresses. Consider implementing IP whitelisting or geographical restrictions.");
		}

		if (recommendations.Count == 0)
		{
			recommendations.Add("No immediate security concerns identified. Continue monitoring.");
		}

		return recommendations;
	}

	/// <summary>
	/// Generates a comprehensive security audit report for the specified time period.
	/// </summary>
	internal async Task<AuditReport> GenerateAuditReportAsync(
		DateTimeOffset startTime,
		DateTimeOffset endTime,
		AuditReportType reportType,
		CancellationToken cancellationToken)
	{
		using var activity = AuditTelemetryConstants.ActivitySource.StartActivity("audit.generate_report");
		activity?.SetTag("audit.report_type", reportType.ToString());
		var reportStopwatch = Stopwatch.StartNew();

		try
		{
			_logger.LogInformation("Generating audit report from {StartTime} to {EndTime}", startTime, endTime);

			var maxResults = _configuration.MaxQueryResultSize;
			var searchResponse = await _elasticsearchClient.SearchAsync<SecurityAuditEvent>(
				s => s
					.Indices("security-audit-*")
					.Query(new MatchAllQuery())
					.Size(maxResults)
					.Sort(static so => so.Field(static f => f.Timestamp, new FieldSort { Order = SortOrder.Asc })),
				cancellationToken).ConfigureAwait(false);

			if (!searchResponse.IsValidResponse)
			{
				throw new InvalidOperationException($"Failed to retrieve audit logs: {searchResponse.DebugInformation}");
			}

			var events = searchResponse.Documents.ToList();

			// Calculate report statistics
			var authenticationEvents = events.Count(static e => e.EventType == SecurityEventType.Authentication);
			var dataAccessEvents = events.Count(static e => e.EventType == SecurityEventType.DataAccess);
			var configurationEvents = events.Count(static e => e.EventType == SecurityEventType.ConfigurationChange);
			var securityIncidents = events.Count(static e => e.EventType == SecurityEventType.SecurityIncident);

			var criticalEvents = events.Count(static e => e.Severity == SecurityEventSeverity.Critical);
			var highSeverityEvents = events.Count(static e => e.Severity == SecurityEventSeverity.High);

			var uniqueUsers = events.Select(static e => e.UserId).Where(static u => !string.IsNullOrEmpty(u))
				.Distinct(StringComparer.Ordinal).Count();
			var uniqueIpAddresses = events.Select(static e => e.SourceIpAddress).Where(static ip => !string.IsNullOrEmpty(ip))
				.Distinct(StringComparer.Ordinal)
				.Count();

			var report = new AuditReport
			{
				ReportId = Guid.NewGuid(),
				StartTime = startTime,
				EndTime = endTime,
				GeneratedAt = DateTimeOffset.UtcNow,
				ReportType = reportType,
				TotalEvents = events.Count,
				EventBreakdown =
					new Dictionary<string, int>
						(StringComparer.Ordinal)
					{
						["Authentication"] = authenticationEvents,
						["DataAccess"] = dataAccessEvents,
						["ConfigurationChange"] = configurationEvents,
						["SecurityIncident"] = securityIncidents,
					},
				SeverityBreakdown = new Dictionary<string, int>
					(StringComparer.Ordinal)
				{
					["Critical"] = criticalEvents,
					["High"] = highSeverityEvents,
					["Medium"] = events.Count(static e => e.Severity == SecurityEventSeverity.Medium),
					["Low"] = events.Count(static e => e.Severity == SecurityEventSeverity.Low),
				},
				UniqueUsers = uniqueUsers,
				UniqueSourceIps = uniqueIpAddresses,
				ComplianceStatus = await EvaluateComplianceStatusAsync(events, cancellationToken).ConfigureAwait(false),
				Recommendations = GenerateSecurityRecommendations(events),
			};

			reportStopwatch.Stop();
			ReportDurationHistogram.Record(reportStopwatch.Elapsed.TotalMilliseconds);
			activity?.SetTag("audit.events_analyzed", events.Count);

			_logger.LogInformation("Audit report generated with {EventCount} events analyzed", events.Count);
			return report;
		}
		catch (Exception ex)
		{
			reportStopwatch.Stop();
			activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
			_logger.LogError(ex, "Failed to generate audit report");
			throw;
		}
	}

	/// <summary>
	/// Generates a compliance report for specific regulatory frameworks.
	/// </summary>
	internal async Task<ComplianceReport> GenerateComplianceReportAsync(
		ComplianceFramework framework,
		DateTimeOffset startTime,
		DateTimeOffset endTime,
		CancellationToken cancellationToken)
	{
		if (!_complianceReporters.TryGetValue(framework, out var reporter))
		{
			throw new ArgumentException($"Compliance framework {framework} is not supported", nameof(framework));
		}

		try
		{
			_logger.LogInformation(
				"Generating compliance report for {Framework} from {StartTime} to {EndTime}",
				framework, startTime, endTime);

			var report = await reporter.GenerateReportAsync(startTime, endTime, cancellationToken).ConfigureAwait(false);

			_logger.LogInformation(
				"Compliance report generated for {Framework} with {EventCount} events analyzed",
				framework, report.TotalEventsAnalyzed);

			return report;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to generate compliance report for {Framework}", framework);
			throw;
		}
	}

	/// <summary>
	/// Searches audit events based on specified criteria for investigation and analysis.
	/// </summary>
	internal async Task<IEnumerable<SecurityAuditEvent>> SearchAuditEventsAsync(
		AuditSearchCriteria searchCriteria,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(searchCriteria);

		try
		{
			_logger.LogDebug("Searching audit events with criteria: {Criteria}", searchCriteria);

			var query = BuildSearchQuery(searchCriteria);

			var searchResponse = await _elasticsearchClient.SearchAsync<SecurityAuditEvent>(
				s => s
					.Indices("security-audit-*")
					.Query(query)
					.Size(searchCriteria.MaxResults)
					.From(searchCriteria.Skip)
					.Sort(so => so.Field(
						new Field("timestamp"),
						new FieldSort { Order = searchCriteria.SortDescending ? SortOrder.Desc : SortOrder.Asc })),
				cancellationToken).ConfigureAwait(false);

			if (!searchResponse.IsValidResponse)
			{
				throw new InvalidOperationException($"Failed to search audit events: {searchResponse.DebugInformation}");
			}

			var events = searchResponse.Documents.ToList();

			_logger.LogDebug("Found {EventCount} audit events matching search criteria", events.Count);
			return events;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to search audit events");
			throw;
		}
	}

	/// <summary>
	/// Generates a security audit report from a request object.
	/// </summary>
	internal async Task<SecurityAuditReport> GenerateAuditReportAsync(
		AuditReportRequest reportRequest,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(reportRequest);

		try
		{
			var auditReport = await GenerateAuditReportAsync(
				reportRequest.StartTime,
				reportRequest.EndTime,
				reportRequest.ReportType,
				cancellationToken).ConfigureAwait(false);

			return new SecurityAuditReport(
				auditReport.ReportId,
				auditReport.StartTime,
				auditReport.EndTime,
				reportRequest.ReportType)
			{
				TotalEventsAnalyzed = auditReport.TotalEvents,
				EventTypeBreakdown = auditReport.EventBreakdown.ToDictionary(
					static kvp => kvp.Key,
					static kvp => (long)kvp.Value, StringComparer.Ordinal),
				SeverityBreakdown = auditReport.SeverityBreakdown.ToDictionary(
					static kvp => Enum.Parse<SecurityEventSeverity>(kvp.Key),
					static kvp => (long)kvp.Value),
				UniqueUserCount = auditReport.UniqueUsers,
				UniqueSourceIpCount = auditReport.UniqueSourceIps,
				ComplianceStatus = auditReport.ComplianceStatus,
				Recommendations = auditReport.Recommendations,
			};
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to generate security audit report");
			throw;
		}
	}

	/// <summary>
	/// Generates a compliance report from a request object.
	/// </summary>
	internal async Task<ComplianceReport> GenerateComplianceReportAsync(
		ComplianceReportRequest complianceRequest,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(complianceRequest);

		try
		{
			return await GenerateComplianceReportAsync(
				complianceRequest.Framework,
				complianceRequest.StartTime,
				complianceRequest.EndTime,
				cancellationToken).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to generate compliance report");
			throw;
		}
	}

	/// <summary>
	/// Searches audit events from a request object.
	/// </summary>
	internal async Task<AuditSearchResult> SearchAuditEventsAsync(
		AuditSearchRequest searchRequest,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(searchRequest);

		try
		{
			var searchId = Guid.NewGuid();
			var startTime = DateTimeOffset.UtcNow;

			var criteria = new AuditSearchCriteria
			{
				StartTime = searchRequest.StartTime,
				EndTime = searchRequest.EndTime,
				UserId = searchRequest.UserId,
				EventType = searchRequest.EventType,
				Severity = searchRequest.Severity?.ToString(),
				SourceIpAddress = searchRequest.SourceIpAddress,
				MaxResults = searchRequest.MaxResults,
				Skip = searchRequest.Skip,
				SortDescending = searchRequest.SortDescending,
			};

			var events = await SearchAuditEventsAsync(criteria, cancellationToken).ConfigureAwait(false);
			var eventsList = events.ToList();

			var executionTime = (long)(DateTimeOffset.UtcNow - startTime).TotalMilliseconds;

			return new AuditSearchResult(searchId, eventsList.Count, eventsList)
			{
				ExecutionTimeMs = executionTime,
				IsTruncated = eventsList.Count == searchRequest.MaxResults,
			};
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to search audit events");
			throw;
		}
	}

	/// <summary>
	/// Evaluates compliance status for a set of audit events.
	/// </summary>
	internal async Task<Dictionary<string, object>> EvaluateComplianceStatusAsync(
		IList<SecurityAuditEvent> events,
		CancellationToken cancellationToken)
	{
		var complianceStatus = new Dictionary<string, object>(StringComparer.Ordinal);

		foreach (var framework in _configuration.ComplianceFrameworks)
		{
			if (_complianceReporters.TryGetValue(framework, out var reporter))
			{
				var violations = new List<string>();

				foreach (var auditEvent in events)
				{
					var violation = await reporter.CheckComplianceViolationAsync(auditEvent, cancellationToken).ConfigureAwait(false);
					if (violation != null)
					{
						violations.Add(violation.Description);
					}
				}

				complianceStatus[framework.ToString()] = new
				{
					Violations = violations.Count,
					Status = violations.Count == 0 ? "Compliant" : "Non-Compliant",
					Details = violations.Take(5).ToList(), // Limit to top 5 violations for summary
				};
			}
		}

		return complianceStatus;
	}

	/// <summary>
	/// Builds Elasticsearch query from search criteria.
	/// </summary>
	private static Query BuildSearchQuery(AuditSearchCriteria criteria)
	{
		var queries = new List<Query>();

		// Time range filter
		if (criteria.StartTime.HasValue || criteria.EndTime.HasValue)
		{
			var rangeQuery = new DateRangeQuery(new Field("timestamp"));
			if (criteria.StartTime.HasValue)
			{
				rangeQuery.Gte = (DateMath)criteria.StartTime.Value.DateTime;
			}

			if (criteria.EndTime.HasValue)
			{
				rangeQuery.Lte = (DateMath)criteria.EndTime.Value.DateTime;
			}

			queries.Add(rangeQuery);
		}

		// User ID filter
		if (!string.IsNullOrEmpty(criteria.UserId))
		{
			queries.Add(new TermQuery(new Field("userId")) { Value = criteria.UserId });
		}

		// Event type filter
		if (!string.IsNullOrEmpty(criteria.EventType))
		{
			queries.Add(new TermQuery(new Field("eventType")) { Value = criteria.EventType });
		}

		// Severity filter
		if (!string.IsNullOrEmpty(criteria.Severity))
		{
			queries.Add(new TermQuery(new Field("severity")) { Value = criteria.Severity });
		}

		// Source IP filter
		if (!string.IsNullOrEmpty(criteria.SourceIpAddress))
		{
			queries.Add(new TermQuery(new Field("sourceIpAddress")) { Value = criteria.SourceIpAddress });
		}

		return queries.Count switch
		{
			0 => new MatchAllQuery(),
			1 => queries[0],
			_ => new BoolQuery { Must = queries },
		};
	}
}
