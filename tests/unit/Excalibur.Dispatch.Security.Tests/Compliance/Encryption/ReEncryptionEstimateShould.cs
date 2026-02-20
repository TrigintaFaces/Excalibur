// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Security.Tests.Compliance.Encryption;

/// <summary>
/// Unit tests for <see cref="ReEncryptionEstimate"/> class.
/// </summary>
/// <remarks>
/// Per AD-256-1, these tests verify the re-encryption estimation for planning purposes.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Compliance")]
public sealed class ReEncryptionEstimateShould
{
	#region Default Value Tests

	[Fact]
	public void HaveZeroEstimatedItemCountByDefault()
	{
		// Arrange & Act
		var estimate = new ReEncryptionEstimate();

		// Assert
		estimate.EstimatedItemCount.ShouldBe(0);
	}

	[Fact]
	public void HaveZeroEstimatedFieldsPerItemByDefault()
	{
		// Arrange & Act
		var estimate = new ReEncryptionEstimate();

		// Assert
		estimate.EstimatedFieldsPerItem.ShouldBe(0);
	}

	[Fact]
	public void HaveZeroEstimatedDurationByDefault()
	{
		// Arrange & Act
		var estimate = new ReEncryptionEstimate();

		// Assert
		estimate.EstimatedDuration.ShouldBe(TimeSpan.Zero);
	}

	[Fact]
	public void HaveEmptyWarningsByDefault()
	{
		// Arrange & Act
		var estimate = new ReEncryptionEstimate();

		// Assert
		_ = estimate.Warnings.ShouldNotBeNull();
		estimate.Warnings.ShouldBeEmpty();
	}

	[Fact]
	public void HaveIsSampledFalseByDefault()
	{
		// Arrange & Act
		var estimate = new ReEncryptionEstimate();

		// Assert
		estimate.IsSampled.ShouldBeFalse();
	}

	#endregion Default Value Tests

	#region Property Assignment Tests

	[Theory]
	[InlineData(0L)]
	[InlineData(100L)]
	[InlineData(10000L)]
	[InlineData(1000000L)]
	public void AllowEstimatedItemCountConfiguration(long itemCount)
	{
		// Arrange & Act
		var estimate = new ReEncryptionEstimate { EstimatedItemCount = itemCount };

		// Assert
		estimate.EstimatedItemCount.ShouldBe(itemCount);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	[InlineData(5)]
	[InlineData(10)]
	public void AllowEstimatedFieldsPerItemConfiguration(int fieldsPerItem)
	{
		// Arrange & Act
		var estimate = new ReEncryptionEstimate { EstimatedFieldsPerItem = fieldsPerItem };

		// Assert
		estimate.EstimatedFieldsPerItem.ShouldBe(fieldsPerItem);
	}

	[Fact]
	public void AllowEstimatedDurationConfiguration()
	{
		// Arrange
		var duration = TimeSpan.FromHours(2);

		// Act
		var estimate = new ReEncryptionEstimate { EstimatedDuration = duration };

		// Assert
		estimate.EstimatedDuration.ShouldBe(duration);
	}

	[Fact]
	public void AllowWarningsConfiguration()
	{
		// Arrange
		var warnings = new List<string> { "Warning 1", "Warning 2" };

		// Act
		var estimate = new ReEncryptionEstimate { Warnings = warnings };

		// Assert
		estimate.Warnings.ShouldBe(warnings);
		estimate.Warnings.Count.ShouldBe(2);
	}

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public void AllowIsSampledConfiguration(bool isSampled)
	{
		// Arrange & Act
		var estimate = new ReEncryptionEstimate { IsSampled = isSampled };

		// Assert
		estimate.IsSampled.ShouldBe(isSampled);
	}

	#endregion Property Assignment Tests

	#region Semantic Tests

	[Fact]
	public void SupportSmallDatasetEstimate()
	{
		// Per AD-256-1: Small dataset with full scan
		var estimate = new ReEncryptionEstimate
		{
			EstimatedItemCount = 1000,
			EstimatedFieldsPerItem = 3,
			EstimatedDuration = TimeSpan.FromMinutes(5),
			IsSampled = false,
			Warnings = []
		};

		estimate.EstimatedItemCount.ShouldBe(1000);
		estimate.IsSampled.ShouldBeFalse();
		estimate.Warnings.ShouldBeEmpty();
	}

	[Fact]
	public void SupportLargeDatasetEstimate_WithSampling()
	{
		// Per AD-256-1: Large dataset with sampling for performance
		var estimate = new ReEncryptionEstimate
		{
			EstimatedItemCount = 10_000_000,
			EstimatedFieldsPerItem = 5,
			EstimatedDuration = TimeSpan.FromHours(8),
			IsSampled = true,
			Warnings = new[] { "Estimate based on 1% sample" }
		};

		estimate.EstimatedItemCount.ShouldBe(10_000_000);
		estimate.IsSampled.ShouldBeTrue();
		estimate.Warnings.ShouldContain("Estimate based on 1% sample");
	}

	[Fact]
	public void SupportEstimateWithMultipleWarnings()
	{
		// Multiple warnings for complex scenarios
		var warnings = new List<string>
		{
			"High parallelism may impact KMS rate limits",
			"Estimate includes items with missing provider metadata",
			"Some fields may require manual review"
		};

		var estimate = new ReEncryptionEstimate
		{
			EstimatedItemCount = 50000,
			EstimatedFieldsPerItem = 2,
			EstimatedDuration = TimeSpan.FromMinutes(30),
			Warnings = warnings
		};

		estimate.Warnings.Count.ShouldBe(3);
		estimate.Warnings.ShouldContain("High parallelism may impact KMS rate limits");
	}

	[Fact]
	public void BeFullyConfigurable()
	{
		// Arrange
		var warnings = new List<string> { "Estimate warning" };

		// Act
		var estimate = new ReEncryptionEstimate
		{
			EstimatedItemCount = 500_000,
			EstimatedFieldsPerItem = 4,
			EstimatedDuration = TimeSpan.FromHours(2.5),
			IsSampled = true,
			Warnings = warnings
		};

		// Assert
		estimate.EstimatedItemCount.ShouldBe(500_000);
		estimate.EstimatedFieldsPerItem.ShouldBe(4);
		estimate.EstimatedDuration.ShouldBe(TimeSpan.FromHours(2.5));
		estimate.IsSampled.ShouldBeTrue();
		estimate.Warnings.ShouldBe(warnings);
	}

	[Fact]
	public void SupportPlanningUseCase()
	{
		// Per AD-256-1: Use for planning and progress tracking
		var estimate = new ReEncryptionEstimate
		{
			EstimatedItemCount = 100_000,
			EstimatedFieldsPerItem = 2,
			EstimatedDuration = TimeSpan.FromMinutes(45),
			IsSampled = false
		};

		// Can calculate total fields
		var totalFields = estimate.EstimatedItemCount * estimate.EstimatedFieldsPerItem;
		totalFields.ShouldBe(200_000);

		// Reasonable duration estimate
		estimate.EstimatedDuration.TotalMinutes.ShouldBeGreaterThan(0);
	}

	[Fact]
	public void SupportEmptyDataset()
	{
		// No items to process
		var estimate = new ReEncryptionEstimate
		{
			EstimatedItemCount = 0,
			EstimatedFieldsPerItem = 0,
			EstimatedDuration = TimeSpan.Zero,
			IsSampled = false
		};

		estimate.EstimatedItemCount.ShouldBe(0);
		estimate.EstimatedDuration.ShouldBe(TimeSpan.Zero);
	}

	[Fact]
	public void SupportReadOnlyWarnings()
	{
		// Warnings should be readonly list
		var warnings = new List<string> { "Test warning" };
		var estimate = new ReEncryptionEstimate { Warnings = warnings };

		// The property returns IReadOnlyList<string>
		_ = estimate.Warnings.ShouldBeAssignableTo<IReadOnlyList<string>>();
	}

	#endregion Semantic Tests
}
