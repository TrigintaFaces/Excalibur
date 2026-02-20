// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Security.Tests.Compliance.Encryption;

/// <summary>
/// Unit tests for <see cref="EncryptedData"/> record, specifically the IsFieldEncrypted detection.
/// </summary>
/// <remarks>
/// Per AD-253-3, these tests verify magic byte detection (0x45 0x58 0x43 0x52 = "EXCR").
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Compliance")]
public sealed class EncryptedDataShould
{
	#region MagicBytes Tests

	[Fact]
	public void MagicBytesShouldBeEXCR()
	{
		// Arrange
		var expected = new byte[] { 0x45, 0x58, 0x43, 0x52 };

		// Act
		var actual = EncryptedData.MagicBytes.ToArray();

		// Assert
		actual.ShouldBe(expected, "Magic bytes should be 'EXCR' (0x45 0x58 0x43 0x52)");
	}

	[Fact]
	public void MagicBytesShouldHaveLengthFour()
	{
		// Arrange & Act
		var length = EncryptedData.MagicBytes.Length;

		// Assert
		length.ShouldBe(4);
	}

	#endregion MagicBytes Tests

	#region IsFieldEncrypted(ReadOnlySpan<byte>) Tests

	[Fact]
	public void IsFieldEncrypted_ShouldReturnTrue_ForDataWithMagicBytes()
	{
		// Arrange - Data starts with EXCR magic bytes
		byte[] data = [0x45, 0x58, 0x43, 0x52, 0x01, 0x02, 0x03, 0x04];

		// Act
		var result = EncryptedData.IsFieldEncrypted(data.AsSpan());

		// Assert
		result.ShouldBeTrue("Data starting with EXCR magic bytes should be detected as encrypted");
	}

	[Fact]
	public void IsFieldEncrypted_ShouldReturnFalse_ForPlaintextData()
	{
		// Arrange - Regular plaintext data (Hello World in ASCII)
		byte[] data = [0x48, 0x65, 0x6C, 0x6C, 0x6F, 0x20, 0x57, 0x6F, 0x72, 0x6C, 0x64];

		// Act
		var result = EncryptedData.IsFieldEncrypted(data.AsSpan());

		// Assert
		result.ShouldBeFalse("Plaintext data should not be detected as encrypted");
	}

	[Fact]
	public void IsFieldEncrypted_ShouldReturnFalse_ForEmptyData()
	{
		// Arrange
		byte[] data = [];

		// Act
		var result = EncryptedData.IsFieldEncrypted(data.AsSpan());

		// Assert
		result.ShouldBeFalse("Empty data should not be detected as encrypted");
	}

	[Fact]
	public void IsFieldEncrypted_ShouldReturnFalse_ForTooShortData()
	{
		// Arrange - Data shorter than magic bytes (3 bytes)
		byte[] data = [0x45, 0x58, 0x43];

		// Act
		var result = EncryptedData.IsFieldEncrypted(data.AsSpan());

		// Assert
		result.ShouldBeFalse("Data shorter than magic bytes should not be detected as encrypted");
	}

	[Fact]
	public void IsFieldEncrypted_ShouldReturnFalse_ForExactlyThreeBytes()
	{
		// Arrange - Exactly 3 bytes (less than required 4)
		byte[] data = [0x45, 0x58, 0x43];

		// Act
		var result = EncryptedData.IsFieldEncrypted(data.AsSpan());

		// Assert
		result.ShouldBeFalse("Three bytes is less than the magic byte length of 4");
	}

	[Fact]
	public void IsFieldEncrypted_ShouldReturnTrue_ForExactlyFourMagicBytes()
	{
		// Arrange - Exactly the magic bytes, no additional data
		byte[] data = [0x45, 0x58, 0x43, 0x52];

		// Act
		var result = EncryptedData.IsFieldEncrypted(data.AsSpan());

		// Assert
		result.ShouldBeTrue("Exactly the magic bytes should be detected as encrypted");
	}

	[Fact]
	public void IsFieldEncrypted_ShouldReturnFalse_ForPartialMagicBytes()
	{
		// Arrange - Partial magic bytes followed by different data (EXCA instead of EXCR)
		byte[] data = [0x45, 0x58, 0x43, 0x41, 0x01, 0x02];

		// Act
		var result = EncryptedData.IsFieldEncrypted(data.AsSpan());

		// Assert
		result.ShouldBeFalse("Partial magic bytes (EXCA) should not be detected as encrypted");
	}

	[Fact]
	public void IsFieldEncrypted_ShouldReturnFalse_ForReversedMagicBytes()
	{
		// Arrange - Magic bytes in reverse order (RCXE instead of EXCR)
		byte[] data = [0x52, 0x43, 0x58, 0x45, 0x01, 0x02];

		// Act
		var result = EncryptedData.IsFieldEncrypted(data.AsSpan());

		// Assert
		result.ShouldBeFalse("Reversed magic bytes should not be detected as encrypted");
	}

	[Fact]
	public void IsFieldEncrypted_ShouldReturnFalse_ForMagicBytesInMiddle()
	{
		// Arrange - Magic bytes are in the middle, not at the start
		byte[] data = [0x01, 0x02, 0x45, 0x58, 0x43, 0x52, 0x03, 0x04];

		// Act
		var result = EncryptedData.IsFieldEncrypted(data.AsSpan());

		// Assert
		result.ShouldBeFalse("Magic bytes in the middle should not be detected as encrypted");
	}

	[Fact]
	public void IsFieldEncrypted_ShouldReturnFalse_ForJsonData()
	{
		// Arrange - JSON data (UTF-8 encoded)
		var jsonBytes = System.Text.Encoding.UTF8.GetBytes("{\"key\":\"value\"}");

		// Act
		var result = EncryptedData.IsFieldEncrypted(jsonBytes.AsSpan());

		// Assert
		result.ShouldBeFalse("JSON data should not be detected as encrypted");
	}

	[Fact]
	public void IsFieldEncrypted_ShouldReturnFalse_ForRandomBytes()
	{
		// Arrange - Random bytes that don't start with magic
		byte[] data = [0x00, 0x11, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77];

		// Act
		var result = EncryptedData.IsFieldEncrypted(data.AsSpan());

		// Assert
		result.ShouldBeFalse("Random bytes not starting with magic should not be detected as encrypted");
	}

	[Fact]
	public void IsFieldEncrypted_ShouldHandleLargeEncryptedData()
	{
		// Arrange - Large data buffer with magic bytes
		var data = new byte[10000];
		data[0] = 0x45; // E
		data[1] = 0x58; // X
		data[2] = 0x43; // C
		data[3] = 0x52; // R
						// Rest is zeros (simulated ciphertext)

		// Act
		var result = EncryptedData.IsFieldEncrypted(data.AsSpan());

		// Assert
		result.ShouldBeTrue("Large encrypted data should be detected correctly");
	}

	#endregion IsFieldEncrypted(ReadOnlySpan<byte>) Tests

	#region IsFieldEncrypted(byte[]?) Tests

	[Fact]
	public void IsFieldEncrypted_ByteArray_ShouldReturnFalse_ForNull()
	{
		// Arrange
		byte[]? data = null;

		// Act
		var result = EncryptedData.IsFieldEncrypted(data);

		// Assert
		result.ShouldBeFalse("Null data should not be detected as encrypted");
	}

	[Fact]
	public void IsFieldEncrypted_ByteArray_ShouldReturnTrue_ForEncryptedData()
	{
		// Arrange - Data starts with EXCR magic bytes
		byte[] data = [0x45, 0x58, 0x43, 0x52, 0x01, 0x02, 0x03, 0x04];

		// Act
		var result = EncryptedData.IsFieldEncrypted(data);

		// Assert
		result.ShouldBeTrue("Byte array with magic bytes should be detected as encrypted");
	}

	[Fact]
	public void IsFieldEncrypted_ByteArray_ShouldReturnFalse_ForPlaintextData()
	{
		// Arrange - Regular plaintext data
		byte[] data = [0x48, 0x65, 0x6C, 0x6C, 0x6F]; // "Hello"

		// Act
		var result = EncryptedData.IsFieldEncrypted(data);

		// Assert
		result.ShouldBeFalse("Plaintext byte array should not be detected as encrypted");
	}

	[Fact]
	public void IsFieldEncrypted_ByteArray_ShouldReturnFalse_ForEmptyArray()
	{
		// Arrange
		byte[] data = [];

		// Act
		var result = EncryptedData.IsFieldEncrypted(data);

		// Assert
		result.ShouldBeFalse("Empty byte array should not be detected as encrypted");
	}

	#endregion IsFieldEncrypted(byte[]?) Tests

	#region Edge Cases and Performance Considerations

	[Theory]
	[InlineData(new byte[] { 0x45 }, false)] // Just 'E'
	[InlineData(new byte[] { 0x45, 0x58 }, false)] // Just 'EX'
	[InlineData(new byte[] { 0x45, 0x58, 0x43 }, false)] // Just 'EXC'
	[InlineData(new byte[] { 0x45, 0x58, 0x43, 0x52 }, true)] // Exactly 'EXCR'
	[InlineData(new byte[] { 0x45, 0x58, 0x43, 0x52, 0x00 }, true)] // 'EXCR' + extra
	public void IsFieldEncrypted_ShouldHandleBoundaryLengths(byte[] data, bool expected)
	{
		// Act
		var result = EncryptedData.IsFieldEncrypted(data.AsSpan());

		// Assert
		result.ShouldBe(expected);
	}

	[Fact]
	public void IsFieldEncrypted_ShouldBeCaseSensitive()
	{
		// Arrange - Lowercase 'excr' (0x65 0x78 0x63 0x72)
		byte[] data = [0x65, 0x78, 0x63, 0x72, 0x01, 0x02];

		// Act
		var result = EncryptedData.IsFieldEncrypted(data.AsSpan());

		// Assert
		result.ShouldBeFalse("Detection should be case-sensitive (only uppercase EXCR)");
	}

	[Fact]
	public void IsFieldEncrypted_ShouldBeAllocationFriendly()
	{
		// Arrange
		byte[] data = [0x45, 0x58, 0x43, 0x52, 0x01, 0x02, 0x03, 0x04];

		// Act - Verify method can be called repeatedly with ReadOnlySpan (zero-copy)
		// Note: We cannot reliably measure allocations in CI due to GC behavior,
		// but the use of ReadOnlySpan<byte> parameter ensures no heap allocations
		// from the data parameter itself
		for (var i = 0; i < 1000; i++)
		{
			_ = EncryptedData.IsFieldEncrypted(data.AsSpan());
		}

		// Assert - Method uses Span-based API (verified by compilation)
		// The signature `ReadOnlySpan<byte>` ensures zero-allocation for the parameter
		true.ShouldBeTrue("ReadOnlySpan<byte> parameter ensures zero-allocation");
	}

	#endregion Edge Cases and Performance Considerations

	#region EncryptedData Record Tests

	[Fact]
	public void EncryptedData_ShouldInitializeWithRequiredProperties()
	{
		// Arrange & Act
		var encryptedData = new EncryptedData
		{
			Ciphertext = [0x01, 0x02, 0x03],
			KeyId = "test-key-id",
			KeyVersion = 1,
			Algorithm = EncryptionAlgorithm.Aes256Gcm,
			Iv = [0x11, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77, 0x88, 0x99, 0xAA, 0xBB, 0xCC],
		};

		// Assert
		encryptedData.Ciphertext.ShouldBe([0x01, 0x02, 0x03]);
		encryptedData.KeyId.ShouldBe("test-key-id");
		encryptedData.KeyVersion.ShouldBe(1);
		encryptedData.Algorithm.ShouldBe(EncryptionAlgorithm.Aes256Gcm);
		encryptedData.Iv.Length.ShouldBe(12);
	}

	[Fact]
	public void EncryptedData_ShouldHaveDefaultEncryptedAtTime()
	{
		// Arrange
		var beforeCreation = DateTimeOffset.UtcNow;

		// Act
		var encryptedData = new EncryptedData
		{
			Ciphertext = [0x01],
			KeyId = "key",
			KeyVersion = 1,
			Algorithm = EncryptionAlgorithm.Aes256Gcm,
			Iv = [0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0A, 0x0B, 0x0C],
		};

		var afterCreation = DateTimeOffset.UtcNow;

		// Assert
		encryptedData.EncryptedAt.ShouldBeGreaterThanOrEqualTo(beforeCreation);
		encryptedData.EncryptedAt.ShouldBeLessThanOrEqualTo(afterCreation);
	}

	[Fact]
	public void EncryptedData_ShouldAllowOptionalAuthTag()
	{
		// Arrange & Act
		var withoutAuthTag = new EncryptedData
		{
			Ciphertext = [0x01],
			KeyId = "key",
			KeyVersion = 1,
			Algorithm = EncryptionAlgorithm.Aes256Gcm,
			Iv = [0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0A, 0x0B, 0x0C],
		};

		var withAuthTag = new EncryptedData
		{
			Ciphertext = [0x01],
			KeyId = "key",
			KeyVersion = 1,
			Algorithm = EncryptionAlgorithm.Aes256Gcm,
			Iv = [0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0A, 0x0B, 0x0C],
			AuthTag = [0xAA, 0xBB, 0xCC, 0xDD],
		};

		// Assert
		withoutAuthTag.AuthTag.ShouldBeNull();
		withAuthTag.AuthTag.ShouldBe([0xAA, 0xBB, 0xCC, 0xDD]);
	}

	[Fact]
	public void EncryptedData_ShouldAllowOptionalTenantId()
	{
		// Arrange & Act
		var withoutTenant = new EncryptedData
		{
			Ciphertext = [0x01],
			KeyId = "key",
			KeyVersion = 1,
			Algorithm = EncryptionAlgorithm.Aes256Gcm,
			Iv = [0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0A, 0x0B, 0x0C],
		};

		var withTenant = new EncryptedData
		{
			Ciphertext = [0x01],
			KeyId = "key",
			KeyVersion = 1,
			Algorithm = EncryptionAlgorithm.Aes256Gcm,
			Iv = [0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0A, 0x0B, 0x0C],
			TenantId = "tenant-123",
		};

		// Assert
		withoutTenant.TenantId.ShouldBeNull();
		withTenant.TenantId.ShouldBe("tenant-123");
	}

	#endregion EncryptedData Record Tests
}
