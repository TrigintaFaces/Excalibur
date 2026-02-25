// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Security.Tests.Compliance.Encryption;

/// <summary>
/// Unit tests for <see cref="ReEncryptionService"/> estimation methods.
/// </summary>
/// <remarks>
/// Per Sprint 397, these tests verify the estimation functionality for re-encryption planning.
/// Tests cover both the parameterless <see cref="IReEncryptionService.EstimateAsync"/> method
/// and the typed <see cref="ReEncryptionService.EstimateForTypeAsync{T}"/> method.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Compliance")]
public sealed class ReEncryptionServiceEstimationShould
{
	private readonly IEncryptionProviderRegistry _registry;
	private readonly ReEncryptionService _sut;

	public ReEncryptionServiceEstimationShould()
	{
		_registry = A.Fake<IEncryptionProviderRegistry>();
		_sut = new ReEncryptionService(
			_registry,
			NullLogger<ReEncryptionService>.Instance);
	}

	#region EstimateAsync (parameterless) Tests

	[Fact]
	public async Task EstimateAsync_ThrowArgumentNullException_WhenOptionsIsNull()
	{
		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(() =>
			_sut.EstimateAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task EstimateAsync_ReturnGuidanceEstimate_WhenNoEntityTypeSpecified()
	{
		// Arrange
		var options = new ReEncryptionOptions
		{
			SourceProviderId = "source",
			TargetProviderId = "target",
		};

		// Act
		var result = await _sut.EstimateAsync(options, CancellationToken.None);

		// Assert
		_ = result.ShouldNotBeNull();
		result.EstimatedItemCount.ShouldBe(0);
		result.EstimatedFieldsPerItem.ShouldBe(0);
		result.EstimatedDuration.ShouldBe(TimeSpan.Zero);
		result.IsSampled.ShouldBeFalse();
	}

	[Fact]
	public async Task EstimateAsync_ReturnWarnings_GuidingUserToTypedOverload()
	{
		// Arrange
		var options = new ReEncryptionOptions();

		// Act
		var result = await _sut.EstimateAsync(options, CancellationToken.None);

		// Assert
		_ = result.Warnings.ShouldNotBeNull();
		result.Warnings.Count.ShouldBeGreaterThan(0);
		result.Warnings.ShouldContain(w => w.Contains("EstimateForTypeAsync"));
	}

	[Fact]
	public async Task EstimateAsync_ReturnWarningAboutEntityType_WhenNotSpecified()
	{
		// Arrange
		var options = new ReEncryptionOptions();

		// Act
		var result = await _sut.EstimateAsync(options, CancellationToken.None);

		// Assert
		result.Warnings.ShouldContain(w => w.Contains("entity type", StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	public async Task EstimateAsync_ReturnWarningAboutDataSource_WhenNotSpecified()
	{
		// Arrange
		var options = new ReEncryptionOptions();

		// Act
		var result = await _sut.EstimateAsync(options, CancellationToken.None);

		// Assert
		result.Warnings.ShouldContain(w => w.Contains("data source", StringComparison.OrdinalIgnoreCase));
	}

	#endregion

	#region EstimateForTypeAsync Tests

	[Fact]
	public async Task EstimateForTypeAsync_ThrowArgumentOutOfRangeException_WhenItemCountIsNegative()
	{
		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentOutOfRangeException>(() =>
			_sut.EstimateForTypeAsync<TestEntityWithEncryptedFields>(-1, CancellationToken.None));
	}

	[Fact]
	public async Task EstimateForTypeAsync_ThrowOperationCanceledException_WhenCancelled()
	{
		// Arrange
		using var cts = new CancellationTokenSource();
		cts.Cancel();

		// Act & Assert
		_ = await Should.ThrowAsync<OperationCanceledException>(() =>
			_sut.EstimateForTypeAsync<TestEntityWithEncryptedFields>(100, cts.Token));
	}

	[Fact]
	public async Task EstimateForTypeAsync_ReturnZeroFieldsWarning_WhenEntityHasNoEncryptedFields()
	{
		// Act
		var result = await _sut.EstimateForTypeAsync<TestEntityWithNoEncryptedFields>(
			100, CancellationToken.None);

		// Assert
		result.EstimatedFieldsPerItem.ShouldBe(0);
		result.EstimatedDuration.ShouldBe(TimeSpan.Zero);
		_ = result.Warnings.ShouldNotBeNull();
		result.Warnings.Count.ShouldBe(1);
		result.Warnings[0].ShouldContain("No encrypted fields");
	}

	[Fact]
	public async Task EstimateForTypeAsync_DetectEncryptedFields_OnEntityType()
	{
		// Act
		var result = await _sut.EstimateForTypeAsync<TestEntityWithEncryptedFields>(
			100, CancellationToken.None);

		// Assert
		result.EstimatedFieldsPerItem.ShouldBe(2); // Two [EncryptedField] properties
	}

	[Fact]
	public async Task EstimateForTypeAsync_ReturnCorrectItemCount()
	{
		// Arrange
		const long itemCount = 1000;

		// Act
		var result = await _sut.EstimateForTypeAsync<TestEntityWithEncryptedFields>(
			itemCount, CancellationToken.None);

		// Assert
		result.EstimatedItemCount.ShouldBe(itemCount);
	}

	[Fact]
	public async Task EstimateForTypeAsync_ReturnZeroForEmptyDataset()
	{
		// Act
		var result = await _sut.EstimateForTypeAsync<TestEntityWithEncryptedFields>(
			0, CancellationToken.None);

		// Assert
		result.EstimatedItemCount.ShouldBe(0);
		result.EstimatedFieldsPerItem.ShouldBe(2);
		result.EstimatedDuration.ShouldBe(TimeSpan.Zero);
	}

	[Fact]
	public async Task EstimateForTypeAsync_CalculateDuration_UsingFormula()
	{
		// Arrange
		// Formula: itemCount × fieldsPerItem × 5ms × 1.2 (I/O overhead)
		const long itemCount = 1000;
		const int expectedFields = 2;
		const double msPerField = 5.0;
		const double ioOverhead = 1.2;
		var expectedMs = itemCount * expectedFields * msPerField * ioOverhead;

		// Act
		var result = await _sut.EstimateForTypeAsync<TestEntityWithEncryptedFields>(
			itemCount, CancellationToken.None);

		// Assert
		result.EstimatedDuration.TotalMilliseconds.ShouldBe(expectedMs);
	}

	[Fact]
	public async Task EstimateForTypeAsync_ReturnIsSampledFalse_ForDirectCount()
	{
		// Act
		var result = await _sut.EstimateForTypeAsync<TestEntityWithEncryptedFields>(
			100, CancellationToken.None);

		// Assert
		result.IsSampled.ShouldBeFalse();
	}

	[Fact]
	public async Task EstimateForTypeAsync_ReturnEmptyWarnings_WhenSuccessful()
	{
		// Act
		var result = await _sut.EstimateForTypeAsync<TestEntityWithEncryptedFields>(
			100, CancellationToken.None);

		// Assert
		_ = result.Warnings.ShouldNotBeNull();
		result.Warnings.ShouldBeEmpty();
	}

	[Theory]
	[InlineData(1)]
	[InlineData(100)]
	[InlineData(10_000)]
	[InlineData(1_000_000)]
	public async Task EstimateForTypeAsync_ScaleLinearlyWithItemCount(long itemCount)
	{
		// Arrange
		const int expectedFields = 2;
		const double msPerField = 5.0;
		const double ioOverhead = 1.2;
		var expectedMs = itemCount * expectedFields * msPerField * ioOverhead;

		// Act
		var result = await _sut.EstimateForTypeAsync<TestEntityWithEncryptedFields>(
			itemCount, CancellationToken.None);

		// Assert
		result.EstimatedDuration.TotalMilliseconds.ShouldBe(expectedMs);
	}

	[Fact]
	public async Task EstimateForTypeAsync_IncreaseWithMoreEncryptedFields()
	{
		// Act
		var result2Fields = await _sut.EstimateForTypeAsync<TestEntityWithEncryptedFields>(
			1000, CancellationToken.None);
		var result3Fields = await _sut.EstimateForTypeAsync<TestEntityWithThreeEncryptedFields>(
			1000, CancellationToken.None);

		// Assert
		result3Fields.EstimatedFieldsPerItem.ShouldBe(3);
		result3Fields.EstimatedDuration.ShouldBeGreaterThan(result2Fields.EstimatedDuration);
	}

	[Fact]
	public async Task EstimateForTypeAsync_IgnoreNonByteArrayProperties()
	{
		// Act
		var result = await _sut.EstimateForTypeAsync<TestEntityWithMixedProperties>(
			100, CancellationToken.None);

		// Assert - Only byte[] properties with [EncryptedField] are counted
		result.EstimatedFieldsPerItem.ShouldBe(1);
	}

	[Fact]
	public async Task EstimateForTypeAsync_IgnoreReadOnlyEncryptedProperties()
	{
		// Act
		var result = await _sut.EstimateForTypeAsync<TestEntityWithReadOnlyEncryptedField>(
			100, CancellationToken.None);

		// Assert - Read-only properties cannot be re-encrypted
		result.EstimatedFieldsPerItem.ShouldBe(0);
		result.Warnings.ShouldContain(w => w.Contains("No encrypted fields"));
	}

	#endregion

	#region Edge Case Tests

	[Fact]
	public async Task EstimateForTypeAsync_HandleLargeItemCount()
	{
		// Test with a reasonably large item count that won't overflow
		// 1 billion items × 2 fields × 5ms × 1.2 = 12 billion ms = 138 days
		// This is within TimeSpan.MaxValue
		const long largeItemCount = 1_000_000_000;

		// Act
		var result = await _sut.EstimateForTypeAsync<TestEntityWithEncryptedFields>(
			largeItemCount,
			CancellationToken.None);

		// Assert
		result.EstimatedDuration.ShouldBeGreaterThan(TimeSpan.Zero);
		result.EstimatedItemCount.ShouldBe(largeItemCount);
	}

	[Fact]
	public async Task EstimateForTypeAsync_ThrowOverflow_ForExtremeValues()
	{
		// Very large values can overflow TimeSpan - this documents the behavior
		// long.MaxValue / 100 is still ~9.2e16, which × 2 × 5 × 1.2 overflows
		const long extremeItemCount = long.MaxValue / 100;

		// Act & Assert - documents current behavior
		_ = await Should.ThrowAsync<OverflowException>(() =>
			_sut.EstimateForTypeAsync<TestEntityWithEncryptedFields>(
				extremeItemCount, CancellationToken.None));
	}

	#endregion

	#region Test Entities

	/// <summary>
	/// Test entity with no encrypted fields.
	/// </summary>
	private sealed class TestEntityWithNoEncryptedFields
	{
		public string Id { get; set; } = string.Empty;
		public string Name { get; set; } = string.Empty;
		public byte[]? Data { get; set; } // Not marked with [EncryptedField]
	}

	/// <summary>
	/// Test entity with two encrypted byte[] fields.
	/// </summary>
	private sealed class TestEntityWithEncryptedFields
	{
		public string Id { get; set; } = string.Empty;

		[EncryptedField]
		public byte[]? SensitiveData { get; set; }

		[EncryptedField]
		public byte[]? ProtectedField { get; set; }

		public byte[]? UnencryptedData { get; set; }
	}

	/// <summary>
	/// Test entity with three encrypted byte[] fields.
	/// </summary>
	private sealed class TestEntityWithThreeEncryptedFields
	{
		public string Id { get; set; } = string.Empty;

		[EncryptedField]
		public byte[]? Field1 { get; set; }

		[EncryptedField]
		public byte[]? Field2 { get; set; }

		[EncryptedField]
		public byte[]? Field3 { get; set; }
	}

	/// <summary>
	/// Test entity with mixed property types.
	/// </summary>
	private sealed class TestEntityWithMixedProperties
	{
		public string Id { get; set; } = string.Empty;

		[EncryptedField]
		public byte[]? EncryptedBytes { get; set; }

		// These should be ignored - EncryptedField only works with byte[]
		public string? StringData { get; set; }
		public int IntData { get; set; }
	}

	/// <summary>
	/// Test entity with read-only encrypted field (cannot be re-encrypted).
	/// </summary>
	private sealed class TestEntityWithReadOnlyEncryptedField
	{
		public string Id { get; set; } = string.Empty;

		[EncryptedField]
		public byte[]? ReadOnlyEncrypted { get; } // No setter - can't be re-encrypted
	}

	#endregion
}
