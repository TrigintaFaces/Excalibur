// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Security.Tests.Compliance.Encryption;

/// <summary>
/// Unit tests for <see cref="ReEncryptionResult{T}"/> class.
/// </summary>
/// <remarks>
/// Per AD-256-1, these tests verify the generic re-encryption result with entity handling.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Compliance")]
public sealed class ReEncryptionResultGenericShould
{
	#region Test Entity

	private sealed class TestEntity
	{
		public string Id { get; set; } = string.Empty;
		public string Name { get; set; } = string.Empty;
		public string EncryptedData { get; set; } = string.Empty;
	}

	#endregion Test Entity

	#region Succeeded Factory Method Tests

	[Fact]
	public void CreateSucceededResult_WithCorrectSuccessFlag()
	{
		// Arrange
		var entity = new TestEntity { Id = "1", Name = "Test" };

		// Act
		var result = ReEncryptionResult<TestEntity>.Succeeded(
			entity,
			"source-provider",
			"target-provider",
			fieldsReEncrypted: 1,
			duration: TimeSpan.FromMilliseconds(100));

		// Assert
		result.Success.ShouldBeTrue();
	}

	[Fact]
	public void CreateSucceededResult_WithEntity()
	{
		// Arrange
		var entity = new TestEntity { Id = "entity-123", Name = "Test Entity" };

		// Act
		var result = ReEncryptionResult<TestEntity>.Succeeded(
			entity,
			"source",
			"target",
			fieldsReEncrypted: 1,
			duration: TimeSpan.FromMilliseconds(50));

		// Assert
		result.Entity.ShouldBe(entity);
		result.Entity.Id.ShouldBe("entity-123");
		result.Entity.Name.ShouldBe("Test Entity");
	}

	[Fact]
	public void CreateSucceededResult_WithSourceProviderId()
	{
		// Arrange
		var entity = new TestEntity { Id = "1" };

		// Act
		var result = ReEncryptionResult<TestEntity>.Succeeded(
			entity,
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
		// Arrange
		var entity = new TestEntity { Id = "1" };

		// Act
		var result = ReEncryptionResult<TestEntity>.Succeeded(
			entity,
			"old-provider",
			"new-provider",
			fieldsReEncrypted: 3,
			duration: TimeSpan.FromMilliseconds(150));

		// Assert
		result.TargetProviderId.ShouldBe("new-provider");
	}

	[Fact]
	public void CreateSucceededResult_WithFieldsReEncryptedCount()
	{
		// Arrange
		var entity = new TestEntity { Id = "1" };

		// Act
		var result = ReEncryptionResult<TestEntity>.Succeeded(
			entity,
			"source",
			"target",
			fieldsReEncrypted: 5,
			duration: TimeSpan.FromMilliseconds(200));

		// Assert
		result.FieldsReEncrypted.ShouldBe(5);
	}

	[Fact]
	public void CreateSucceededResult_WithDuration()
	{
		// Arrange
		var entity = new TestEntity { Id = "1" };
		var duration = TimeSpan.FromMilliseconds(300);

		// Act
		var result = ReEncryptionResult<TestEntity>.Succeeded(
			entity,
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
		// Arrange
		var entity = new TestEntity { Id = "1" };

		// Act
		var result = ReEncryptionResult<TestEntity>.Succeeded(
			entity,
			"source",
			"target",
			fieldsReEncrypted: 1,
			duration: TimeSpan.FromMilliseconds(50));

		// Assert
		result.ErrorMessage.ShouldBeNull();
	}

	[Fact]
	public void CreateSucceededResult_WithNullException()
	{
		// Arrange
		var entity = new TestEntity { Id = "1" };

		// Act
		var result = ReEncryptionResult<TestEntity>.Succeeded(
			entity,
			"source",
			"target",
			fieldsReEncrypted: 1,
			duration: TimeSpan.FromMilliseconds(50));

		// Assert
		result.Exception.ShouldBeNull();
	}

	#endregion Succeeded Factory Method Tests

	#region Failed Factory Method Tests

	[Fact]
	public void CreateFailedResult_WithCorrectSuccessFlag()
	{
		// Arrange
		var entity = new TestEntity { Id = "1" };

		// Act
		var result = ReEncryptionResult<TestEntity>.Failed(entity, "Re-encryption failed");

		// Assert
		result.Success.ShouldBeFalse();
	}

	[Fact]
	public void CreateFailedResult_WithOriginalEntity()
	{
		// Arrange
		var entity = new TestEntity { Id = "failed-entity", Name = "Original" };

		// Act
		var result = ReEncryptionResult<TestEntity>.Failed(entity, "Error occurred");

		// Assert
		result.Entity.ShouldBe(entity);
		result.Entity.Id.ShouldBe("failed-entity");
		result.Entity.Name.ShouldBe("Original");
	}

	[Fact]
	public void CreateFailedResult_WithErrorMessage()
	{
		// Arrange
		var entity = new TestEntity { Id = "1" };

		// Act
		var result = ReEncryptionResult<TestEntity>.Failed(entity, "Provider not found");

		// Assert
		result.ErrorMessage.ShouldBe("Provider not found");
	}

	[Fact]
	public void CreateFailedResult_WithException()
	{
		// Arrange
		var entity = new TestEntity { Id = "1" };
		var exception = new InvalidOperationException("Decryption failed");

		// Act
		var result = ReEncryptionResult<TestEntity>.Failed(entity, "Error", exception);

		// Assert
		result.Exception.ShouldBe(exception);
	}

	[Fact]
	public void CreateFailedResult_WithNullException_WhenNotProvided()
	{
		// Arrange
		var entity = new TestEntity { Id = "1" };

		// Act
		var result = ReEncryptionResult<TestEntity>.Failed(entity, "Error");

		// Assert
		result.Exception.ShouldBeNull();
	}

	[Fact]
	public void CreateFailedResult_WithZeroFieldsReEncrypted()
	{
		// Arrange
		var entity = new TestEntity { Id = "1" };

		// Act
		var result = ReEncryptionResult<TestEntity>.Failed(entity, "Error");

		// Assert
		result.FieldsReEncrypted.ShouldBe(0);
	}

	[Fact]
	public void CreateFailedResult_WithZeroDuration()
	{
		// Arrange
		var entity = new TestEntity { Id = "1" };

		// Act
		var result = ReEncryptionResult<TestEntity>.Failed(entity, "Error");

		// Assert
		result.Duration.ShouldBe(TimeSpan.Zero);
	}

	[Fact]
	public void CreateFailedResult_WithNullProviderIds()
	{
		// Arrange
		var entity = new TestEntity { Id = "1" };

		// Act
		var result = ReEncryptionResult<TestEntity>.Failed(entity, "Error");

		// Assert
		result.SourceProviderId.ShouldBeNull();
		result.TargetProviderId.ShouldBeNull();
	}

	#endregion Failed Factory Method Tests

	#region Semantic Tests

	[Fact]
	public void SupportBatchProcessingSuccess()
	{
		// Per AD-256-1: Batch processing returns entity with result
		var entity = new TestEntity { Id = "batch-1", Name = "Batch Item" };

		var result = ReEncryptionResult<TestEntity>.Succeeded(
			entity,
			"old-key",
			"new-key",
			fieldsReEncrypted: 2,
			duration: TimeSpan.FromMilliseconds(75));

		result.Success.ShouldBeTrue();
		_ = result.Entity.ShouldNotBeNull();
		result.FieldsReEncrypted.ShouldBeGreaterThan(0);
	}

	[Fact]
	public void SupportContinueOnError_PreservingOriginalEntity()
	{
		// Per AD-256-1: When ContinueOnError=true, failed items retain original entity
		var entity = new TestEntity
		{
			Id = "error-item",
			Name = "Original Name",
			EncryptedData = "EXCR:encrypted:data"
		};

		var result = ReEncryptionResult<TestEntity>.Failed(
			entity,
			"Decryption key expired",
			new InvalidOperationException("Key expired"));

		result.Success.ShouldBeFalse();
		result.Entity.ShouldBe(entity);
		result.Entity.EncryptedData.ShouldBe("EXCR:encrypted:data"); // Still encrypted
	}

	[Fact]
	public void SupportMultipleFieldsReEncryption()
	{
		// Entity with multiple encrypted fields
		var entity = new TestEntity { Id = "multi-field" };

		var result = ReEncryptionResult<TestEntity>.Succeeded(
			entity,
			"source",
			"target",
			fieldsReEncrypted: 10,
			duration: TimeSpan.FromSeconds(2));

		result.Success.ShouldBeTrue();
		result.FieldsReEncrypted.ShouldBe(10);
	}

	[Fact]
	public void SupportZeroFieldsReEncrypted_WhenNoEncryptedFields()
	{
		// Entity has no encrypted fields but operation succeeds
		var entity = new TestEntity { Id = "no-encrypted-fields" };

		var result = ReEncryptionResult<TestEntity>.Succeeded(
			entity,
			"source",
			"target",
			fieldsReEncrypted: 0,
			duration: TimeSpan.FromMilliseconds(5));

		result.Success.ShouldBeTrue();
		result.FieldsReEncrypted.ShouldBe(0);
		result.Entity.ShouldBe(entity);
	}

	#endregion Semantic Tests
}
