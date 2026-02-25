// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;
using System.Reflection;

using Excalibur.EventSourcing.Migration;

namespace Excalibur.EventSourcing.Tests.Core.Migration;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class MigrationRunnerOptionsShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		// Arrange & Act
		var sut = new MigrationRunnerOptions();

		// Assert
		sut.MigrationAssembly.ShouldBeNull();
		sut.DryRun.ShouldBeFalse();
		sut.ContinueOnError.ShouldBeFalse();
		sut.ParallelStreams.ShouldBe(1);
		sut.BatchSize.ShouldBe(500);
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		// Arrange
		var assembly = Assembly.GetExecutingAssembly();

		// Act
		var sut = new MigrationRunnerOptions
		{
			MigrationAssembly = assembly,
			DryRun = true,
			ContinueOnError = true,
			ParallelStreams = 8,
			BatchSize = 250,
		};

		// Assert
		sut.MigrationAssembly.ShouldBe(assembly);
		sut.DryRun.ShouldBeTrue();
		sut.ContinueOnError.ShouldBeTrue();
		sut.ParallelStreams.ShouldBe(8);
		sut.BatchSize.ShouldBe(250);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	[InlineData(33)]
	public void FailValidationForInvalidParallelStreams(int invalidParallelStreams)
	{
		// Arrange
		var sut = new MigrationRunnerOptions { ParallelStreams = invalidParallelStreams };
		var context = new ValidationContext(sut) { MemberName = nameof(MigrationRunnerOptions.ParallelStreams) };
		var results = new List<ValidationResult>();

		// Act
		var isValid = Validator.TryValidateProperty(sut.ParallelStreams, context, results);

		// Assert
		isValid.ShouldBeFalse();
	}

	[Theory]
	[InlineData(1)]
	[InlineData(16)]
	[InlineData(32)]
	public void PassValidationForValidParallelStreams(int validParallelStreams)
	{
		// Arrange
		var sut = new MigrationRunnerOptions { ParallelStreams = validParallelStreams };
		var context = new ValidationContext(sut) { MemberName = nameof(MigrationRunnerOptions.ParallelStreams) };
		var results = new List<ValidationResult>();

		// Act
		var isValid = Validator.TryValidateProperty(sut.ParallelStreams, context, results);

		// Assert
		isValid.ShouldBeTrue();
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	[InlineData(100001)]
	public void FailValidationForInvalidBatchSize(int invalidBatchSize)
	{
		// Arrange
		var sut = new MigrationRunnerOptions { BatchSize = invalidBatchSize };
		var context = new ValidationContext(sut) { MemberName = nameof(MigrationRunnerOptions.BatchSize) };
		var results = new List<ValidationResult>();

		// Act
		var isValid = Validator.TryValidateProperty(sut.BatchSize, context, results);

		// Assert
		isValid.ShouldBeFalse();
	}

	[Theory]
	[InlineData(1)]
	[InlineData(500)]
	[InlineData(100000)]
	public void PassValidationForValidBatchSize(int validBatchSize)
	{
		// Arrange
		var sut = new MigrationRunnerOptions { BatchSize = validBatchSize };
		var context = new ValidationContext(sut) { MemberName = nameof(MigrationRunnerOptions.BatchSize) };
		var results = new List<ValidationResult>();

		// Act
		var isValid = Validator.TryValidateProperty(sut.BatchSize, context, results);

		// Assert
		isValid.ShouldBeTrue();
	}
}
