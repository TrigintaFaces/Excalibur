using Excalibur.Dispatch.Compliance;

namespace Excalibur.Dispatch.Compliance.Tests.Monitoring;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class ComplianceMetricsShould : IDisposable
{
	private readonly ComplianceMetrics _sut = new();

	[Fact]
	public void Have_correct_meter_name()
	{
		ComplianceMetrics.MeterName.ShouldBe("Excalibur.Dispatch.Compliance");
	}

	[Fact]
	public void Expose_non_null_meter()
	{
		_sut.Meter.ShouldNotBeNull();
		_sut.Meter.Name.ShouldBe(ComplianceMetrics.MeterName);
	}

	[Fact]
	public void Record_key_rotation_without_throwing()
	{
		Should.NotThrow(() => _sut.RecordKeyRotation("key-1", "AesProvider"));
	}

	[Fact]
	public void Record_key_rotation_failure_without_throwing()
	{
		Should.NotThrow(() =>
			_sut.RecordKeyRotationFailure("key-1", "AesProvider", "ProviderUnavailable"));
	}

	[Fact]
	public void Update_keys_nearing_expiration_without_throwing()
	{
		Should.NotThrow(() => _sut.UpdateKeysNearingExpiration(3, "AesProvider"));
	}

	[Fact]
	public void Record_encryption_latency_without_throwing()
	{
		Should.NotThrow(() =>
			_sut.RecordEncryptionLatency(15.5, "Encrypt", "AesProvider", true));
	}

	[Fact]
	public void Record_encryption_latency_for_failures()
	{
		Should.NotThrow(() =>
			_sut.RecordEncryptionLatency(100.0, "Decrypt", "AesProvider", false));
	}

	[Fact]
	public void Record_encryption_operation_without_throwing()
	{
		Should.NotThrow(() =>
			_sut.RecordEncryptionOperation("Encrypt", "AesProvider", 1024));
	}

	[Fact]
	public void Record_encryption_operation_with_zero_bytes()
	{
		Should.NotThrow(() =>
			_sut.RecordEncryptionOperation("Encrypt", "AesProvider", 0));
	}

	[Fact]
	public void Record_audit_event_logged_without_throwing()
	{
		Should.NotThrow(() =>
			_sut.RecordAuditEventLogged("EncryptionOperation", "Success"));
	}

	[Fact]
	public void Record_audit_event_logged_with_tenant_id()
	{
		Should.NotThrow(() =>
			_sut.RecordAuditEventLogged("EncryptionOperation", "Success", "tenant-1"));
	}

	[Fact]
	public void Record_audit_event_logged_with_null_tenant_id()
	{
		Should.NotThrow(() =>
			_sut.RecordAuditEventLogged("EncryptionOperation", "Success", null));
	}

	[Fact]
	public void Record_audit_event_logged_with_empty_tenant_id()
	{
		Should.NotThrow(() =>
			_sut.RecordAuditEventLogged("EncryptionOperation", "Success", ""));
	}

	[Fact]
	public void Update_audit_backlog_size_without_throwing()
	{
		Should.NotThrow(() => _sut.UpdateAuditBacklogSize(42));
	}

	[Fact]
	public void Record_audit_integrity_check_without_throwing()
	{
		Should.NotThrow(() =>
			_sut.RecordAuditIntegrityCheck(1000, 0, 250.5));
	}

	[Fact]
	public void Record_audit_integrity_check_with_violations()
	{
		Should.NotThrow(() =>
			_sut.RecordAuditIntegrityCheck(1000, 5, 300.0));
	}

	[Fact]
	public void Record_key_usage_without_throwing()
	{
		Should.NotThrow(() =>
			_sut.RecordKeyUsage("key-1", "AesProvider", "Encrypt"));
	}

	[Fact]
	public void Dispose_without_throwing()
	{
		var metrics = new ComplianceMetrics();

		Should.NotThrow(() => metrics.Dispose());
	}

	[Fact]
	public void Handle_double_dispose_safely()
	{
		var metrics = new ComplianceMetrics();

		metrics.Dispose();

		Should.NotThrow(() => metrics.Dispose());
	}

	public void Dispose()
	{
		_sut.Dispose();
	}
}
