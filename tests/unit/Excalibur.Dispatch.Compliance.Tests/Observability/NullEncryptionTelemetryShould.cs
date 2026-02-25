using Excalibur.Dispatch.Compliance;

namespace Excalibur.Dispatch.Compliance.Tests.Observability;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class NullEncryptionTelemetryShould
{
	[Fact]
	public void Expose_singleton_instance()
	{
		var instance = NullEncryptionTelemetry.Instance;

		instance.ShouldNotBeNull();
		instance.ShouldBeSameAs(NullEncryptionTelemetry.Instance);
	}

	[Fact]
	public void Expose_non_null_meter()
	{
		NullEncryptionTelemetry.Instance.Meter.ShouldNotBeNull();
	}

	[Fact]
	public void Not_throw_on_record_operation()
	{
		// No-op — should not throw
		Should.NotThrow(() =>
			NullEncryptionTelemetry.Instance.RecordOperation("Encrypt", "AES-256", "success", "TestProvider"));
	}

	[Fact]
	public void Not_throw_on_record_operation_duration()
	{
		Should.NotThrow(() =>
			NullEncryptionTelemetry.Instance.RecordOperationDuration(42.5, "Decrypt", "TestProvider"));
	}

	[Fact]
	public void Not_throw_on_update_provider_health()
	{
		Should.NotThrow(() =>
			NullEncryptionTelemetry.Instance.UpdateProviderHealth("TestProvider", "healthy", 100));
	}

	[Fact]
	public void Not_throw_on_record_fields_migrated()
	{
		Should.NotThrow(() =>
			NullEncryptionTelemetry.Instance.RecordFieldsMigrated(10, "OldProvider", "NewProvider", "store1"));
	}

	[Fact]
	public void Not_throw_on_record_key_rotation()
	{
		Should.NotThrow(() =>
			NullEncryptionTelemetry.Instance.RecordKeyRotation("TestProvider", "scheduled"));
	}

	[Fact]
	public void Not_throw_on_record_bytes_processed()
	{
		Should.NotThrow(() =>
			NullEncryptionTelemetry.Instance.RecordBytesProcessed(1024, "Encrypt", "TestProvider"));
	}

	[Fact]
	public void Not_throw_on_record_cache_access()
	{
		Should.NotThrow(() =>
			NullEncryptionTelemetry.Instance.RecordCacheAccess(true, "TestProvider"));

		Should.NotThrow(() =>
			NullEncryptionTelemetry.Instance.RecordCacheAccess(false, "TestProvider"));
	}

	[Fact]
	public void Not_throw_on_update_active_key_count()
	{
		Should.NotThrow(() =>
			NullEncryptionTelemetry.Instance.UpdateActiveKeyCount(5, "TestProvider"));
	}

	[Fact]
	public void Return_self_for_encryption_telemetry_details_service()
	{
		var details = NullEncryptionTelemetry.Instance.GetService(typeof(IEncryptionTelemetryDetails));

		details.ShouldNotBeNull();
		details.ShouldBeSameAs(NullEncryptionTelemetry.Instance);
	}

	[Fact]
	public void Return_null_for_unknown_service_type()
	{
		var result = NullEncryptionTelemetry.Instance.GetService(typeof(string));

		result.ShouldBeNull();
	}
}
