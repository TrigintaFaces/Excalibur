// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Validation.Context;

namespace Excalibur.Dispatch.Tests.Validation;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ValidationEnumsShould
{
	// --- ValidationMode ---

	[Fact]
	public void ValidationMode_HaveExpectedValues()
	{
		// Assert
		ValidationMode.Strict.ShouldBe((ValidationMode)0);
		ValidationMode.Lenient.ShouldBe((ValidationMode)1);
	}

	[Fact]
	public void ValidationMode_HaveTwoValues()
	{
		// Act
		var values = Enum.GetValues<ValidationMode>();

		// Assert
		values.Length.ShouldBe(2);
	}

	[Fact]
	public void ValidationMode_DefaultToStrict()
	{
		// Arrange
		ValidationMode mode = default;

		// Assert
		mode.ShouldBe(ValidationMode.Strict);
	}

	// --- ValidationSeverity ---

	[Fact]
	public void ValidationSeverity_HaveExpectedValues()
	{
		// Assert
		ValidationSeverity.Info.ShouldBe((ValidationSeverity)0);
		ValidationSeverity.Warning.ShouldBe((ValidationSeverity)1);
		ValidationSeverity.Error.ShouldBe((ValidationSeverity)2);
		ValidationSeverity.Critical.ShouldBe((ValidationSeverity)3);
	}

	[Fact]
	public void ValidationSeverity_HaveFourValues()
	{
		// Act
		var values = Enum.GetValues<ValidationSeverity>();

		// Assert
		values.Length.ShouldBe(4);
	}

	[Fact]
	public void ValidationSeverity_DefaultToInfo()
	{
		// Arrange
		ValidationSeverity severity = default;

		// Assert
		severity.ShouldBe(ValidationSeverity.Info);
	}
}
