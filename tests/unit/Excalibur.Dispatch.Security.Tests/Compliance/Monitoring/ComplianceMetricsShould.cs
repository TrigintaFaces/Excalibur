// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.Metrics;

namespace Excalibur.Dispatch.Security.Tests.Compliance.Monitoring;

[Trait("Category", TestCategories.Unit)]
public sealed class ComplianceMetricsShould : IDisposable
{
	private readonly ComplianceMetrics _sut;
	private readonly MeterListener _listener;
	private readonly List<(string Name, object? Value, KeyValuePair<string, object?>[] Tags)> _recordedMeasurements = [];

	public ComplianceMetricsShould()
	{
		_sut = new ComplianceMetrics();
		_listener = new MeterListener();

		_listener.InstrumentPublished = (instrument, listener) =>
		{
			if (ReferenceEquals(instrument.Meter, _sut.Meter))
			{
				listener.EnableMeasurementEvents(instrument);
			}
		};

		_listener.SetMeasurementEventCallback<long>(OnMeasurement);
		_listener.SetMeasurementEventCallback<int>(OnMeasurement);
		_listener.SetMeasurementEventCallback<double>(OnMeasurement);

		_listener.Start();
	}

	public void Dispose()
	{
		_listener.Dispose();
		_sut.Dispose();
	}

	[Fact]
	public void HaveCorrectMeterName()
	{
		// Assert
		_sut.Meter.Name.ShouldBe("Excalibur.Dispatch.Compliance");
	}

	[Fact]
	public void RecordKeyRotation()
	{
		// Act
		_sut.RecordKeyRotation("key-1", "InMemory");
		_listener.RecordObservableInstruments();

		// Assert
		var measurement = _recordedMeasurements.FirstOrDefault(m =>
			m.Name == "dispatch.compliance.key_rotations");

		measurement.ShouldNotBe(default);
		measurement.Value.ShouldBe(1L);
		measurement.Tags.ShouldContain(t => t.Key == "key_id" && (string?)t.Value == "key-1");
		measurement.Tags.ShouldContain(t => t.Key == "provider" && (string?)t.Value == "InMemory");
	}

	[Fact]
	public void RecordKeyRotationFailure()
	{
		// Act
		_sut.RecordKeyRotationFailure("key-1", "AzureKeyVault", "Timeout");
		_listener.RecordObservableInstruments();

		// Assert
		var measurement = _recordedMeasurements.FirstOrDefault(m =>
			m.Name == "dispatch.compliance.key_rotation_failures");

		measurement.ShouldNotBe(default);
		measurement.Value.ShouldBe(1L);
		measurement.Tags.ShouldContain(t => t.Key == "key_id" && (string?)t.Value == "key-1");
		measurement.Tags.ShouldContain(t => t.Key == "provider" && (string?)t.Value == "AzureKeyVault");
		measurement.Tags.ShouldContain(t => t.Key == "error_type" && (string?)t.Value == "Timeout");
	}

	[Fact]
	public void UpdateKeysNearingExpiration()
	{
		// Act
		_sut.UpdateKeysNearingExpiration(5, "AwsKms");
		_listener.RecordObservableInstruments();

		// Assert
		var measurement = _recordedMeasurements.FirstOrDefault(m =>
			m.Name == "dispatch.compliance.keys_nearing_expiration");

		measurement.ShouldNotBe(default);
		measurement.Value.ShouldBe(5);
		measurement.Tags.ShouldContain(t => t.Key == "provider" && (string?)t.Value == "AwsKms");
	}

	[Fact]
	public void RecordEncryptionLatency()
	{
		// Act
		_sut.RecordEncryptionLatency(15.5, "encrypt", "AesGcm", success: true);
		_listener.RecordObservableInstruments();

		// Assert
		var measurement = _recordedMeasurements.FirstOrDefault(m =>
			m.Name == "dispatch.compliance.encryption_latency");

		measurement.ShouldNotBe(default);
		measurement.Value.ShouldBe(15.5);
		measurement.Tags.ShouldContain(t => t.Key == "operation" && (string?)t.Value == "encrypt");
		measurement.Tags.ShouldContain(t => t.Key == "provider" && (string?)t.Value == "AesGcm");
		measurement.Tags.ShouldContain(t => t.Key == "success" && (bool?)t.Value == true);
	}

	[Fact]
	public void RecordEncryptionOperation()
	{
		// Act
		_sut.RecordEncryptionOperation("decrypt", "AesGcm", 1024);
		_listener.RecordObservableInstruments();

		// Assert
		var operationMeasurement = _recordedMeasurements.FirstOrDefault(m =>
			m.Name == "dispatch.compliance.encryption_operations");

		operationMeasurement.ShouldNotBe(default);
		operationMeasurement.Value.ShouldBe(1L);
		operationMeasurement.Tags.ShouldContain(t => t.Key == "operation" && (string?)t.Value == "decrypt");

		var bytesMeasurement = _recordedMeasurements.FirstOrDefault(m =>
			m.Name == "dispatch.compliance.encryption_bytes_processed");

		bytesMeasurement.ShouldNotBe(default);
		bytesMeasurement.Value.ShouldBe(1024L);
	}

	[Fact]
	public void RecordEncryptionOperation_SkipBytesWhenZero()
	{
		// Act
		_sut.RecordEncryptionOperation("encrypt", "AesGcm", 0);
		_listener.RecordObservableInstruments();

		// Assert - should record operation but not bytes
		var operationMeasurement = _recordedMeasurements.FirstOrDefault(m =>
			m.Name == "dispatch.compliance.encryption_operations");
		operationMeasurement.ShouldNotBe(default);

		var bytesMeasurement = _recordedMeasurements.FirstOrDefault(m =>
			m.Name == "dispatch.compliance.encryption_bytes_processed");
		bytesMeasurement.ShouldBe(default);
	}

	[Fact]
	public void RecordAuditEventLogged_WithoutTenantId()
	{
		// Act
		_sut.RecordAuditEventLogged("KeyRotation", "Success");
		_listener.RecordObservableInstruments();

		// Assert
		var measurement = _recordedMeasurements.FirstOrDefault(m =>
			m.Name == "dispatch.compliance.audit_events_logged");

		measurement.ShouldNotBe(default);
		measurement.Value.ShouldBe(1L);
		measurement.Tags.ShouldContain(t => t.Key == "event_type" && (string?)t.Value == "KeyRotation");
		measurement.Tags.ShouldContain(t => t.Key == "outcome" && (string?)t.Value == "Success");
		measurement.Tags.ShouldNotContain(t => t.Key == "tenant_id");
	}

	[Fact]
	public void RecordAuditEventLogged_WithTenantId()
	{
		// Act
		_sut.RecordAuditEventLogged("DataAccess", "Failure", "tenant-123");
		_listener.RecordObservableInstruments();

		// Assert
		var measurement = _recordedMeasurements.FirstOrDefault(m =>
			m.Name == "dispatch.compliance.audit_events_logged");

		measurement.ShouldNotBe(default);
		measurement.Tags.ShouldContain(t => t.Key == "tenant_id" && (string?)t.Value == "tenant-123");
	}

	[Fact]
	public void UpdateAuditBacklogSize()
	{
		// Act
		_sut.UpdateAuditBacklogSize(42);
		_listener.RecordObservableInstruments();

		// Assert
		var measurement = _recordedMeasurements.FirstOrDefault(m =>
			m.Name == "dispatch.compliance.audit_backlog_size");

		measurement.ShouldNotBe(default);
		measurement.Value.ShouldBe(42);
	}

	[Fact]
	public void RecordAuditIntegrityCheck_WithViolations()
	{
		// Act
		_sut.RecordAuditIntegrityCheck(1000, 3, 250.5);
		_listener.RecordObservableInstruments();

		// Assert
		var checkMeasurement = _recordedMeasurements.FirstOrDefault(m =>
			m.Name == "dispatch.compliance.audit_integrity_checks");

		checkMeasurement.ShouldNotBe(default);
		checkMeasurement.Value.ShouldBe(1L);

		var violationMeasurement = _recordedMeasurements.FirstOrDefault(m =>
			m.Name == "dispatch.compliance.audit_integrity_violations");

		violationMeasurement.ShouldNotBe(default);
		violationMeasurement.Value.ShouldBe(3L);

		var durationMeasurement = _recordedMeasurements.FirstOrDefault(m =>
			m.Name == "dispatch.compliance.audit_integrity_check_duration");

		durationMeasurement.ShouldNotBe(default);
		durationMeasurement.Value.ShouldBe(250.5);
	}

	[Fact]
	public void RecordAuditIntegrityCheck_WithoutViolations()
	{
		// Act
		_sut.RecordAuditIntegrityCheck(500, 0, 100.0);
		_listener.RecordObservableInstruments();

		// Assert
		var violationMeasurement = _recordedMeasurements.FirstOrDefault(m =>
			m.Name == "dispatch.compliance.audit_integrity_violations");

		// Should not record violations when count is 0
		violationMeasurement.ShouldBe(default);
	}

	[Fact]
	public void RecordKeyUsage()
	{
		// Act
		_sut.RecordKeyUsage("key-1", "InMemory", "encrypt");
		_listener.RecordObservableInstruments();

		// Assert
		var measurement = _recordedMeasurements.FirstOrDefault(m =>
			m.Name == "dispatch.compliance.key_usage_operations");

		measurement.ShouldNotBe(default);
		measurement.Value.ShouldBe(1L);
		measurement.Tags.ShouldContain(t => t.Key == "key_id" && (string?)t.Value == "key-1");
		measurement.Tags.ShouldContain(t => t.Key == "provider" && (string?)t.Value == "InMemory");
		measurement.Tags.ShouldContain(t => t.Key == "operation" && (string?)t.Value == "encrypt");
	}

	[Fact]
	public void DisposeSafely_WhenCalledMultipleTimes()
	{
		// Act & Assert - should not throw
		_sut.Dispose();
		_sut.Dispose();
	}

	private void OnMeasurement<T>(
																	Instrument instrument,
		T measurement,
		ReadOnlySpan<KeyValuePair<string, object?>> tags,
		object? state)
	{
		_recordedMeasurements.Add((instrument.Name, measurement, tags.ToArray()));
	}
}
