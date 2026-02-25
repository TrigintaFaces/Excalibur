// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Security.Tests.Compliance.Abstractions.Encryption;

/// <summary>
/// Unit tests for <see cref="KeyRotationResult"/> record.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
[Trait("Feature", "Encryption")]
public sealed class KeyRotationResultShould : UnitTestBase
{
	[Fact]
	public void CreateSuccessfulResultWithFactoryMethod()
	{
		// Arrange
		var newKey = new KeyMetadata
		{
			KeyId = "key-new",
			Version = 2,
			Status = KeyStatus.Active,
			Algorithm = EncryptionAlgorithm.Aes256Gcm,
			CreatedAt = DateTimeOffset.UtcNow
		};
		var previousKey = new KeyMetadata
		{
			KeyId = "key-new",
			Version = 1,
			Status = KeyStatus.DecryptOnly,
			Algorithm = EncryptionAlgorithm.Aes256Gcm,
			CreatedAt = DateTimeOffset.UtcNow.AddYears(-1)
		};

		// Act
		var result = KeyRotationResult.Succeeded(newKey, previousKey);

		// Assert
		result.Success.ShouldBeTrue();
		result.NewKey.ShouldBe(newKey);
		result.PreviousKey.ShouldBe(previousKey);
		result.ErrorMessage.ShouldBeNull();
	}

	[Fact]
	public void CreateSuccessfulResultWithoutPreviousKey()
	{
		// Arrange - first key, no previous
		var newKey = new KeyMetadata
		{
			KeyId = "key-first",
			Version = 1,
			Status = KeyStatus.Active,
			Algorithm = EncryptionAlgorithm.Aes256Gcm,
			CreatedAt = DateTimeOffset.UtcNow
		};

		// Act
		var result = KeyRotationResult.Succeeded(newKey);

		// Assert
		result.Success.ShouldBeTrue();
		result.NewKey.ShouldBe(newKey);
		result.PreviousKey.ShouldBeNull();
		result.ErrorMessage.ShouldBeNull();
	}

	[Fact]
	public void CreateFailedResultWithFactoryMethod()
	{
		// Arrange
		var errorMessage = "Key rotation failed: provider unavailable";

		// Act
		var result = KeyRotationResult.Failed(errorMessage);

		// Assert
		result.Success.ShouldBeFalse();
		result.ErrorMessage.ShouldBe(errorMessage);
		result.NewKey.ShouldBeNull();
		result.PreviousKey.ShouldBeNull();
	}

	[Fact]
	public void HaveDefaultTimestampOnCreation()
	{
		// Arrange
		var before = DateTimeOffset.UtcNow;

		// Act
		var result = KeyRotationResult.Failed("test");

		var after = DateTimeOffset.UtcNow;

		// Assert
		result.RotatedAt.ShouldBeGreaterThanOrEqualTo(before);
		result.RotatedAt.ShouldBeLessThanOrEqualTo(after);
	}

	[Fact]
	public void SupportRecordEquality()
	{
		// Arrange
		var newKey = new KeyMetadata
		{
			KeyId = "key-eq",
			Version = 1,
			Status = KeyStatus.Active,
			Algorithm = EncryptionAlgorithm.Aes256Gcm,
			CreatedAt = DateTimeOffset.UtcNow
		};
		var rotatedAt = DateTimeOffset.UtcNow;

		var result1 = new KeyRotationResult
		{
			Success = true,
			NewKey = newKey,
			RotatedAt = rotatedAt
		};

		var result2 = new KeyRotationResult
		{
			Success = true,
			NewKey = newKey,
			RotatedAt = rotatedAt
		};

		// Assert
		result1.ShouldBe(result2);
	}

	[Fact]
	public void CreateResultWithCustomTimestamp()
	{
		// Arrange
		var customTimestamp = DateTimeOffset.UtcNow.AddDays(-1);

		// Act
		var result = new KeyRotationResult
		{
			Success = true,
			RotatedAt = customTimestamp
		};

		// Assert
		result.RotatedAt.ShouldBe(customTimestamp);
	}
}
