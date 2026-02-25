// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.Metrics;

namespace Excalibur.Dispatch.Security.Tests.Compliance.Observability;

[Trait("Category", TestCategories.Unit)]
public sealed class EncryptionTelemetryShould : IDisposable
{
	private readonly EncryptionTelemetry _sut;
	private readonly MeterListener _listener;
	private readonly List<(string Name, object? Value, KeyValuePair<string, object?>[] Tags)> _recordedMeasurements = [];

	public EncryptionTelemetryShould()
	{
		_sut = new EncryptionTelemetry();
		_listener = new MeterListener();

		_listener.InstrumentPublished = (instrument, listener) =>
		{
			if (instrument.Meter.Name == EncryptionTelemetry.MeterName)
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
		_sut.Meter.Name.ShouldBe("Excalibur.Dispatch.Encryption");
	}

	[Fact]
	public void HaveCorrectMeterVersion()
	{
		// Assert
		EncryptionTelemetry.MeterVersion.ShouldBe("1.0.0");
	}

	[Fact]
	public void RecordOperation()
	{
		// Act
		_sut.RecordOperation("Encrypt", "AES-256-GCM", "success", "AesGcm");
		_listener.RecordObservableInstruments();

		// Assert
		var measurement = _recordedMeasurements.FirstOrDefault(m =>
			m.Name == "dispatch.encryption.operations.total");

		measurement.ShouldNotBe(default);
		measurement.Value.ShouldBe(1L);
		measurement.Tags.ShouldContain(t => t.Key == "operation" && (string?)t.Value == "Encrypt");
		measurement.Tags.ShouldContain(t => t.Key == "algorithm" && (string?)t.Value == "AES-256-GCM");
		measurement.Tags.ShouldContain(t => t.Key == "status" && (string?)t.Value == "success");
		measurement.Tags.ShouldContain(t => t.Key == "provider" && (string?)t.Value == "AesGcm");
	}

	[Fact]
	public void RecordOperationDuration()
	{
		// Act
		_sut.RecordOperationDuration(15.5, "Encrypt", "AesGcm");
		_listener.RecordObservableInstruments();

		// Assert
		var measurement = _recordedMeasurements.FirstOrDefault(m =>
			m.Name == "dispatch.encryption.operation.duration");

		measurement.ShouldNotBe(default);
		measurement.Value.ShouldBe(15.5);
		measurement.Tags.ShouldContain(t => t.Key == "operation" && (string?)t.Value == "Encrypt");
		measurement.Tags.ShouldContain(t => t.Key == "provider" && (string?)t.Value == "AesGcm");
	}

	[Fact]
	public void UpdateProviderHealth()
	{
		// Act
		_sut.UpdateProviderHealth("AesGcm", "healthy", 100);
		_listener.RecordObservableInstruments();

		// Assert
		var measurement = _recordedMeasurements.FirstOrDefault(m =>
			m.Name == "dispatch.encryption.provider.health");

		measurement.ShouldNotBe(default);
		measurement.Value.ShouldBe(100);
		measurement.Tags.ShouldContain(t => t.Key == "provider" && (string?)t.Value == "AesGcm");
		measurement.Tags.ShouldContain(t => t.Key == "status" && (string?)t.Value == "healthy");
	}

	[Fact]
	public void RecordFieldsMigrated()
	{
		// Act
		_sut.RecordFieldsMigrated(500, "OldProvider", "NewProvider", "UserData");
		_listener.RecordObservableInstruments();

		// Assert
		var measurement = _recordedMeasurements.FirstOrDefault(m =>
			m.Name == "dispatch.encryption.migration.fields_migrated");

		measurement.ShouldNotBe(default);
		measurement.Value.ShouldBe(500L);
		measurement.Tags.ShouldContain(t => t.Key == "from_provider" && (string?)t.Value == "OldProvider");
		measurement.Tags.ShouldContain(t => t.Key == "to_provider" && (string?)t.Value == "NewProvider");
		measurement.Tags.ShouldContain(t => t.Key == "store" && (string?)t.Value == "UserData");
	}

	[Fact]
	public void RecordFieldsMigrated_SkipWhenZero()
	{
		// Act
		_sut.RecordFieldsMigrated(0, "OldProvider", "NewProvider", "UserData");
		_listener.RecordObservableInstruments();

		// Assert
		var measurement = _recordedMeasurements.FirstOrDefault(m =>
			m.Name == "dispatch.encryption.migration.fields_migrated");

		measurement.ShouldBe(default);
	}

	[Fact]
	public void RecordKeyRotation()
	{
		// Act
		_sut.RecordKeyRotation("AesGcm", "scheduled");
		_listener.RecordObservableInstruments();

		// Assert
		var measurement = _recordedMeasurements.FirstOrDefault(m =>
			m.Name == "dispatch.encryption.key_rotation.total");

		measurement.ShouldNotBe(default);
		measurement.Value.ShouldBe(1L);
		measurement.Tags.ShouldContain(t => t.Key == "provider" && (string?)t.Value == "AesGcm");
		measurement.Tags.ShouldContain(t => t.Key == "rotation_type" && (string?)t.Value == "scheduled");
	}

	[Fact]
	public void RecordBytesProcessed()
	{
		// Act
		_sut.RecordBytesProcessed(1024, "Encrypt", "AesGcm");
		_listener.RecordObservableInstruments();

		// Assert
		var measurement = _recordedMeasurements.FirstOrDefault(m =>
			m.Name == "dispatch.encryption.bytes.processed");

		measurement.ShouldNotBe(default);
		measurement.Value.ShouldBe(1024L);
		measurement.Tags.ShouldContain(t => t.Key == "operation" && (string?)t.Value == "Encrypt");
		measurement.Tags.ShouldContain(t => t.Key == "provider" && (string?)t.Value == "AesGcm");
	}

	[Fact]
	public void RecordBytesProcessed_SkipWhenZero()
	{
		// Act
		_sut.RecordBytesProcessed(0, "Encrypt", "AesGcm");
		_listener.RecordObservableInstruments();

		// Assert
		var measurement = _recordedMeasurements.FirstOrDefault(m =>
			m.Name == "dispatch.encryption.bytes.processed");

		measurement.ShouldBe(default);
	}

	[Fact]
	public void RecordCacheAccess_Hit()
	{
		// Act
		_sut.RecordCacheAccess(hit: true, "AesGcm");
		_listener.RecordObservableInstruments();

		// Assert
		var measurement = _recordedMeasurements.FirstOrDefault(m =>
			m.Name == "dispatch.encryption.cache.accesses");

		measurement.ShouldNotBe(default);
		measurement.Value.ShouldBe(1L);
		measurement.Tags.ShouldContain(t => t.Key == "hit" && (bool?)t.Value == true);
		measurement.Tags.ShouldContain(t => t.Key == "provider" && (string?)t.Value == "AesGcm");
	}

	[Fact]
	public void RecordCacheAccess_Miss()
	{
		// Act
		_sut.RecordCacheAccess(hit: false, "AesGcm");
		_listener.RecordObservableInstruments();

		// Assert
		var measurement = _recordedMeasurements.FirstOrDefault(m =>
			m.Name == "dispatch.encryption.cache.accesses");

		measurement.ShouldNotBe(default);
		measurement.Tags.ShouldContain(t => t.Key == "hit" && (bool?)t.Value == false);
	}

	[Fact]
	public void UpdateActiveKeyCount()
	{
		// Act
		_sut.UpdateActiveKeyCount(5, "AesGcm");
		_listener.RecordObservableInstruments();

		// Assert
		var measurement = _recordedMeasurements.FirstOrDefault(m =>
			m.Name == "dispatch.encryption.keys.active");

		measurement.ShouldNotBe(default);
		measurement.Value.ShouldBe(5);
		measurement.Tags.ShouldContain(t => t.Key == "provider" && (string?)t.Value == "AesGcm");
	}

	[Fact]
	public void ThrowOnNullOperation()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			_sut.RecordOperation(null!, "AES-256-GCM", "success", "AesGcm"));
	}

	[Fact]
	public void ThrowOnNullAlgorithm()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			_sut.RecordOperation("Encrypt", null!, "success", "AesGcm"));
	}

	[Fact]
	public void ThrowOnNullStatus()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			_sut.RecordOperation("Encrypt", "AES-256-GCM", null!, "AesGcm"));
	}

	[Fact]
	public void ThrowOnNullProvider()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			_sut.RecordOperation("Encrypt", "AES-256-GCM", "success", null!));
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
