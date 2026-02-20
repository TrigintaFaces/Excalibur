// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Serialization;

namespace Excalibur.Dispatch.Tests.Serialization;

/// <summary>
/// Unit tests for <see cref="EncryptionMigrationProgress"/> and <see cref="MigrationOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class MigrationProgressShould
{
	// EncryptionMigrationProgress tests
	[Fact]
	public void CreateInitialProgressWithZeroValues()
	{
		// Arrange & Act
		var progress = EncryptionMigrationProgress.Initial;

		// Assert
		progress.TotalMigrated.ShouldBe(0);
		progress.TotalFailed.ShouldBe(0);
		progress.TotalSkipped.ShouldBe(0);
		progress.CurrentBatchSize.ShouldBe(0);
		progress.EstimatedRemaining.ShouldBeNull();
	}

	[Fact]
	public void CalculateTotalProcessedCorrectly()
	{
		// Arrange
		var progress = new EncryptionMigrationProgress(
			TotalMigrated: 100,
			TotalFailed: 5,
			TotalSkipped: 10,
			CurrentBatchSize: 50);

		// Act & Assert
		progress.TotalProcessed.ShouldBe(115); // 100 + 5 + 10
	}

	[Fact]
	public void ReturnHundredPercentSuccessRateWhenNoRecordsProcessed()
	{
		// Arrange
		var progress = EncryptionMigrationProgress.Initial;

		// Act & Assert
		progress.SuccessRate.ShouldBe(100.0);
	}

	[Fact]
	public void CalculateSuccessRateCorrectly()
	{
		// Arrange - 80 migrated, 20 failed = 80% success
		var progress = new EncryptionMigrationProgress(
			TotalMigrated: 80,
			TotalFailed: 20,
			TotalSkipped: 0,
			CurrentBatchSize: 100);

		// Act & Assert
		progress.SuccessRate.ShouldBe(80.0);
	}

	[Fact]
	public void ExcludeSkippedFromSuccessRateCalculation()
	{
		// Arrange - 80 migrated, 20 failed, 100 skipped = 80% success (skipped not counted)
		var progress = new EncryptionMigrationProgress(
			TotalMigrated: 80,
			TotalFailed: 20,
			TotalSkipped: 100,
			CurrentBatchSize: 100);

		// Act & Assert
		progress.SuccessRate.ShouldBe(80.0);
	}

	[Fact]
	public void CalculateFullSuccessRate()
	{
		// Arrange
		var progress = new EncryptionMigrationProgress(
			TotalMigrated: 100,
			TotalFailed: 0,
			TotalSkipped: 0,
			CurrentBatchSize: 100);

		// Act & Assert
		progress.SuccessRate.ShouldBe(100.0);
	}

	[Fact]
	public void CalculateZeroSuccessRate()
	{
		// Arrange
		var progress = new EncryptionMigrationProgress(
			TotalMigrated: 0,
			TotalFailed: 100,
			TotalSkipped: 0,
			CurrentBatchSize: 100);

		// Act & Assert
		progress.SuccessRate.ShouldBe(0.0);
	}

	[Fact]
	public void CalculateFailureRateAsInverseOfSuccessRate()
	{
		// Arrange - 80% success = 20% failure
		var progress = new EncryptionMigrationProgress(
			TotalMigrated: 80,
			TotalFailed: 20,
			TotalSkipped: 0,
			CurrentBatchSize: 100);

		// Act & Assert
		progress.FailureRate.ShouldBe(20.0);
	}

	[Fact]
	public void ReturnNullCompletionPercentageWhenEstimatedRemainingIsNull()
	{
		// Arrange
		var progress = new EncryptionMigrationProgress(
			TotalMigrated: 100,
			TotalFailed: 0,
			TotalSkipped: 0,
			CurrentBatchSize: 100,
			EstimatedRemaining: null);

		// Act & Assert
		progress.CompletionPercentage.ShouldBeNull();
	}

	[Fact]
	public void CalculateCompletionPercentageCorrectly()
	{
		// Arrange - 75 processed, 25 remaining = 75% complete
		var progress = new EncryptionMigrationProgress(
			TotalMigrated: 70,
			TotalFailed: 3,
			TotalSkipped: 2,
			CurrentBatchSize: 50,
			EstimatedRemaining: 25);

		// Act & Assert
		progress.CompletionPercentage.ShouldBe(75.0);
	}

	[Fact]
	public void ReturnHundredPercentCompletionWhenNoRecordsRemaining()
	{
		// Arrange
		var progress = new EncryptionMigrationProgress(
			TotalMigrated: 100,
			TotalFailed: 0,
			TotalSkipped: 0,
			CurrentBatchSize: 100,
			EstimatedRemaining: 0);

		// Act & Assert
		progress.CompletionPercentage.ShouldBe(100.0);
	}

	[Fact]
	public void ReturnHundredPercentCompletionWhenBothZero()
	{
		// Arrange - Edge case: no records processed, no records remaining
		var progress = new EncryptionMigrationProgress(
			TotalMigrated: 0,
			TotalFailed: 0,
			TotalSkipped: 0,
			CurrentBatchSize: 0,
			EstimatedRemaining: 0);

		// Act & Assert
		progress.CompletionPercentage.ShouldBe(100.0);
	}

	[Fact]
	public void GenerateToStringWithoutCompletionPercentage()
	{
		// Arrange
		var progress = new EncryptionMigrationProgress(
			TotalMigrated: 100,
			TotalFailed: 5,
			TotalSkipped: 10,
			CurrentBatchSize: 50);

		// Act
		var result = progress.ToString();

		// Assert
		result.ShouldContain("Migrated: 100");
		result.ShouldContain("Failed: 5");
		result.ShouldContain("Skipped: 10");
		result.ShouldContain("Success rate:");
		result.ShouldNotContain("complete");
	}

	[Fact]
	public void GenerateToStringWithCompletionPercentage()
	{
		// Arrange
		var progress = new EncryptionMigrationProgress(
			TotalMigrated: 100,
			TotalFailed: 5,
			TotalSkipped: 10,
			CurrentBatchSize: 50,
			EstimatedRemaining: 85);

		// Act
		var result = progress.ToString();

		// Assert
		result.ShouldContain("Migrated: 100");
		result.ShouldContain("complete");
	}

	[Fact]
	public void SupportRecordEquality()
	{
		// Arrange
		var progress1 = new EncryptionMigrationProgress(100, 5, 10, 50, 25);
		var progress2 = new EncryptionMigrationProgress(100, 5, 10, 50, 25);

		// Act & Assert
		progress1.ShouldBe(progress2);
		(progress1 == progress2).ShouldBeTrue();
	}

	[Fact]
	public void SupportRecordInequality()
	{
		// Arrange
		var progress1 = new EncryptionMigrationProgress(100, 5, 10, 50, 25);
		var progress2 = new EncryptionMigrationProgress(100, 6, 10, 50, 25);

		// Act & Assert
		progress1.ShouldNotBe(progress2);
		(progress1 != progress2).ShouldBeTrue();
	}

	// MigrationOptions tests
	[Fact]
	public void HaveDefaultBatchSizeOfOneThousand()
	{
		// Arrange & Act
		var options = new MigrationOptions();

		// Assert
		options.BatchSize.ShouldBe(1000);
	}

	[Fact]
	public void HaveReadBackVerificationDisabledByDefault()
	{
		// Arrange & Act
		var options = new MigrationOptions();

		// Assert
		options.EnableReadBackVerification.ShouldBeFalse();
	}

	[Fact]
	public void HaveDefaultMaxConsecutiveFailuresOfOneHundred()
	{
		// Arrange & Act
		var options = new MigrationOptions();

		// Assert
		options.MaxConsecutiveFailures.ShouldBe(100);
	}

	[Fact]
	public void HaveContinueOnFailureEnabledByDefault()
	{
		// Arrange & Act
		var options = new MigrationOptions();

		// Assert
		options.ContinueOnFailure.ShouldBeTrue();
	}

	[Fact]
	public void HaveZeroDelayBetweenBatchesByDefault()
	{
		// Arrange & Act
		var options = new MigrationOptions();

		// Assert
		options.DelayBetweenBatchesMs.ShouldBe(0);
	}

	[Fact]
	public void AllowSettingBatchSize()
	{
		// Arrange
		var options = new MigrationOptions();

		// Act
		options.BatchSize = 500;

		// Assert
		options.BatchSize.ShouldBe(500);
	}

	[Fact]
	public void AllowSettingEnableReadBackVerification()
	{
		// Arrange
		var options = new MigrationOptions();

		// Act
		options.EnableReadBackVerification = true;

		// Assert
		options.EnableReadBackVerification.ShouldBeTrue();
	}

	[Fact]
	public void AllowSettingMaxConsecutiveFailures()
	{
		// Arrange
		var options = new MigrationOptions();

		// Act
		options.MaxConsecutiveFailures = 50;

		// Assert
		options.MaxConsecutiveFailures.ShouldBe(50);
	}

	[Fact]
	public void AllowSettingContinueOnFailure()
	{
		// Arrange
		var options = new MigrationOptions();

		// Act
		options.ContinueOnFailure = false;

		// Assert
		options.ContinueOnFailure.ShouldBeFalse();
	}

	[Fact]
	public void AllowSettingDelayBetweenBatches()
	{
		// Arrange
		var options = new MigrationOptions();

		// Act
		options.DelayBetweenBatchesMs = 100;

		// Assert
		options.DelayBetweenBatchesMs.ShouldBe(100);
	}

	[Fact]
	public void SupportObjectInitializer()
	{
		// Arrange & Act
		var options = new MigrationOptions
		{
			BatchSize = 500,
			EnableReadBackVerification = true,
			MaxConsecutiveFailures = 50,
			ContinueOnFailure = false,
			DelayBetweenBatchesMs = 100,
		};

		// Assert
		options.BatchSize.ShouldBe(500);
		options.EnableReadBackVerification.ShouldBeTrue();
		options.MaxConsecutiveFailures.ShouldBe(50);
		options.ContinueOnFailure.ShouldBeFalse();
		options.DelayBetweenBatchesMs.ShouldBe(100);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	[InlineData(100)]
	[InlineData(10000)]
	public void AcceptVariousBatchSizes(int batchSize)
	{
		// Arrange
		var options = new MigrationOptions { BatchSize = batchSize };

		// Assert
		options.BatchSize.ShouldBe(batchSize);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(50)]
	[InlineData(1000)]
	public void AcceptVariousDelayValues(int delay)
	{
		// Arrange
		var options = new MigrationOptions { DelayBetweenBatchesMs = delay };

		// Assert
		options.DelayBetweenBatchesMs.ShouldBe(delay);
	}
}
