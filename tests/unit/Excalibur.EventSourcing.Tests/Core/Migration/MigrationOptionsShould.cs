// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

using Excalibur.EventSourcing.Migration;

namespace Excalibur.EventSourcing.Tests.Core.Migration;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class MigrationOptionsShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		// Arrange & Act
		var sut = new MigrationOptions();

		// Assert
		sut.BatchSize.ShouldBe(500);
		sut.SourceStreamPattern.ShouldBeNull();
		sut.TargetStreamPrefix.ShouldBeNull();
		sut.DryRun.ShouldBeFalse();
		sut.MaxEvents.ShouldBe(0);
		sut.ContinueOnError.ShouldBeFalse();
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		// Arrange & Act
		var sut = new MigrationOptions
		{
			BatchSize = 1000,
			SourceStreamPattern = "Order-*",
			TargetStreamPrefix = "V2_",
			DryRun = true,
			MaxEvents = 5000,
			ContinueOnError = true,
		};

		// Assert
		sut.BatchSize.ShouldBe(1000);
		sut.SourceStreamPattern.ShouldBe("Order-*");
		sut.TargetStreamPrefix.ShouldBe("V2_");
		sut.DryRun.ShouldBeTrue();
		sut.MaxEvents.ShouldBe(5000);
		sut.ContinueOnError.ShouldBeTrue();
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	[InlineData(100001)]
	public void FailValidationForInvalidBatchSize(int invalidBatchSize)
	{
		// Arrange
		var sut = new MigrationOptions { BatchSize = invalidBatchSize };
		var context = new ValidationContext(sut) { MemberName = nameof(MigrationOptions.BatchSize) };
		var results = new List<ValidationResult>();

		// Act
		var isValid = Validator.TryValidateProperty(sut.BatchSize, context, results);

		// Assert
		isValid.ShouldBeFalse();
		results.ShouldNotBeEmpty();
	}

	[Theory]
	[InlineData(1)]
	[InlineData(500)]
	[InlineData(100000)]
	public void PassValidationForValidBatchSize(int validBatchSize)
	{
		// Arrange
		var sut = new MigrationOptions { BatchSize = validBatchSize };
		var context = new ValidationContext(sut) { MemberName = nameof(MigrationOptions.BatchSize) };
		var results = new List<ValidationResult>();

		// Act
		var isValid = Validator.TryValidateProperty(sut.BatchSize, context, results);

		// Assert
		isValid.ShouldBeTrue();
	}

	[Fact]
	public void FailValidationForNegativeMaxEvents()
	{
		// Arrange
		var sut = new MigrationOptions { MaxEvents = -1 };
		var context = new ValidationContext(sut) { MemberName = nameof(MigrationOptions.MaxEvents) };
		var results = new List<ValidationResult>();

		// Act
		var isValid = Validator.TryValidateProperty(sut.MaxEvents, context, results);

		// Assert
		isValid.ShouldBeFalse();
	}

	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	[InlineData(int.MaxValue)]
	public void PassValidationForValidMaxEvents(int validMaxEvents)
	{
		// Arrange
		var sut = new MigrationOptions { MaxEvents = validMaxEvents };
		var context = new ValidationContext(sut) { MemberName = nameof(MigrationOptions.MaxEvents) };
		var results = new List<ValidationResult>();

		// Act
		var isValid = Validator.TryValidateProperty(sut.MaxEvents, context, results);

		// Assert
		isValid.ShouldBeTrue();
	}
}
