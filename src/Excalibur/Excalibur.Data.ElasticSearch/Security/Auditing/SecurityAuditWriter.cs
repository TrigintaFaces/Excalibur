// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;

using Excalibur.Data.ElasticSearch.Diagnostics;

using Microsoft.Extensions.Logging;

namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Handles recording of security audit events (authentication, data access,
/// configuration changes, security incidents) into processing queues.
/// </summary>
/// <remarks>
/// <para>
/// Extracted from <see cref="SecurityAuditor"/> following SRP. Each record method
/// validates the event, determines severity, enqueues for bulk storage, emits
/// telemetry counters, and checks compliance violations.
/// </para>
/// <para>
/// Events are enqueued into priority (incidents) or normal queues. The queue
/// processor in <see cref="SecurityAuditor"/> drains and bulk-indexes them.
/// </para>
/// </remarks>
internal sealed class SecurityAuditWriter
{
	private static readonly Counter<long> EventsRecordedCounter = AuditTelemetryConstants.Meter.CreateCounter<long>(
		AuditTelemetryConstants.MetricNames.EventsRecorded,
		"events",
		"Total audit events recorded");

	private readonly AuditOptions _configuration;
	private readonly SemaphoreSlim _auditSemaphore;
	private readonly ConcurrentQueue<SecurityAuditEvent> _normalEventQueue;
	private readonly ConcurrentQueue<SecurityAuditEvent> _priorityEventQueue;
	private readonly Dictionary<ComplianceFramework, ComplianceReporter> _complianceReporters;
	private readonly ILogger _logger;

	/// <summary>
	/// Raised when a security event is recorded for real-time monitoring.
	/// </summary>
	internal event EventHandler<SecurityEventRecordedEventArgs>? SecurityEventRecorded;

	/// <summary>
	/// Raised when a compliance violation is detected.
	/// </summary>
	internal event EventHandler<ComplianceViolationEventArgs>? ComplianceViolationDetected;

	/// <summary>
	/// Initializes a new instance of the <see cref="SecurityAuditWriter"/> class.
	/// </summary>
	internal SecurityAuditWriter(
		AuditOptions configuration,
		SemaphoreSlim auditSemaphore,
		ConcurrentQueue<SecurityAuditEvent> normalEventQueue,
		ConcurrentQueue<SecurityAuditEvent> priorityEventQueue,
		Dictionary<ComplianceFramework, ComplianceReporter> complianceReporters,
		ILogger logger)
	{
		_configuration = configuration;
		_auditSemaphore = auditSemaphore;
		_normalEventQueue = normalEventQueue;
		_priorityEventQueue = priorityEventQueue;
		_complianceReporters = complianceReporters;
		_logger = logger;
	}

	/// <summary>
	/// Audits a security activity event for compliance and monitoring purposes.
	/// </summary>
	internal async Task AuditSecurityActivityAsync(
		SecurityActivityEvent activityEvent,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(activityEvent);

		if (!_configuration.Enabled)
		{
			return;
		}

		await _auditSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			var securityEvent = new SecurityAuditEvent
			{
				EventId = Guid.NewGuid().ToString(),
				Timestamp = activityEvent.Timestamp,
				EventType = SecurityEventType.DataAccess,
				Severity = SecurityEventSeverity.Low,
				Source = "ElasticsearchSecurityAuditor",
				UserId = activityEvent.UserId,
				SourceIpAddress = "Unknown",
				UserAgent = "Unknown",
				Details = new Dictionary<string, object>
					(StringComparer.Ordinal)
				{ ["ActivityType"] = activityEvent.ActivityType, ["Timestamp"] = activityEvent.Timestamp, },
			};

			_normalEventQueue.Enqueue(securityEvent);

			_logger.LogDebug(
				"Security activity audited: {ActivityType} by {UserId}",
				activityEvent.ActivityType, activityEvent.UserId);
		}
		finally
		{
			_ = _auditSemaphore.Release();
		}
	}

	/// <summary>
	/// Records an authentication event for security monitoring and compliance purposes.
	/// </summary>
	internal async Task<bool> RecordAuthenticationEventAsync(
		AuthenticationEvent authenticationEvent,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(authenticationEvent);

		if (!_configuration.Enabled || !_configuration.AuditAuthentication)
		{
			return false;
		}

		await _auditSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			var severity = DetermineAuthenticationSeverity(authenticationEvent);
			var securityEvent = new SecurityAuditEvent
			{
				EventId = authenticationEvent.EventId,
				Timestamp = authenticationEvent.Timestamp,
				EventType = SecurityEventType.Authentication,
				Severity = severity,
				Source = "ElasticsearchSecurityAuditor",
				UserId = authenticationEvent.UserId,
				SourceIpAddress = authenticationEvent.IpAddress,
				UserAgent = authenticationEvent.UserAgent,
				Details = new Dictionary<string, object>
					(StringComparer.Ordinal)
				{
					["Result"] = authenticationEvent.Result.ToString(),
					["AuthenticationMethod"] = authenticationEvent.AuthenticationMethod,
					["Success"] = authenticationEvent.Result == AuthenticationResult.Success,
					["FailureReason"] = authenticationEvent.FailureReason ?? string.Empty,
					["Context"] = authenticationEvent.Context ?? new Dictionary<string, object>(StringComparer.Ordinal),
				},
			};

			_normalEventQueue.Enqueue(securityEvent);

			EventsRecordedCounter.Add(1, new TagList
			{
				{ AuditTelemetryConstants.Tags.EventType, "authentication" },
				{ AuditTelemetryConstants.Tags.Severity, severity.ToString().ToLowerInvariant() },
			});

			SecurityEventRecorded?.Invoke(this, new SecurityEventRecordedEventArgs(
				"Authentication", authenticationEvent.EventId, authenticationEvent.Timestamp)
			{ Severity = severity.ToString() });

			await CheckComplianceViolationAsync(securityEvent, cancellationToken).ConfigureAwait(false);

			_logger.LogDebug(
				"Authentication event audited: {UserId} from {SourceIp} result: {Result}",
				authenticationEvent.UserId, authenticationEvent.IpAddress, authenticationEvent.Result);

			return true;
		}
		finally
		{
			_ = _auditSemaphore.Release();
		}
	}

	/// <summary>
	/// Records a data access event for monitoring data usage patterns and detecting anomalies.
	/// </summary>
	internal async Task<bool> RecordDataAccessEventAsync(
		DataAccessEvent dataAccessEvent,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(dataAccessEvent);

		if (!_configuration.Enabled || !_configuration.AuditDataAccess)
		{
			return false;
		}

		await _auditSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			var severity = DetermineDataAccessSeverity(dataAccessEvent);
			var securityEvent = new SecurityAuditEvent
			{
				EventId = dataAccessEvent.EventId,
				Timestamp = dataAccessEvent.Timestamp,
				EventType = SecurityEventType.DataAccess,
				Severity = severity,
				Source = "ElasticsearchSecurityAuditor",
				UserId = dataAccessEvent.UserId,
				SourceIpAddress = dataAccessEvent.IpAddress,
				Details = new Dictionary<string, object>
					(StringComparer.Ordinal)
				{
					["Operation"] = dataAccessEvent.Operation.ToString(),
					["ResourceType"] = dataAccessEvent.ResourceType,
					["ResourceId"] = dataAccessEvent.ResourceId,
					["DataClassification"] = dataAccessEvent.DataClassification ?? string.Empty,
					["DataSize"] = dataAccessEvent.DataSize ?? 0,
					["IsSuccessful"] = dataAccessEvent.IsSuccessful,
					["Context"] = dataAccessEvent.Context ?? new Dictionary<string, object>(StringComparer.Ordinal),
				},
			};

			_normalEventQueue.Enqueue(securityEvent);

			EventsRecordedCounter.Add(1, new TagList
			{
				{ AuditTelemetryConstants.Tags.EventType, "data_access" },
				{ AuditTelemetryConstants.Tags.Severity, severity.ToString().ToLowerInvariant() },
			});

			SecurityEventRecorded?.Invoke(this, new SecurityEventRecordedEventArgs(
				"DataAccess", dataAccessEvent.EventId, dataAccessEvent.Timestamp)
			{ Severity = severity.ToString() });

			await CheckComplianceViolationAsync(securityEvent, cancellationToken).ConfigureAwait(false);

			_logger.LogDebug(
				"Data access event audited: {UserId} performed {Operation} on {ResourceType}",
				dataAccessEvent.UserId, dataAccessEvent.Operation, dataAccessEvent.ResourceType);

			return true;
		}
		finally
		{
			_ = _auditSemaphore.Release();
		}
	}

	/// <summary>
	/// Records a security configuration change for compliance and change tracking purposes.
	/// </summary>
	internal async Task<bool> RecordConfigurationChangeAsync(
		ConfigurationChangeEvent configurationEvent,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(configurationEvent);

		if (!_configuration.Enabled || !_configuration.AuditConfigurationChanges)
		{
			return false;
		}

		await _auditSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			var severity = DetermineConfigurationChangeSeverity(configurationEvent);
			var securityEvent = new SecurityAuditEvent
			{
				EventId = configurationEvent.EventId.ToString(),
				Timestamp = configurationEvent.Timestamp,
				EventType = SecurityEventType.ConfigurationChange,
				Severity = severity,
				Source = "ElasticsearchSecurityAuditor",
				UserId = configurationEvent.ChangedBy,
				SourceIpAddress = null,
				Details = new Dictionary<string, object>
					(StringComparer.Ordinal)
				{
					["ChangeType"] = configurationEvent.ChangeType.ToString(),
					["ConfigurationSection"] = configurationEvent.ConfigurationSection,
					["PreviousValue"] = configurationEvent.PreviousValue ?? string.Empty,
					["NewValue"] = configurationEvent.NewValue ?? string.Empty,
					["ChangeReason"] = configurationEvent.ChangeReason ?? string.Empty,
					["AdditionalData"] = configurationEvent.AdditionalData,
				},
			};

			_normalEventQueue.Enqueue(securityEvent);

			SecurityEventRecorded?.Invoke(this, new SecurityEventRecordedEventArgs(
				"ConfigurationChange", configurationEvent.EventId.ToString(), configurationEvent.Timestamp)
			{
				Severity = severity.ToString(),
			});

			await CheckComplianceViolationAsync(securityEvent, cancellationToken).ConfigureAwait(false);

			_logger.LogInformation(
				"Configuration change event audited: {UserId} changed {ConfigurationSection}",
				configurationEvent.ChangedBy, configurationEvent.ConfigurationSection);

			return true;
		}
		finally
		{
			_ = _auditSemaphore.Release();
		}
	}

	/// <summary>
	/// Records a general security event for compliance and monitoring purposes.
	/// </summary>
	internal async Task<bool> RecordSecurityEventAsync(
		SecurityEvent securityEvent,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(securityEvent);

		if (!_configuration.Enabled)
		{
			return false;
		}

		await _auditSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			var auditEvent = new SecurityAuditEvent
			{
				EventId = securityEvent.EventId.ToString(),
				Timestamp = securityEvent.Timestamp,
				EventType = MapToSecurityEventType(securityEvent.EventType),
				Severity = MapToSecurityEventSeverity(securityEvent.Severity),
				Source = securityEvent.Source ?? "ElasticsearchSecurityAuditor",
				UserId = securityEvent.UserId,
				SourceIpAddress = securityEvent.SourceIpAddress,
				UserAgent = securityEvent.UserAgent,
				Details = securityEvent.AdditionalData ?? [],
			};

			_normalEventQueue.Enqueue(auditEvent);

			SecurityEventRecorded?.Invoke(this, new SecurityEventRecordedEventArgs(
				securityEvent.EventType, securityEvent.EventId.ToString(), securityEvent.Timestamp)
			{
				Severity = MapToSecurityEventSeverity(securityEvent.Severity).ToString(),
			});

			await CheckComplianceViolationAsync(auditEvent, cancellationToken).ConfigureAwait(false);

			_logger.LogDebug(
				"Security event recorded: {EventType} for user {UserId}",
				securityEvent.EventType, securityEvent.UserId);

			return true;
		}
		finally
		{
			_ = _auditSemaphore.Release();
		}
	}

	/// <summary>
	/// Records a security incident or violation for immediate attention and investigation.
	/// </summary>
	internal async Task<bool> RecordSecurityIncidentAsync(
		SecurityIncident securityIncident,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(securityIncident);

		if (!_configuration.Enabled)
		{
			return false;
		}

		await _auditSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			var auditEvent = new SecurityAuditEvent
			{
				EventId = securityIncident.IncidentId.ToString(),
				Timestamp = securityIncident.Timestamp,
				EventType = SecurityEventType.SecurityIncident,
				Severity = MapToSecurityEventSeverity(securityIncident.Severity),
				Source = "SecurityIncidentHandler",
				UserId = securityIncident.AffectedUserId,
				SourceIpAddress = securityIncident.SourceIpAddress,
				Details = new Dictionary<string, object>
					(StringComparer.Ordinal)
				{
					["IncidentType"] = securityIncident.IncidentType,
					["Description"] = securityIncident.Description,
					["AffectedSystems"] = securityIncident.AffectedSystems,
					["ResponseActions"] = securityIncident.ResponseActions,
					["Resolution"] = securityIncident.Resolution ?? string.Empty,
					["AdditionalData"] = securityIncident.AdditionalData ?? [],
				},
			};

			// Add to priority queue for incidents -- drained first by timer
			_priorityEventQueue.Enqueue(auditEvent);

			EventsRecordedCounter.Add(1, new TagList
			{
				{ AuditTelemetryConstants.Tags.EventType, "security_incident" },
				{ AuditTelemetryConstants.Tags.Severity, MapToSecurityEventSeverity(securityIncident.Severity).ToString().ToLowerInvariant() },
			});

			SecurityEventRecorded?.Invoke(this, new SecurityEventRecordedEventArgs(
				"SecurityIncident", securityIncident.IncidentId.ToString(), securityIncident.Timestamp)
			{
				Severity = MapToSecurityEventSeverity(securityIncident.Severity).ToString(),
			});

			await CheckComplianceViolationAsync(auditEvent, cancellationToken).ConfigureAwait(false);

			_logger.LogWarning(
				"Security incident recorded: {IncidentType} affecting user {UserId} - {Description}",
				securityIncident.IncidentType, securityIncident.AffectedUserId, securityIncident.Description);

			return true;
		}
		finally
		{
			_ = _auditSemaphore.Release();
		}
	}

	/// <summary>
	/// Determines the severity level for an authentication event.
	/// </summary>
	internal static SecurityEventSeverity DetermineAuthenticationSeverity(AuthenticationEvent authenticationEvent) =>
		authenticationEvent.Result switch
		{
			AuthenticationResult.Success => SecurityEventSeverity.Low,
			AuthenticationResult.InvalidCredentials => SecurityEventSeverity.Medium,
			AuthenticationResult.AccountLocked => SecurityEventSeverity.High,
			_ => SecurityEventSeverity.Medium,
		};

	/// <summary>
	/// Determines the severity level for a data access event.
	/// </summary>
	internal static SecurityEventSeverity DetermineDataAccessSeverity(DataAccessEvent dataAccessEvent)
	{
		var baseSeverity = dataAccessEvent.Operation switch
		{
			DataAccessOperation.Read => SecurityEventSeverity.Low,
			DataAccessOperation.Write => SecurityEventSeverity.Low,
			DataAccessOperation.Update => SecurityEventSeverity.Medium,
			DataAccessOperation.Delete => SecurityEventSeverity.High,
			DataAccessOperation.Export => SecurityEventSeverity.High,
			_ => SecurityEventSeverity.Low,
		};

		// Elevate severity for sensitive data
		if (dataAccessEvent.DataClassification is "Sensitive" or "Confidential")
		{
			baseSeverity = baseSeverity switch
			{
				SecurityEventSeverity.Low => SecurityEventSeverity.Medium,
				SecurityEventSeverity.Medium => SecurityEventSeverity.High,
				SecurityEventSeverity.High => SecurityEventSeverity.Critical,
				_ => baseSeverity,
			};
		}

		return baseSeverity;
	}

	/// <summary>
	/// Determines the severity level for a configuration change event.
	/// </summary>
	internal static SecurityEventSeverity DetermineConfigurationChangeSeverity(ConfigurationChangeEvent configurationEvent) =>
		configurationEvent.ChangeType switch
		{
			ConfigurationChangeType.SecuritySettings => SecurityEventSeverity.High,
			ConfigurationChangeType.AuthenticationSettings => SecurityEventSeverity.High,
			ConfigurationChangeType.AuthorizationSettings => SecurityEventSeverity.High,
			ConfigurationChangeType.EncryptionSettings => SecurityEventSeverity.High,
			ConfigurationChangeType.NetworkSettings => SecurityEventSeverity.Medium,
			ConfigurationChangeType.ApplicationSettings => SecurityEventSeverity.Low,
			_ => SecurityEventSeverity.Medium,
		};

	/// <summary>
	/// Maps external security event type to internal event type.
	/// </summary>
	internal static SecurityEventType MapToSecurityEventType(string eventType) =>
		eventType.ToUpperInvariant() switch
		{
			"AUTHENTICATION" => SecurityEventType.Authentication,
			"DATAACCESS" => SecurityEventType.DataAccess,
			"CONFIGURATIONCHANGE" => SecurityEventType.ConfigurationChange,
			"SECURITYINCIDENT" => SecurityEventType.SecurityIncident,
			"ACCESSCONTROL" => SecurityEventType.AccessControl,
			_ => SecurityEventType.Other,
		};

	/// <summary>
	/// Maps external severity level to internal severity.
	/// </summary>
	internal static SecurityEventSeverity MapToSecurityEventSeverity(string severity) =>
		severity.ToUpperInvariant() switch
		{
			"CRITICAL" => SecurityEventSeverity.Critical,
			"HIGH" => SecurityEventSeverity.High,
			"MEDIUM" => SecurityEventSeverity.Medium,
			"LOW" => SecurityEventSeverity.Low,
			_ => SecurityEventSeverity.Low,
		};

	/// <summary>
	/// Maps ComplianceFramework to Auditing.ComplianceFramework.
	/// </summary>
	internal static ComplianceFramework MapToAuditingFramework(ComplianceFramework framework) =>
		// Both enums have the same values, so we can directly cast
		(ComplianceFramework)(int)framework;

	/// <summary>
	/// Checks for compliance violations in security events.
	/// </summary>
	private async Task CheckComplianceViolationAsync(SecurityAuditEvent securityEvent, CancellationToken cancellationToken)
	{
		foreach (var framework in _configuration.ComplianceFrameworks)
		{
			if (_complianceReporters.TryGetValue(framework, out var reporter))
			{
				var violation = await reporter.CheckComplianceViolationAsync(securityEvent, cancellationToken).ConfigureAwait(false);
				if (violation != null)
				{
					ComplianceViolationDetected?.Invoke(this, new ComplianceViolationEventArgs(
						MapToAuditingFramework(framework), violation.ViolationType, violation.Description, securityEvent.EventId));

					_logger.LogWarning(
						"Compliance violation detected for {Framework}: {ViolationType} - {Description}",
						framework, violation.ViolationType, violation.Description);
				}
			}
		}
	}
}
