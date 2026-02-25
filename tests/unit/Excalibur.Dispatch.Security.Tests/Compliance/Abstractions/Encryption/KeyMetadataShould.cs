// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Security.Tests.Compliance.Abstractions.Encryption;

/// <summary>
/// Unit tests for <see cref="KeyMetadata"/> record.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
[Trait("Feature", "Encryption")]
public sealed class KeyMetadataShould : UnitTestBase
{
	[Fact]
	public void CreateValidKeyMetadataWithRequiredProperties()
	{
		// Arrange
		var createdAt = DateTimeOffset.UtcNow;

		// Act
		var metadata = new KeyMetadata
		{
			KeyId = "key-001",
			Version = 1,
			Status = KeyStatus.Active,
			Algorithm = EncryptionAlgorithm.Aes256Gcm,
			CreatedAt = createdAt
		};

		// Assert
		metadata.KeyId.ShouldBe("key-001");
		metadata.Version.ShouldBe(1);
		metadata.Status.ShouldBe(KeyStatus.Active);
		metadata.Algorithm.ShouldBe(EncryptionAlgorithm.Aes256Gcm);
		metadata.CreatedAt.ShouldBe(createdAt);
	}

	[Fact]
	public void CreateFullyPopulatedKeyMetadata()
	{
		// Arrange
		var createdAt = DateTimeOffset.UtcNow.AddMonths(-6);
		var expiresAt = DateTimeOffset.UtcNow.AddMonths(6);
		var lastRotatedAt = DateTimeOffset.UtcNow.AddMonths(-1);

		// Act
		var metadata = new KeyMetadata
		{
			KeyId = "key-002",
			Version = 3,
			Status = KeyStatus.Active,
			Algorithm = EncryptionAlgorithm.Aes256CbcHmac,
			CreatedAt = createdAt,
			ExpiresAt = expiresAt,
			LastRotatedAt = lastRotatedAt,
			Purpose = "field-encryption",
			IsFipsCompliant = true
		};

		// Assert
		metadata.KeyId.ShouldBe("key-002");
		metadata.Version.ShouldBe(3);
		metadata.Status.ShouldBe(KeyStatus.Active);
		metadata.Algorithm.ShouldBe(EncryptionAlgorithm.Aes256CbcHmac);
		metadata.CreatedAt.ShouldBe(createdAt);
		metadata.ExpiresAt.ShouldBe(expiresAt);
		metadata.LastRotatedAt.ShouldBe(lastRotatedAt);
		metadata.Purpose.ShouldBe("field-encryption");
		metadata.IsFipsCompliant.ShouldBeTrue();
	}

	[Fact]
	public void HaveNullOptionalPropertiesByDefault()
	{
		// Act
		var metadata = new KeyMetadata
		{
			KeyId = "key-003",
			Version = 1,
			Status = KeyStatus.Active,
			Algorithm = EncryptionAlgorithm.Aes256Gcm,
			CreatedAt = DateTimeOffset.UtcNow
		};

		// Assert
		metadata.ExpiresAt.ShouldBeNull();
		metadata.LastRotatedAt.ShouldBeNull();
		metadata.Purpose.ShouldBeNull();
		metadata.IsFipsCompliant.ShouldBeFalse();
	}

	[Fact]
	public void SupportRecordEquality()
	{
		// Arrange
		var createdAt = DateTimeOffset.UtcNow;

		var metadata1 = new KeyMetadata
		{
			KeyId = "key-001",
			Version = 1,
			Status = KeyStatus.Active,
			Algorithm = EncryptionAlgorithm.Aes256Gcm,
			CreatedAt = createdAt
		};

		var metadata2 = new KeyMetadata
		{
			KeyId = "key-001",
			Version = 1,
			Status = KeyStatus.Active,
			Algorithm = EncryptionAlgorithm.Aes256Gcm,
			CreatedAt = createdAt
		};

		// Assert
		metadata1.ShouldBe(metadata2);
	}

	[Theory]
	[InlineData(EncryptionAlgorithm.Aes256Gcm)]
	[InlineData(EncryptionAlgorithm.Aes256CbcHmac)]
	public void SupportAllEncryptionAlgorithms(EncryptionAlgorithm algorithm)
	{
		// Act
		var metadata = new KeyMetadata
		{
			KeyId = "key-alg",
			Version = 1,
			Status = KeyStatus.Active,
			Algorithm = algorithm,
			CreatedAt = DateTimeOffset.UtcNow
		};

		// Assert
		metadata.Algorithm.ShouldBe(algorithm);
	}

	[Theory]
	[InlineData(KeyStatus.Active)]
	[InlineData(KeyStatus.DecryptOnly)]
	[InlineData(KeyStatus.PendingDestruction)]
	[InlineData(KeyStatus.Destroyed)]
	[InlineData(KeyStatus.Suspended)]
	public void SupportAllKeyStatuses(KeyStatus status)
	{
		// Act
		var metadata = new KeyMetadata
		{
			KeyId = "key-status",
			Version = 1,
			Status = status,
			Algorithm = EncryptionAlgorithm.Aes256Gcm,
			CreatedAt = DateTimeOffset.UtcNow
		};

		// Assert
		metadata.Status.ShouldBe(status);
	}

	[Fact]
	public void SupportIncrementingVersions()
	{
		// Act
		var v1 = new KeyMetadata
		{
			KeyId = "key-versioned",
			Version = 1,
			Status = KeyStatus.DecryptOnly,
			Algorithm = EncryptionAlgorithm.Aes256Gcm,
			CreatedAt = DateTimeOffset.UtcNow.AddYears(-1)
		};

		var v2 = new KeyMetadata
		{
			KeyId = "key-versioned",
			Version = 2,
			Status = KeyStatus.Active,
			Algorithm = EncryptionAlgorithm.Aes256Gcm,
			CreatedAt = DateTimeOffset.UtcNow
		};

		// Assert
		v2.Version.ShouldBeGreaterThan(v1.Version);
		v1.Status.ShouldBe(KeyStatus.DecryptOnly); // Old key should be decrypt-only
		v2.Status.ShouldBe(KeyStatus.Active); // New key should be active
	}
}
