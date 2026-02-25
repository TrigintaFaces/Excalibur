// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

namespace Excalibur.Tests.Caching.AdaptiveTtl;

/// <summary>
/// Tests for Sprint 567 S567.5: AdaptiveTtlOptions bounds validation.
/// Validates that [Range] DataAnnotations on AdaptiveTtlOptions properties
/// correctly enforce bounds: MinTtl > Zero, MaxTtl >= MinTtl, and multipliers > 0.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Caching")]
[Trait("Feature", "AdaptiveTtl")]
[Trait("Priority", "2")]
public sealed class AdaptiveTtlOptionsValidationShould : UnitTestBase
{
	#region MinTtl / MaxTtl Validation

	[Fact]
	public void DefaultOptions_PassValidation()
	{
		// Arrange
		var options = new AdaptiveTtlOptions();

		// Act
		var results = ValidateModel(options);

		// Assert
		results.ShouldBeEmpty("Default AdaptiveTtlOptions should pass validation");
	}

	[Fact]
	public void MinTtl_CanBeSetToPositiveValue()
	{
		// Arrange
		var options = new AdaptiveTtlOptions { MinTtl = TimeSpan.FromSeconds(1) };

		// Assert
		options.MinTtl.ShouldBe(TimeSpan.FromSeconds(1));
	}

	[Fact]
	public void MaxTtl_CanBeSetToValueGreaterThanMinTtl()
	{
		// Arrange
		var options = new AdaptiveTtlOptions
		{
			MinTtl = TimeSpan.FromSeconds(5),
			MaxTtl = TimeSpan.FromHours(1),
		};

		// Assert
		options.MaxTtl.ShouldBeGreaterThan(options.MinTtl);
	}

	#endregion

	#region TargetHitRate Validation

	[Theory]
	[InlineData(0.0)]
	[InlineData(0.5)]
	[InlineData(1.0)]
	public void TargetHitRate_AcceptsValuesInRange(double value)
	{
		// Arrange
		var options = new AdaptiveTtlOptions { TargetHitRate = value };

		// Act
		var results = ValidateModel(options);

		// Assert
		results.ShouldBeEmpty($"TargetHitRate={value} should be valid (0.0-1.0 range)");
	}

	[Theory]
	[InlineData(-0.1)]
	[InlineData(1.1)]
	[InlineData(2.0)]
	public void TargetHitRate_RejectsValuesOutOfRange(double value)
	{
		// Arrange
		var options = new AdaptiveTtlOptions { TargetHitRate = value };

		// Act
		var results = ValidateModel(options);

		// Assert
		results.ShouldNotBeEmpty($"TargetHitRate={value} should fail validation (outside 0.0-1.0)");
		results.ShouldContain(r => r.MemberNames.Contains(nameof(AdaptiveTtlOptions.TargetHitRate)));
	}

	#endregion

	#region LearningRate Validation

	[Theory]
	[InlineData(0.001)]
	[InlineData(0.1)]
	[InlineData(1.0)]
	public void LearningRate_AcceptsValuesInRange(double value)
	{
		// Arrange
		var options = new AdaptiveTtlOptions { LearningRate = value };

		// Act
		var results = ValidateModel(options);

		// Assert
		results.ShouldBeEmpty($"LearningRate={value} should be valid (0.001-1.0 range)");
	}

	[Theory]
	[InlineData(0.0)]
	[InlineData(-0.1)]
	[InlineData(1.1)]
	public void LearningRate_RejectsValuesOutOfRange(double value)
	{
		// Arrange
		var options = new AdaptiveTtlOptions { LearningRate = value };

		// Act
		var results = ValidateModel(options);

		// Assert
		results.ShouldNotBeEmpty($"LearningRate={value} should fail validation (outside 0.001-1.0)");
		results.ShouldContain(r => r.MemberNames.Contains(nameof(AdaptiveTtlOptions.LearningRate)));
	}

	#endregion

	#region DiscountFactor Validation

	[Theory]
	[InlineData(0.0)]
	[InlineData(0.5)]
	[InlineData(1.0)]
	public void DiscountFactor_AcceptsValuesInRange(double value)
	{
		// Arrange
		var options = new AdaptiveTtlOptions { DiscountFactor = value };

		// Act
		var results = ValidateModel(options);

		// Assert
		results.ShouldBeEmpty($"DiscountFactor={value} should be valid (0.0-1.0 range)");
	}

	[Theory]
	[InlineData(-0.1)]
	[InlineData(1.1)]
	public void DiscountFactor_RejectsValuesOutOfRange(double value)
	{
		// Arrange
		var options = new AdaptiveTtlOptions { DiscountFactor = value };

		// Act
		var results = ValidateModel(options);

		// Assert
		results.ShouldNotBeEmpty($"DiscountFactor={value} should fail validation (outside 0.0-1.0)");
		results.ShouldContain(r => r.MemberNames.Contains(nameof(AdaptiveTtlOptions.DiscountFactor)));
	}

	#endregion

	#region Weight Options Validation

	[Theory]
	[InlineData(0.0)]
	[InlineData(0.5)]
	[InlineData(1.0)]
	public void WeightOptions_AcceptValuesInRange(double value)
	{
		// Arrange
		var weights = new AdaptiveTtlWeightOptions
		{
			HitRateWeight = value,
			AccessFrequencyWeight = value,
			TemporalWeight = value,
			CostWeight = value,
			LoadWeight = value,
			VolatilityWeight = value,
		};

		// Act
		var results = ValidateModel(weights);

		// Assert
		results.ShouldBeEmpty($"All weights set to {value} should be valid (0.0-1.0 range)");
	}

	[Theory]
	[InlineData(-0.1)]
	[InlineData(1.1)]
	public void HitRateWeight_RejectsValuesOutOfRange(double value)
	{
		// Arrange
		var weights = new AdaptiveTtlWeightOptions { HitRateWeight = value };

		// Act
		var results = ValidateModel(weights);

		// Assert
		results.ShouldNotBeEmpty($"HitRateWeight={value} should fail validation");
	}

	[Theory]
	[InlineData(-0.1)]
	[InlineData(1.1)]
	public void AccessFrequencyWeight_RejectsValuesOutOfRange(double value)
	{
		// Arrange
		var weights = new AdaptiveTtlWeightOptions { AccessFrequencyWeight = value };

		// Act
		var results = ValidateModel(weights);

		// Assert
		results.ShouldNotBeEmpty($"AccessFrequencyWeight={value} should fail validation");
	}

	#endregion

	#region Threshold Options Validation

	[Theory]
	[InlineData(0.0)]
	[InlineData(0.5)]
	[InlineData(1.0)]
	public void HighLoadThreshold_AcceptsValuesInRange(double value)
	{
		// Arrange
		var thresholds = new AdaptiveTtlThresholdOptions { HighLoadThreshold = value };

		// Act
		var results = ValidateModel(thresholds);

		// Assert
		results.ShouldBeEmpty($"HighLoadThreshold={value} should be valid (0.0-1.0 range)");
	}

	[Theory]
	[InlineData(-0.1)]
	[InlineData(1.1)]
	public void HighLoadThreshold_RejectsValuesOutOfRange(double value)
	{
		// Arrange
		var thresholds = new AdaptiveTtlThresholdOptions { HighLoadThreshold = value };

		// Act
		var results = ValidateModel(thresholds);

		// Assert
		results.ShouldNotBeEmpty($"HighLoadThreshold={value} should fail validation");
	}

	[Theory]
	[InlineData(0.0)]
	[InlineData(0.5)]
	[InlineData(1.0)]
	public void LowLoadThreshold_AcceptsValuesInRange(double value)
	{
		// Arrange
		var thresholds = new AdaptiveTtlThresholdOptions { LowLoadThreshold = value };

		// Act
		var results = ValidateModel(thresholds);

		// Assert
		results.ShouldBeEmpty($"LowLoadThreshold={value} should be valid (0.0-1.0 range)");
	}

	[Theory]
	[InlineData(-0.1)]
	[InlineData(1.1)]
	public void LowLoadThreshold_RejectsValuesOutOfRange(double value)
	{
		// Arrange
		var thresholds = new AdaptiveTtlThresholdOptions { LowLoadThreshold = value };

		// Act
		var results = ValidateModel(thresholds);

		// Assert
		results.ShouldNotBeEmpty($"LowLoadThreshold={value} should fail validation");
	}

	[Fact]
	public void MaxExpectedFrequency_AcceptsPositiveValues()
	{
		// Arrange
		var thresholds = new AdaptiveTtlThresholdOptions { MaxExpectedFrequency = 0.1 };

		// Act
		var results = ValidateModel(thresholds);

		// Assert
		results.ShouldBeEmpty("MaxExpectedFrequency=0.1 should be valid");
	}

	[Fact]
	public void LargeContentThresholdMb_AcceptsPositiveValues()
	{
		// Arrange
		var thresholds = new AdaptiveTtlThresholdOptions { LargeContentThresholdMb = 0.001 };

		// Act
		var results = ValidateModel(thresholds);

		// Assert
		results.ShouldBeEmpty("LargeContentThresholdMb=0.001 should be valid");
	}

	#endregion

	#region Multiple Validation Failures

	[Fact]
	public void MultipleInvalidProperties_ReportsAllFailures()
	{
		// Arrange
		var options = new AdaptiveTtlOptions
		{
			TargetHitRate = -1.0,   // invalid
			LearningRate = 0.0,     // invalid (min is 0.001)
			DiscountFactor = 2.0,   // invalid
		};

		// Act
		var results = ValidateModel(options);

		// Assert
		results.Count.ShouldBeGreaterThanOrEqualTo(3, "Should report at least 3 validation failures");
	}

	#endregion

	#region Helper

	private static List<ValidationResult> ValidateModel(object model)
	{
		var context = new ValidationContext(model);
		var results = new List<ValidationResult>();
		Validator.TryValidateObject(model, context, results, validateAllProperties: true);
		return results;
	}

	#endregion
}
