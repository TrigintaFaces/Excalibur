// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Security.Tests.Compliance.Encryption;

/// <summary>
/// Unit tests for <see cref="ReEncryptionResult"/> class.
/// </summary>
/// <remarks>
/// Per AD-256-1, these tests verify the re-encryption result handling.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Compliance")]
public sealed class ReEncryptionResultShould
{
	#region Succeeded Factory Method Tests

	[Fact]
	public void CreateSucceededResult_WithCorrectSuccessFlag()
	{
		// Arrange & Act
		var result = ReEncryptionResult.Succeeded(
			"source-provider",
			"target-provider",
			fieldsReEncrypted: 3,
			duration: TimeSpan.FromMilliseconds(150));

		// Assert
		result.Success.ShouldBeTrue();
	}

	[Fact]
	public void CreateSucceededResult_WithSourceProviderId()
	{
		// Arrange & Act
		var result = ReEncryptionResult.Succeeded(
			"aws-kms-2023",
			"aws-kms-2024",
			fieldsReEncrypted: 2,
			duration: TimeSpan.FromMilliseconds(100));

		// Assert
		result.SourceProviderId.ShouldBe("aws-kms-2023");
	}

	[Fact]
	public void CreateSucceededResult_WithTargetProviderId()
	{
		// Arrange & Act
		var result = ReEncryptionResult.Succeeded(
			"old-provider",
			"new-provider",
			fieldsReEncrypted: 5,
			duration: TimeSpan.FromMilliseconds(200));

		// Assert
		result.TargetProviderId.ShouldBe("new-provider");
	}

	[Fact]
	public void CreateSucceededResult_WithFieldsReEncryptedCount()
	{
		// Arrange & Act
		var result = ReEncryptionResult.Succeeded(
			"source",
			"target",
			fieldsReEncrypted: 7,
			duration: TimeSpan.FromMilliseconds(50));

		// Assert
		result.FieldsReEncrypted.ShouldBe(7);
	}

	[Fact]
	public void CreateSucceededResult_WithDuration()
	{
		// Arrange
		var duration = TimeSpan.FromMilliseconds(250);

		// Act
		var result = ReEncryptionResult.Succeeded(
			"source",
			"target",
			fieldsReEncrypted: 1,
			duration: duration);

		// Assert
		result.Duration.ShouldBe(duration);
	}

	[Fact]
	public void CreateSucceededResult_WithNullErrorMessage()
	{
		// Arrange & Act
		var result = ReEncryptionResult.Succeeded(
			"source",
			"target",
			fieldsReEncrypted: 1,
			duration: TimeSpan.FromMilliseconds(100));

		// Assert
		result.ErrorMessage.ShouldBeNull();
	}

	[Fact]
	public void CreateSucceededResult_WithNullException()
	{
		// Arrange & Act
		var result = ReEncryptionResult.Succeeded(
			"source",
			"target",
			fieldsReEncrypted: 1,
			duration: TimeSpan.FromMilliseconds(100));

		// Assert
		result.Exception.ShouldBeNull();
	}

	#endregion Succeeded Factory Method Tests

	#region Failed Factory Method Tests

	[Fact]
	public void CreateFailedResult_WithCorrectSuccessFlag()
	{
		// Arrange & Act
		var result = ReEncryptionResult.Failed("Re-encryption failed");

		// Assert
		result.Success.ShouldBeFalse();
	}

	[Fact]
	public void CreateFailedResult_WithErrorMessage()
	{
		// Arrange & Act
		var result = ReEncryptionResult.Failed("Provider not found");

		// Assert
		result.ErrorMessage.ShouldBe("Provider not found");
	}

	[Fact]
	public void CreateFailedResult_WithException()
	{
		// Arrange
		var exception = new InvalidOperationException("Test exception");

		// Act
		var result = ReEncryptionResult.Failed("Operation failed", exception);

		// Assert
		result.Exception.ShouldBe(exception);
	}

	[Fact]
	public void CreateFailedResult_WithNullException_WhenNotProvided()
	{
		// Arrange & Act
		var result = ReEncryptionResult.Failed("Error occurred");

		// Assert
		result.Exception.ShouldBeNull();
	}

	[Fact]
	public void CreateFailedResult_WithZeroFieldsReEncrypted()
	{
		// Arrange & Act
		var result = ReEncryptionResult.Failed("Decryption failed");

		// Assert
		result.FieldsReEncrypted.ShouldBe(0);
	}

	[Fact]
	public void CreateFailedResult_WithZeroDuration()
	{
		// Arrange & Act
		var result = ReEncryptionResult.Failed("Error");

		// Assert
		result.Duration.ShouldBe(TimeSpan.Zero);
	}

	[Fact]
	public void CreateFailedResult_WithNullProviderIds()
	{
		// Arrange & Act
		var result = ReEncryptionResult.Failed("Provider lookup failed");

		// Assert
		result.SourceProviderId.ShouldBeNull();
		result.TargetProviderId.ShouldBeNull();
	}

	#endregion Failed Factory Method Tests

	#region Semantic Tests

	[Fact]
	public void SupportKeyRotationSuccess()
	{
		// Per AD-256-1: Key rotation success scenario
		var result = ReEncryptionResult.Succeeded(
			"aws-kms-key-2023",
			"aws-kms-key-2024",
			fieldsReEncrypted: 5,
			duration: TimeSpan.FromSeconds(1));

		result.Success.ShouldBeTrue();
		result.SourceProviderId.ShouldNotBe(result.TargetProviderId);
		result.FieldsReEncrypted.ShouldBeGreaterThan(0);
	}

	[Fact]
	public void SupportDecryptionFailure()
	{
		// Per AD-256-1: Decryption failure with exception
		var exception = new InvalidOperationException("Invalid key");

		var result = ReEncryptionResult.Failed(
			"Failed to decrypt: Invalid key",
			exception);

		result.Success.ShouldBeFalse();
		result.ErrorMessage.ShouldContain("Invalid key");
		result.Exception.ShouldBe(exception);
	}

	[Fact]
	public void SupportZeroFieldsReEncrypted_WhenNoEncryptedFields()
	{
		// Entity has no encrypted fields but operation succeeds
		var result = ReEncryptionResult.Succeeded(
			"source",
			"target",
			fieldsReEncrypted: 0,
			duration: TimeSpan.FromMilliseconds(10));

		result.Success.ShouldBeTrue();
		result.FieldsReEncrypted.ShouldBe(0);
	}

	#endregion Semantic Tests
}
