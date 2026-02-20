// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Security.Tests.Compliance.Observability;

[Trait("Category", TestCategories.Unit)]
public sealed class NullEncryptionTelemetryShould
{
	[Fact]
	public void ReturnSingletonInstance()
	{
		// Arrange & Act
		var instance1 = NullEncryptionTelemetry.Instance;
		var instance2 = NullEncryptionTelemetry.Instance;

		// Assert
		instance1.ShouldBeSameAs(instance2);
	}

	[Fact]
	public void HaveMeterWithNullSuffix()
	{
		// Arrange
		var sut = NullEncryptionTelemetry.Instance;

		// Assert
		sut.Meter.Name.ShouldBe("Excalibur.Dispatch.Encryption.Null");
	}

	[Fact]
	public void NotThrow_WhenRecordingOperation()
	{
		// Arrange
		var sut = NullEncryptionTelemetry.Instance;

		// Act & Assert - should not throw
		Should.NotThrow(() => sut.RecordOperation("Encrypt", "AES-256-GCM", "success", "AesGcm"));
	}

	[Fact]
	public void NotThrow_WhenRecordingOperationDuration()
	{
		// Arrange
		var sut = NullEncryptionTelemetry.Instance;

		// Act & Assert - should not throw
		Should.NotThrow(() => sut.RecordOperationDuration(15.5, "Encrypt", "AesGcm"));
	}

	[Fact]
	public void NotThrow_WhenUpdatingProviderHealth()
	{
		// Arrange
		var sut = NullEncryptionTelemetry.Instance;

		// Act & Assert - should not throw
		Should.NotThrow(() => sut.UpdateProviderHealth("AesGcm", "healthy", 100));
	}

	[Fact]
	public void NotThrow_WhenRecordingFieldsMigrated()
	{
		// Arrange
		var sut = NullEncryptionTelemetry.Instance;

		// Act & Assert - should not throw
		Should.NotThrow(() => sut.RecordFieldsMigrated(500, "OldProvider", "NewProvider", "UserData"));
	}

	[Fact]
	public void NotThrow_WhenRecordingKeyRotation()
	{
		// Arrange
		var sut = NullEncryptionTelemetry.Instance;

		// Act & Assert - should not throw
		Should.NotThrow(() => sut.RecordKeyRotation("AesGcm", "scheduled"));
	}

	[Fact]
	public void NotThrow_WhenRecordingBytesProcessed()
	{
		// Arrange
		var sut = NullEncryptionTelemetry.Instance;

		// Act & Assert - should not throw
		Should.NotThrow(() => sut.RecordBytesProcessed(1024, "Encrypt", "AesGcm"));
	}

	[Fact]
	public void NotThrow_WhenRecordingCacheAccess()
	{
		// Arrange
		var sut = NullEncryptionTelemetry.Instance;

		// Act & Assert - should not throw
		Should.NotThrow(() => sut.RecordCacheAccess(hit: true, "AesGcm"));
	}

	[Fact]
	public void NotThrow_WhenUpdatingActiveKeyCount()
	{
		// Arrange
		var sut = NullEncryptionTelemetry.Instance;

		// Act & Assert - should not throw
		Should.NotThrow(() => sut.UpdateActiveKeyCount(5, "AesGcm"));
	}
}
