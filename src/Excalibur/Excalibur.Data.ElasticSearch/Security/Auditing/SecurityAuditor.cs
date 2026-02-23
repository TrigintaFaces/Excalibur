// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;
using System.Diagnostics.Metrics;

using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Core.Bulk;
using Elastic.Clients.Elasticsearch.IndexManagement;
using Elastic.Clients.Elasticsearch.Mapping;

using Excalibur.Data.ElasticSearch.Diagnostics;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Production implementation of comprehensive security auditing for Elasticsearch operations with GDPR, SOC2, and HIPAA compliance,
/// tamper-proof logging, real-time threat detection, and automated incident response capabilities.
/// </summary>
/// <remarks>
/// <para>
/// Facade that delegates to focused service classes following SRP:
/// <list type="bullet">
/// <item><description><see cref="SecurityAuditWriter"/> — event recording (authentication, data access, config changes, incidents)</description></item>
/// <item><description><see cref="SecurityAuditQueryService"/> — reporting and search (audit reports, compliance reports, event search)</description></item>
/// <item><description><see cref="SecurityAuditMaintenanceService"/> — integrity validation and archival</description></item>
/// </list>
/// </para>
/// <para>
/// Owns shared infrastructure: event processing queues, timers, index initialization, and disposal.
/// </para>
/// </remarks>
public sealed class SecurityAuditor : IElasticsearchSecurityAuditor, IElasticsearchSecurityAuditorReporting, IElasticsearchSecurityAuditorMaintenance, IDisposable, IAsyncDisposable
{
	private static readonly Counter<long> EventsFailedCounter = AuditTelemetryConstants.Meter.CreateCounter<long>(
		AuditTelemetryConstants.MetricNames.EventsFailed,
		"events",
		"Total audit event recording failures");

	private static readonly Histogram<int> BulkStoreSizeHistogram = AuditTelemetryConstants.Meter.CreateHistogram<int>(
		AuditTelemetryConstants.MetricNames.BulkStoreSize,
		"events",
		"Number of events in a bulk store batch");

	private readonly ElasticsearchClient _elasticsearchClient;
	private readonly ILogger<SecurityAuditor> _logger;
	private readonly SemaphoreSlim _auditSemaphore;
	private readonly Timer? _complianceReportTimer;
	private readonly Dictionary<ComplianceFramework, ComplianceReporter> _complianceReporters;
	private readonly ConcurrentQueue<SecurityAuditEvent> _normalEventQueue = new();
	private readonly ConcurrentQueue<SecurityAuditEvent> _priorityEventQueue = new();
	private readonly Timer _auditEventProcessor;
	private readonly ConcurrentBag<Task> _trackedTasks = [];
	private volatile bool _disposed;

	// Focused service delegates
	private readonly SecurityAuditWriter _writer;
	private readonly SecurityAuditQueryService _queryService;
	private readonly SecurityAuditMaintenanceService _maintenanceService;

	/// <inheritdoc />
	public AuditOptions Configuration { get; }

	/// <inheritdoc />
	public bool IntegrityProtectionEnabled => Configuration.EnsureLogIntegrity;

	/// <inheritdoc />
	public IReadOnlyCollection<ComplianceFramework> SupportedComplianceFrameworks { get; }

	/// <summary>
	/// Initializes a new instance of the <see cref="SecurityAuditor" /> class.
	/// </summary>
	/// <param name="elasticsearchClient"> The Elasticsearch client for audit log storage. </param>
	/// <param name="auditOptions"> The audit configuration options. </param>
	/// <param name="monitoringOptions"> The security monitoring configuration options. </param>
	/// <param name="logger"> The logger for operational events. </param>
	/// <exception cref="ArgumentNullException"> Thrown when required dependencies are null. </exception>
	public SecurityAuditor(
		ElasticsearchClient elasticsearchClient,
		IOptions<AuditOptions> auditOptions,
		IOptions<SecurityMonitoringOptions> monitoringOptions,
		ILogger<SecurityAuditor> logger)
	{
		_elasticsearchClient = elasticsearchClient ?? throw new ArgumentNullException(nameof(elasticsearchClient));
		Configuration = auditOptions?.Value ?? throw new ArgumentNullException(nameof(auditOptions));
		_ = monitoringOptions?.Value ?? throw new ArgumentNullException(nameof(monitoringOptions));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));

		_auditSemaphore = new SemaphoreSlim(Environment.ProcessorCount, Environment.ProcessorCount);
		_complianceReporters = InitializeComplianceReporters();
		SupportedComplianceFrameworks = [.. Configuration.ComplianceFrameworks];

		// Initialize focused service delegates
		_writer = new SecurityAuditWriter(
			Configuration, _auditSemaphore, _normalEventQueue, _priorityEventQueue, _complianceReporters, _logger);
		_queryService = new SecurityAuditQueryService(
			_elasticsearchClient, Configuration, _complianceReporters, _logger);
		_maintenanceService = new SecurityAuditMaintenanceService(
			_elasticsearchClient, Configuration, _logger);

		// Wire up events from delegates
		_writer.SecurityEventRecorded += (sender, args) => SecurityEventRecorded?.Invoke(this, args);
		_writer.ComplianceViolationDetected += (sender, args) => ComplianceViolationDetected?.Invoke(this, args);
		_maintenanceService.AuditArchiveCompleted += (sender, args) => AuditArchiveCompleted?.Invoke(this, args);

		// Initialize compliance reporting timer
		if (Configuration.ComplianceFrameworks.Count != 0)
		{
			_complianceReportTimer = new Timer(GenerateComplianceReports, state: null,
				TimeSpan.FromHours(24), TimeSpan.FromHours(24));
		}

		// Initialize audit event processor
		_auditEventProcessor = new Timer(ProcessAuditEventQueue, state: null,
			TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));

		_trackedTasks.Add(InitializeAuditIndicesAsync());

		_logger.LogInformation(
			"SecurityAuditor initialized with {FrameworkCount} compliance frameworks enabled",
			Configuration.ComplianceFrameworks.Count);
	}

	/// <inheritdoc />
	public event EventHandler<SecurityEventRecordedEventArgs>? SecurityEventRecorded;

	/// <inheritdoc />
	// R0.8: Event is never used - keeping for future extensibility
#pragma warning disable CS0067

	public event EventHandler<AuditIntegrityViolationEventArgs>? IntegrityViolationDetected;

#pragma warning restore CS0067

	/// <inheritdoc />
	public event EventHandler<AuditArchiveCompletedEventArgs>? AuditArchiveCompleted;

	/// <summary>
	/// Raised when a compliance violation is detected.
	/// </summary>
	public event EventHandler<ComplianceViolationEventArgs>? ComplianceViolationDetected;

	/// <inheritdoc />
	public Task AuditSecurityActivityAsync(
		SecurityActivityEvent activityEvent,
		CancellationToken cancellationToken) =>
		_writer.AuditSecurityActivityAsync(activityEvent, cancellationToken);

	/// <inheritdoc />
	public Task<bool> RecordAuthenticationEventAsync(
		AuthenticationEvent authenticationEvent,
		CancellationToken cancellationToken) =>
		_writer.RecordAuthenticationEventAsync(authenticationEvent, cancellationToken);

	/// <inheritdoc />
	public Task<bool> RecordDataAccessEventAsync(
		DataAccessEvent dataAccessEvent,
		CancellationToken cancellationToken) =>
		_writer.RecordDataAccessEventAsync(dataAccessEvent, cancellationToken);

	/// <inheritdoc />
	public Task<bool> RecordConfigurationChangeAsync(
		ConfigurationChangeEvent configurationEvent,
		CancellationToken cancellationToken) =>
		_writer.RecordConfigurationChangeAsync(configurationEvent, cancellationToken);

	/// <summary>
	/// Records a general security event for compliance and monitoring purposes.
	/// </summary>
	public Task<bool> RecordSecurityEventAsync(
		SecurityEvent securityEvent,
		CancellationToken cancellationToken) =>
		_writer.RecordSecurityEventAsync(securityEvent, cancellationToken);

	/// <inheritdoc />
	public Task<bool> RecordSecurityIncidentAsync(
		SecurityIncident securityIncident,
		CancellationToken cancellationToken) =>
		_writer.RecordSecurityIncidentAsync(securityIncident, cancellationToken);

	/// <summary>
	/// Generates a comprehensive audit report for the specified time period.
	/// </summary>
	public Task<AuditReport> GenerateAuditReportAsync(
		DateTimeOffset startTime,
		DateTimeOffset endTime,
		AuditReportType reportType,
		CancellationToken cancellationToken) =>
		_queryService.GenerateAuditReportAsync(startTime, endTime, reportType, cancellationToken);

	/// <summary>
	/// Generates a compliance report for the specified framework and time period.
	/// </summary>
	public Task<ComplianceReport> GenerateComplianceReportAsync(
		ComplianceFramework framework,
		DateTimeOffset startTime,
		DateTimeOffset endTime,
		CancellationToken cancellationToken) =>
		_queryService.GenerateComplianceReportAsync(framework, startTime, endTime, cancellationToken);

	/// <summary>
	/// Searches audit events based on internal search criteria.
	/// </summary>
	public Task<IEnumerable<SecurityAuditEvent>> SearchAuditEventsAsync(
		AuditSearchCriteria searchCriteria,
		CancellationToken cancellationToken) =>
		_queryService.SearchAuditEventsAsync(searchCriteria, cancellationToken);

	/// <summary>
	/// Validates the integrity of audit logs for the specified time period.
	/// </summary>
	public Task<AuditLogIntegrityResult> ValidateAuditIntegrityAsync(
		DateTimeOffset startTime,
		DateTimeOffset endTime,
		CancellationToken cancellationToken) =>
		_maintenanceService.ValidateAuditIntegrityAsync(startTime, endTime, cancellationToken);

	/// <summary>
	/// Archives audit events before the cutoff date to the specified location.
	/// </summary>
	public Task<AuditArchiveResult> ArchiveAuditEventsAsync(
		DateTimeOffset cutoffDate,
		string archiveLocation,
		CancellationToken cancellationToken) =>
		_maintenanceService.ArchiveAuditEventsAsync(cutoffDate, archiveLocation, cancellationToken);

	/// <inheritdoc />
	public Task<SecurityAuditReport> GenerateAuditReportAsync(
		AuditReportRequest reportRequest,
		CancellationToken cancellationToken) =>
		_queryService.GenerateAuditReportAsync(reportRequest, cancellationToken);

	/// <inheritdoc />
	public Task<ComplianceReport> GenerateComplianceReportAsync(
		ComplianceReportRequest complianceRequest,
		CancellationToken cancellationToken) =>
		_queryService.GenerateComplianceReportAsync(complianceRequest, cancellationToken);

	/// <inheritdoc />
	public Task<AuditSearchResult> SearchAuditEventsAsync(
		AuditSearchRequest searchRequest,
		CancellationToken cancellationToken) =>
		_queryService.SearchAuditEventsAsync(searchRequest, cancellationToken);

	/// <inheritdoc />
	public Task<AuditIntegrityResult> ValidateAuditIntegrityAsync(
		AuditIntegrityRequest validationRequest,
		CancellationToken cancellationToken) =>
		_maintenanceService.ValidateAuditIntegrityAsync(validationRequest, cancellationToken);

	/// <inheritdoc />
	public Task<AuditArchiveResult> ArchiveAuditEventsAsync(
		AuditArchiveRequest archiveRequest,
		CancellationToken cancellationToken) =>
		_maintenanceService.ArchiveAuditEventsAsync(archiveRequest, cancellationToken);

	/// <summary>
	/// Releases all resources used by the SecurityAuditor.
	/// </summary>
	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;

		_auditSemaphore?.Dispose();
		_complianceReportTimer?.Dispose();
		_auditEventProcessor?.Dispose();

		// Best-effort drain of tracked tasks with timeout to prevent abandonment
		try
		{
			var tasks = _trackedTasks.ToArray();
			if (tasks.Length > 0)
			{
				Task.WaitAll(tasks, TimeSpan.FromSeconds(5));
			}
		}
		catch (AggregateException)
		{
			// Expected -- tasks may cancel or fault during shutdown
		}

		foreach (var reporter in _complianceReporters.Values)
		{
			if (reporter is IDisposable disposableReporter)
			{
				disposableReporter.Dispose();
			}
		}
	}

	/// <summary>
	/// Asynchronously releases resources, ensuring tracked timer callbacks have completed.
	/// </summary>
	public async ValueTask DisposeAsync()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;

		// Wait for in-flight timer callbacks
		_auditSemaphore?.Dispose();
		if (_complianceReportTimer != null)
		{
			await _complianceReportTimer.DisposeAsync().ConfigureAwait(false);
		}

		await _auditEventProcessor.DisposeAsync().ConfigureAwait(false);

		// Wait for tracked tasks to complete
		try
		{
			await Task.WhenAll(_trackedTasks).ConfigureAwait(false);
		}
		catch (OperationCanceledException)
		{
			// Expected during shutdown
		}

		foreach (var reporter in _complianceReporters.Values)
		{
			if (reporter is IAsyncDisposable asyncDisposable)
			{
				await asyncDisposable.DisposeAsync().ConfigureAwait(false);
			}
			else if (reporter is IDisposable disposableReporter)
			{
				disposableReporter.Dispose();
			}
		}
	}

	/// <summary>
	/// Initializes the audit log indices in Elasticsearch.
	/// </summary>
	private async Task InitializeAuditIndicesAsync()
	{
		try
		{
			const string indexName = "security-audit-template";
			var templateExists = await _elasticsearchClient.Indices.ExistsIndexTemplateAsync(indexName).ConfigureAwait(false);

			if (!templateExists.Exists)
			{
				await CreateAuditIndexTemplateAsync(indexName).ConfigureAwait(false);
				_logger.LogInformation("Security audit index template created");
			}
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to initialize audit indices");
		}
	}

	/// <summary>
	/// Creates the index template for security audit logs.
	/// </summary>
	private async Task CreateAuditIndexTemplateAsync(string templateName)
	{
		var template = new PutIndexTemplateRequest(templateName)
		{
			IndexPatterns = new[] { "security-audit-*" },
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

		_ = await _elasticsearchClient.Indices.PutIndexTemplateAsync(template).ConfigureAwait(false);
	}

	/// <summary>
	/// Processes the audit event queue and stores events in Elasticsearch.
	/// </summary>
	private void ProcessAuditEventQueue(object? state)
	{
		if (_disposed || (_priorityEventQueue.IsEmpty && _normalEventQueue.IsEmpty))
		{
			return;
		}

		var task = QueueBackgroundWork(async () =>
		{
			await ProcessAuditEventQueueCoreAsync().ConfigureAwait(false);
		});
		_trackedTasks.Add(task);
	}

	private async Task ProcessAuditEventQueueCoreAsync()
	{
		if (_priorityEventQueue.IsEmpty && _normalEventQueue.IsEmpty)
		{
			return;
		}

		var eventsToProcess = new List<SecurityAuditEvent>();

		// Drain priority queue first (incidents), then normal events
		while (eventsToProcess.Count < 100 && _priorityEventQueue.TryDequeue(out var priorityEvent))
		{
			eventsToProcess.Add(priorityEvent);
		}

		while (eventsToProcess.Count < 100 && _normalEventQueue.TryDequeue(out var normalEvent))
		{
			eventsToProcess.Add(normalEvent);
		}

		if (eventsToProcess.Count == 0)
		{
			return;
		}

		try
		{
			// Add integrity hashes if enabled
			if (Configuration.EnsureLogIntegrity)
			{
				foreach (var auditEvent in eventsToProcess)
				{
					auditEvent.IntegrityHash = SecurityAuditMaintenanceService.ComputeIntegrityHash(auditEvent);
				}
			}

			BulkStoreSizeHistogram.Record(eventsToProcess.Count);

			// Bulk index the events
			var indexName = $"security-audit-{DateTimeOffset.UtcNow:yyyy-MM}";
			var bulkRequest = new BulkRequest(indexName);

			foreach (var auditEvent in eventsToProcess)
			{
				bulkRequest.Operations?.Add(new BulkIndexOperation<SecurityAuditEvent>(auditEvent) { Id = auditEvent.EventId });
			}

			var response = await _elasticsearchClient.BulkAsync(bulkRequest).ConfigureAwait(false);

			if (!response.IsValidResponse)
			{
				_logger.LogError("Failed to store audit events in Elasticsearch: {Error}", response.DebugInformation);
			}
			else
			{
				_logger.LogDebug("Stored {EventCount} audit events in Elasticsearch", eventsToProcess.Count);
			}
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error processing audit event queue");

			// Re-queue events for retry (into normal queue)
			foreach (var evt in eventsToProcess)
			{
				_normalEventQueue.Enqueue(evt);
			}
		}
	}

	/// <summary>
	/// Initializes compliance reporters for enabled frameworks.
	/// </summary>
	private Dictionary<ComplianceFramework, ComplianceReporter> InitializeComplianceReporters()
	{
		var reporters = new Dictionary<ComplianceFramework, ComplianceReporter>();

		foreach (var framework in Configuration.ComplianceFrameworks)
		{
			reporters[framework] = new GenericComplianceReporter(_elasticsearchClient, _logger);
		}

		return reporters;
	}

	/// <summary>
	/// Generates compliance reports for all enabled frameworks.
	/// </summary>
	private void GenerateComplianceReports(object? state)
	{
		if (_disposed)
		{
			return;
		}

		var task = QueueBackgroundWork(async () =>
		{
			var endTime = DateTimeOffset.UtcNow;
			var startTime = endTime.AddDays(-1); // Daily reports

			foreach (var framework in Configuration.ComplianceFrameworks)
			{
				try
				{
					var report = await _queryService.GenerateComplianceReportAsync(framework, startTime, endTime, CancellationToken.None).ConfigureAwait(false);
					_logger.LogInformation(
						"Daily compliance report generated for {Framework}: {Violations} violations found",
						framework, report.Violations.Count);
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, "Failed to generate compliance report for {Framework}", framework);
				}
			}
		});
		_trackedTasks.Add(task);
	}

	private static Task QueueBackgroundWork(Func<Task> callback) =>
		Task.Factory.StartNew(
			callback,
			CancellationToken.None,
			TaskCreationOptions.DenyChildAttach,
			TaskScheduler.Default).Unwrap();
}
