using Excalibur.Dispatch.Compliance;

namespace Excalibur.Dispatch.Compliance.Tests.Observability;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class EncryptionTelemetryShould : IDisposable
{
	private readonly EncryptionTelemetry _sut = new();

	[Fact]
	public void Expose_non_null_meter()
	{
		_sut.Meter.ShouldNotBeNull();
		_sut.Meter.Name.ShouldBe(EncryptionTelemetry.MeterName);
	}

	[Fact]
	public void Have_correct_meter_name_constant()
	{
		EncryptionTelemetry.MeterName.ShouldBe("Excalibur.Dispatch.Encryption");
	}

	[Fact]
	public void Have_correct_meter_version_constant()
	{
		EncryptionTelemetry.MeterVersion.ShouldBe("1.0.0");
	}

	[Fact]
	public void Record_operation_without_throwing()
	{
		Should.NotThrow(() =>
			_sut.RecordOperation("Encrypt", "AES-256-GCM", "success", "TestProvider"));
	}

	[Fact]
	public void Throw_for_null_operation_in_record_operation()
	{
		Should.Throw<ArgumentNullException>(() =>
			_sut.RecordOperation(null!, "AES-256", "success", "TestProvider"));
	}

	[Fact]
	public void Throw_for_null_algorithm_in_record_operation()
	{
		Should.Throw<ArgumentNullException>(() =>
			_sut.RecordOperation("Encrypt", null!, "success", "TestProvider"));
	}

	[Fact]
	public void Throw_for_null_status_in_record_operation()
	{
		Should.Throw<ArgumentNullException>(() =>
			_sut.RecordOperation("Encrypt", "AES-256", null!, "TestProvider"));
	}

	[Fact]
	public void Throw_for_null_provider_in_record_operation()
	{
		Should.Throw<ArgumentNullException>(() =>
			_sut.RecordOperation("Encrypt", "AES-256", "success", null!));
	}

	[Fact]
	public void Record_operation_duration_without_throwing()
	{
		Should.NotThrow(() =>
			_sut.RecordOperationDuration(42.5, "Decrypt", "TestProvider"));
	}

	[Fact]
	public void Throw_for_null_operation_in_record_operation_duration()
	{
		Should.Throw<ArgumentNullException>(() =>
			_sut.RecordOperationDuration(42.5, null!, "TestProvider"));
	}

	[Fact]
	public void Throw_for_null_provider_in_record_operation_duration()
	{
		Should.Throw<ArgumentNullException>(() =>
			_sut.RecordOperationDuration(42.5, "Decrypt", null!));
	}

	[Fact]
	public void Update_provider_health_without_throwing()
	{
		Should.NotThrow(() =>
			_sut.UpdateProviderHealth("TestProvider", "healthy", 100));
	}

	[Fact]
	public void Throw_for_null_provider_in_update_provider_health()
	{
		Should.Throw<ArgumentNullException>(() =>
			_sut.UpdateProviderHealth(null!, "healthy", 100));
	}

	[Fact]
	public void Throw_for_null_status_in_update_provider_health()
	{
		Should.Throw<ArgumentNullException>(() =>
			_sut.UpdateProviderHealth("TestProvider", null!, 100));
	}

	[Fact]
	public void Record_fields_migrated_without_throwing()
	{
		Should.NotThrow(() =>
			_sut.RecordFieldsMigrated(10, "OldProvider", "NewProvider", "store1"));
	}

	[Fact]
	public void Not_record_fields_migrated_when_count_is_zero()
	{
		// Should not throw even with zero count (just skips recording)
		Should.NotThrow(() =>
			_sut.RecordFieldsMigrated(0, "OldProvider", "NewProvider", "store1"));
	}

	[Fact]
	public void Throw_for_null_from_provider_in_record_fields_migrated()
	{
		Should.Throw<ArgumentNullException>(() =>
			_sut.RecordFieldsMigrated(10, null!, "NewProvider", "store1"));
	}

	[Fact]
	public void Throw_for_null_to_provider_in_record_fields_migrated()
	{
		Should.Throw<ArgumentNullException>(() =>
			_sut.RecordFieldsMigrated(10, "OldProvider", null!, "store1"));
	}

	[Fact]
	public void Throw_for_null_store_in_record_fields_migrated()
	{
		Should.Throw<ArgumentNullException>(() =>
			_sut.RecordFieldsMigrated(10, "OldProvider", "NewProvider", null!));
	}

	[Fact]
	public void Record_key_rotation_without_throwing()
	{
		Should.NotThrow(() =>
			_sut.RecordKeyRotation("TestProvider", "scheduled"));
	}

	[Fact]
	public void Throw_for_null_provider_in_record_key_rotation()
	{
		Should.Throw<ArgumentNullException>(() =>
			_sut.RecordKeyRotation(null!, "scheduled"));
	}

	[Fact]
	public void Throw_for_null_rotation_type_in_record_key_rotation()
	{
		Should.Throw<ArgumentNullException>(() =>
			_sut.RecordKeyRotation("TestProvider", null!));
	}

	[Fact]
	public void Record_bytes_processed_without_throwing()
	{
		Should.NotThrow(() =>
			_sut.RecordBytesProcessed(1024, "Encrypt", "TestProvider"));
	}

	[Fact]
	public void Not_record_bytes_processed_when_zero()
	{
		// Zero bytes should not throw (just skips recording)
		Should.NotThrow(() =>
			_sut.RecordBytesProcessed(0, "Encrypt", "TestProvider"));
	}

	[Fact]
	public void Throw_for_null_operation_in_record_bytes_processed()
	{
		Should.Throw<ArgumentNullException>(() =>
			_sut.RecordBytesProcessed(1024, null!, "TestProvider"));
	}

	[Fact]
	public void Throw_for_null_provider_in_record_bytes_processed()
	{
		Should.Throw<ArgumentNullException>(() =>
			_sut.RecordBytesProcessed(1024, "Encrypt", null!));
	}

	[Fact]
	public void Record_cache_access_without_throwing()
	{
		Should.NotThrow(() => _sut.RecordCacheAccess(true, "TestProvider"));
		Should.NotThrow(() => _sut.RecordCacheAccess(false, "TestProvider"));
	}

	[Fact]
	public void Throw_for_null_provider_in_record_cache_access()
	{
		Should.Throw<ArgumentNullException>(() =>
			_sut.RecordCacheAccess(true, null!));
	}

	[Fact]
	public void Update_active_key_count_without_throwing()
	{
		Should.NotThrow(() =>
			_sut.UpdateActiveKeyCount(5, "TestProvider"));
	}

	[Fact]
	public void Throw_for_null_provider_in_update_active_key_count()
	{
		Should.Throw<ArgumentNullException>(() =>
			_sut.UpdateActiveKeyCount(5, null!));
	}

	[Fact]
	public void Return_self_for_encryption_telemetry_details_service()
	{
		var details = _sut.GetService(typeof(IEncryptionTelemetryDetails));

		details.ShouldNotBeNull();
		details.ShouldBeSameAs(_sut);
	}

	[Fact]
	public void Return_null_for_unknown_service_type()
	{
		var result = _sut.GetService(typeof(string));

		result.ShouldBeNull();
	}

	[Fact]
	public void Dispose_without_throwing()
	{
		var telemetry = new EncryptionTelemetry();

		Should.NotThrow(() => telemetry.Dispose());
	}

	[Fact]
	public void Handle_double_dispose_safely()
	{
		var telemetry = new EncryptionTelemetry();

		telemetry.Dispose();

		// Second dispose should not throw
		Should.NotThrow(() => telemetry.Dispose());
	}

	public void Dispose()
	{
		_sut.Dispose();
	}
}
