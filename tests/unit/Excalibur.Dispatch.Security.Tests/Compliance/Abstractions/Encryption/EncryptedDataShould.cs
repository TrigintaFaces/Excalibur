// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Security.Tests.Compliance.Abstractions.Encryption;

/// <summary>
/// Unit tests for <see cref="EncryptedData"/> record.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
[Trait("Feature", "Encryption")]
public sealed class EncryptedDataShould : UnitTestBase
{
	[Fact]
	public void HaveMagicBytesThatIdentifyEncryptedData()
	{
		// Assert - Magic bytes are "EXCR" (0x45, 0x58, 0x43, 0x52)
		EncryptedData.MagicBytes.Length.ShouldBe(4);
		EncryptedData.MagicBytes[0].ShouldBe((byte)0x45); // 'E'
		EncryptedData.MagicBytes[1].ShouldBe((byte)0x58); // 'X'
		EncryptedData.MagicBytes[2].ShouldBe((byte)0x43); // 'C'
		EncryptedData.MagicBytes[3].ShouldBe((byte)0x52); // 'R'
	}

	[Fact]
	public void DetectEncryptedDataWithMagicBytesPrefix()
	{
		// Arrange
		byte[] encryptedData = [0x45, 0x58, 0x43, 0x52, 0x00, 0x01, 0x02, 0x03];

		// Act & Assert
		EncryptedData.IsFieldEncrypted(encryptedData).ShouldBeTrue();
		EncryptedData.IsFieldEncrypted(encryptedData.AsSpan()).ShouldBeTrue();
	}

	[Fact]
	public void NotDetectUnencryptedDataAsMagicBytes()
	{
		// Arrange
		byte[] plainData = [0x00, 0x01, 0x02, 0x03, 0x04, 0x05];

		// Act & Assert
		EncryptedData.IsFieldEncrypted(plainData).ShouldBeFalse();
		EncryptedData.IsFieldEncrypted(plainData.AsSpan()).ShouldBeFalse();
	}

	[Fact]
	public void ReturnFalseForNullData()
	{
		// Act & Assert
		EncryptedData.IsFieldEncrypted(null).ShouldBeFalse();
	}

	[Fact]
	public void ReturnFalseForEmptyData()
	{
		// Arrange
		byte[] empty = [];

		// Act & Assert
		EncryptedData.IsFieldEncrypted(empty).ShouldBeFalse();
		EncryptedData.IsFieldEncrypted(empty.AsSpan()).ShouldBeFalse();
	}

	[Fact]
	public void ReturnFalseForDataShorterThanMagicBytes()
	{
		// Arrange
		byte[] shortData = [0x45, 0x58, 0x43]; // Only 3 bytes, need 4

		// Act & Assert
		EncryptedData.IsFieldEncrypted(shortData).ShouldBeFalse();
	}

	[Fact]
	public void CreateValidEncryptedDataRecord()
	{
		// Arrange
		var ciphertext = new byte[] { 0x01, 0x02, 0x03, 0x04 };
		var iv = new byte[] { 0x10, 0x20, 0x30, 0x40 };
		var authTag = new byte[] { 0xAA, 0xBB, 0xCC, 0xDD };

		// Act
		var encrypted = new EncryptedData
		{
			Ciphertext = ciphertext,
			KeyId = "key-123",
			KeyVersion = 1,
			Algorithm = EncryptionAlgorithm.Aes256Gcm,
			Iv = iv,
			AuthTag = authTag,
			TenantId = "tenant-456"
		};

		// Assert
		encrypted.Ciphertext.ShouldBe(ciphertext);
		encrypted.KeyId.ShouldBe("key-123");
		encrypted.KeyVersion.ShouldBe(1);
		encrypted.Algorithm.ShouldBe(EncryptionAlgorithm.Aes256Gcm);
		encrypted.Iv.ShouldBe(iv);
		encrypted.AuthTag.ShouldBe(authTag);
		encrypted.TenantId.ShouldBe("tenant-456");
		encrypted.EncryptedAt.ShouldNotBe(default);
	}

	[Fact]
	public void SetEncryptedAtTimestampAutomatically()
	{
		// Arrange
		var before = DateTimeOffset.UtcNow;

		// Act
		var encrypted = new EncryptedData
		{
			Ciphertext = [0x01],
			KeyId = "key",
			KeyVersion = 1,
			Algorithm = EncryptionAlgorithm.Aes256Gcm,
			Iv = [0x02]
		};

		var after = DateTimeOffset.UtcNow;

		// Assert
		encrypted.EncryptedAt.ShouldBeGreaterThanOrEqualTo(before);
		encrypted.EncryptedAt.ShouldBeLessThanOrEqualTo(after);
	}

	[Fact]
	public void AllowNullAuthTagForNonAuthenticatedModes()
	{
		// Act
		var encrypted = new EncryptedData
		{
			Ciphertext = [0x01],
			KeyId = "key",
			KeyVersion = 1,
			Algorithm = EncryptionAlgorithm.Aes256CbcHmac,
			Iv = [0x02],
			AuthTag = null
		};

		// Assert
		encrypted.AuthTag.ShouldBeNull();
	}

	[Fact]
	public void SupportRecordEquality()
	{
		// Arrange
		var ciphertext = new byte[] { 0x01, 0x02 };
		var iv = new byte[] { 0x10, 0x20 };
		var timestamp = DateTimeOffset.UtcNow;

		var encrypted1 = new EncryptedData
		{
			Ciphertext = ciphertext,
			KeyId = "key-123",
			KeyVersion = 1,
			Algorithm = EncryptionAlgorithm.Aes256Gcm,
			Iv = iv,
			EncryptedAt = timestamp
		};

		var encrypted2 = new EncryptedData
		{
			Ciphertext = ciphertext,
			KeyId = "key-123",
			KeyVersion = 1,
			Algorithm = EncryptionAlgorithm.Aes256Gcm,
			Iv = iv,
			EncryptedAt = timestamp
		};

		// Assert - Records with same values should be equal
		encrypted1.ShouldBe(encrypted2);
	}

	[Theory]
	[InlineData(EncryptionAlgorithm.Aes256Gcm)]
	[InlineData(EncryptionAlgorithm.Aes256CbcHmac)]
	public void SupportAllEncryptionAlgorithms(EncryptionAlgorithm algorithm)
	{
		// Act
		var encrypted = new EncryptedData
		{
			Ciphertext = [0x01],
			KeyId = "key",
			KeyVersion = 1,
			Algorithm = algorithm,
			Iv = [0x02]
		};

		// Assert
		encrypted.Algorithm.ShouldBe(algorithm);
	}
}
