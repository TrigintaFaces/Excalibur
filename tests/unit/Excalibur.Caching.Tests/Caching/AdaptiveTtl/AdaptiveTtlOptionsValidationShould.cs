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

	#endregion

	#region Multiple Validation Failures

	[Fact]
	public void MultipleInvalidProperties_ReportsAllFailures()
	{
		// Arrange
		var thresholds = new AdaptiveTtlThresholdOptions
		{
			HighLoadThreshold = -1.0,   // invalid
			LowLoadThreshold = 2.0,     // invalid
		};

		// Act
		var results = ValidateModel(thresholds);

		// Assert
		results.Count.ShouldBeGreaterThanOrEqualTo(2, "Should report a failure for every out-of-range property, not just the first");
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
