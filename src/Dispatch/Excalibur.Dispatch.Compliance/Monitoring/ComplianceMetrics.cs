// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.Metrics;

using Excalibur.Dispatch.Compliance.Diagnostics;

namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Provides OpenTelemetry-based metrics collection for compliance features.
/// </summary>
/// <remarks>
/// <para>
/// This implementation uses the System.Diagnostics.Metrics API which is compatible
/// with OpenTelemetry exporters. Metrics are named following OpenTelemetry semantic
/// conventions with the "dispatch.compliance" prefix.
/// </para>
/// <para>
/// Grafana dashboard templates are available for visualizing these metrics.
/// </para>
/// </remarks>
public sealed class ComplianceMetrics : IComplianceMetrics, IDisposable
{
	/// <summary>
	/// The meter name for compliance metrics.
	/// </summary>
	public const string MeterName = "Excalibur.Dispatch.Compliance";

	private readonly Counter<long> _keyRotations;
	private readonly Counter<long> _keyRotationFailures;
	private readonly Gauge<int> _keysNearingExpiration;
	private readonly Histogram<double> _encryptionLatency;
	private readonly Counter<long> _encryptionOperations;
	private readonly Counter<long> _encryptionBytesProcessed;
	private readonly Counter<long> _auditEventsLogged;
	private readonly Gauge<int> _auditBacklogSize;
	private readonly Counter<long> _auditIntegrityChecks;
	private readonly Counter<long> _auditIntegrityViolations;
	private readonly Histogram<double> _auditIntegrityCheckDuration;
	private readonly Counter<long> _keyUsageOperations;
	private readonly TagCardinalityGuard _keyIdGuard = new(maxCardinality: 128);
	private readonly TagCardinalityGuard _providerGuard = new(maxCardinality: 50);
	private readonly TagCardinalityGuard _errorTypeGuard = new(maxCardinality: 50);
	private readonly TagCardinalityGuard _eventTypeGuard = new(maxCardinality: 128);
	private readonly TagCardinalityGuard _operationGuard = new(maxCardinality: 50);
	private readonly TagCardinalityGuard _tenantIdGuard = new(maxCardinality: 128);
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="ComplianceMetrics"/> class.
	/// </summary>
	public ComplianceMetrics()
	{
		Meter = new Meter(MeterName, "1.0.0");

		// Key rotation metrics
		_keyRotations = Meter.CreateCounter<long>(
			"dispatch.compliance.key_rotations",
			"count",
			"Total number of successful key rotations");

		_keyRotationFailures = Meter.CreateCounter<long>(
			"dispatch.compliance.key_rotation_failures",
			"count",
			"Total number of failed key rotation attempts");

		_keysNearingExpiration = Meter.CreateGauge<int>(
			"dispatch.compliance.keys_nearing_expiration",
			"count",
			"Number of keys approaching their expiration date");

		// Encryption metrics
		_encryptionLatency = Meter.CreateHistogram<double>(
			"dispatch.compliance.encryption_latency",
			"ms",
			"Encryption/decryption operation latency in milliseconds");

		_encryptionOperations = Meter.CreateCounter<long>(
			"dispatch.compliance.encryption_operations",
			"count",
			"Total number of encryption/decryption operations");

		_encryptionBytesProcessed = Meter.CreateCounter<long>(
			"dispatch.compliance.encryption_bytes_processed",
			"bytes",
			"Total bytes processed by encryption operations");

		// Audit metrics
		_auditEventsLogged = Meter.CreateCounter<long>(
			"dispatch.compliance.audit_events_logged",
			"count",
			"Total number of audit events logged");

		_auditBacklogSize = Meter.CreateGauge<int>(
			"dispatch.compliance.audit_backlog_size",
			"count",
			"Number of audit events waiting to be processed");

		_auditIntegrityChecks = Meter.CreateCounter<long>(
			"dispatch.compliance.audit_integrity_checks",
			"count",
			"Total number of audit integrity checks performed");

		_auditIntegrityViolations = Meter.CreateCounter<long>(
			"dispatch.compliance.audit_integrity_violations",
			"count",
			"Total number of audit integrity violations detected");

		_auditIntegrityCheckDuration = Meter.CreateHistogram<double>(
			"dispatch.compliance.audit_integrity_check_duration",
			"ms",
			"Duration of audit integrity verification in milliseconds");

		// Key usage tracking
		_keyUsageOperations = Meter.CreateCounter<long>(
			"dispatch.compliance.key_usage_operations",
			"count",
			"Total number of key usage operations");
	}

	/// <inheritdoc />
	public Meter Meter { get; }

	/// <inheritdoc />
	public void RecordKeyRotation(string keyId, string provider)
	{
		_keyRotations.Add(1,
			new KeyValuePair<string, object?>("key_id", _keyIdGuard.Guard(keyId)),
			new KeyValuePair<string, object?>("provider", _providerGuard.Guard(provider)));
	}

	/// <inheritdoc />
	public void RecordKeyRotationFailure(string keyId, string provider, string errorType)
	{
		_keyRotationFailures.Add(1,
			new KeyValuePair<string, object?>("key_id", _keyIdGuard.Guard(keyId)),
			new KeyValuePair<string, object?>("provider", _providerGuard.Guard(provider)),
			new KeyValuePair<string, object?>("error_type", _errorTypeGuard.Guard(errorType)));
	}

	/// <inheritdoc />
	public void UpdateKeysNearingExpiration(int count, string provider)
	{
		_keysNearingExpiration.Record(count,
			new KeyValuePair<string, object?>("provider", _providerGuard.Guard(provider)));
	}

	/// <inheritdoc />
	public void RecordEncryptionLatency(double durationMs, string operation, string provider, bool success)
	{
		_encryptionLatency.Record(durationMs,
			new KeyValuePair<string, object?>("operation", _operationGuard.Guard(operation)),
			new KeyValuePair<string, object?>("provider", _providerGuard.Guard(provider)),
			new KeyValuePair<string, object?>("success", success));
	}

	/// <inheritdoc />
	public void RecordEncryptionOperation(string operation, string provider, long sizeBytes)
	{
		var guardedOperation = _operationGuard.Guard(operation);
		var guardedProvider = _providerGuard.Guard(provider);

		_encryptionOperations.Add(1,
			new KeyValuePair<string, object?>("operation", guardedOperation),
			new KeyValuePair<string, object?>("provider", guardedProvider));

		if (sizeBytes > 0)
		{
			_encryptionBytesProcessed.Add(sizeBytes,
				new KeyValuePair<string, object?>("operation", guardedOperation),
				new KeyValuePair<string, object?>("provider", guardedProvider));
		}
	}

	/// <inheritdoc />
	public void RecordAuditEventLogged(string eventType, string outcome, string? tenantId = null)
	{
		var tags = new List<KeyValuePair<string, object?>>
		{
			new("event_type", _eventTypeGuard.Guard(eventType)),
			new("outcome", outcome)
		};

		if (!string.IsNullOrEmpty(tenantId))
		{
			tags.Add(new KeyValuePair<string, object?>("tenant_id", _tenantIdGuard.Guard(tenantId)));
		}

		_auditEventsLogged.Add(1, [.. tags]);
	}

	/// <inheritdoc />
	public void UpdateAuditBacklogSize(int count)
	{
		_auditBacklogSize.Record(count);
	}

	/// <inheritdoc />
	public void RecordAuditIntegrityCheck(long eventsVerified, int violationsFound, double durationMs)
	{
		_auditIntegrityChecks.Add(1,
			new KeyValuePair<string, object?>("events_verified", eventsVerified));

		if (violationsFound > 0)
		{
			_auditIntegrityViolations.Add(violationsFound);
		}

		_auditIntegrityCheckDuration.Record(durationMs);
	}

	/// <inheritdoc />
	public void RecordKeyUsage(string keyId, string provider, string operation)
	{
		_keyUsageOperations.Add(1,
			new KeyValuePair<string, object?>("key_id", _keyIdGuard.Guard(keyId)),
			new KeyValuePair<string, object?>("provider", _providerGuard.Guard(provider)),
			new KeyValuePair<string, object?>("operation", _operationGuard.Guard(operation)));
	}

	/// <inheritdoc />
	public void Dispose()
	{
		if (!_disposed)
		{
			Meter.Dispose();
			_disposed = true;
		}
	}
}
