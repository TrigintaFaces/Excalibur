// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Options.Middleware;

namespace Excalibur.Dispatch.Tests.Options.Middleware;

/// <summary>
/// Unit tests for <see cref="ValidationOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Options")]
[Trait("Priority", "0")]
public sealed class ValidationOptionsShould
{
	#region Default Value Tests

	[Fact]
	public void Default_Enabled_IsTrue()
	{
		// Arrange & Act
		var options = new ValidationOptions();

		// Assert
		options.Enabled.ShouldBeTrue();
	}

	[Fact]
	public void Default_UseDataAnnotations_IsTrue()
	{
		// Arrange & Act
		var options = new ValidationOptions();

		// Assert
		options.UseDataAnnotations.ShouldBeTrue();
	}

	[Fact]
	public void Default_UseCustomValidation_IsTrue()
	{
		// Arrange & Act
		var options = new ValidationOptions();

		// Assert
		options.UseCustomValidation.ShouldBeTrue();
	}

	[Fact]
	public void Default_StopOnFirstError_IsFalse()
	{
		// Arrange & Act
		var options = new ValidationOptions();

		// Assert
		options.StopOnFirstError.ShouldBeFalse();
	}

	[Fact]
	public void Default_BypassValidationForTypes_IsNull()
	{
		// Arrange & Act
		var options = new ValidationOptions();

		// Assert
		options.BypassValidationForTypes.ShouldBeNull();
	}

	#endregion

	#region Property Setter Tests

	[Fact]
	public void Enabled_CanBeSet()
	{
		// Arrange
		var options = new ValidationOptions();

		// Act
		options.Enabled = false;

		// Assert
		options.Enabled.ShouldBeFalse();
	}

	[Fact]
	public void UseDataAnnotations_CanBeSet()
	{
		// Arrange
		var options = new ValidationOptions();

		// Act
		options.UseDataAnnotations = false;

		// Assert
		options.UseDataAnnotations.ShouldBeFalse();
	}

	[Fact]
	public void UseCustomValidation_CanBeSet()
	{
		// Arrange
		var options = new ValidationOptions();

		// Act
		options.UseCustomValidation = false;

		// Assert
		options.UseCustomValidation.ShouldBeFalse();
	}

	[Fact]
	public void StopOnFirstError_CanBeSet()
	{
		// Arrange
		var options = new ValidationOptions();

		// Act
		options.StopOnFirstError = true;

		// Assert
		options.StopOnFirstError.ShouldBeTrue();
	}

	[Fact]
	public void BypassValidationForTypes_CanBeSet()
	{
		// Arrange
		var options = new ValidationOptions();
		var types = new[] { "SystemMessage", "InternalEvent" };

		// Act
		options.BypassValidationForTypes = types;

		// Assert
		options.BypassValidationForTypes.ShouldBe(types);
	}

	#endregion

	#region Object Initializer Tests

	[Fact]
	public void ObjectInitializer_SetsAllProperties()
	{
		// Arrange
		var bypassTypes = new[] { "TestType" };

		// Act
		var options = new ValidationOptions
		{
			Enabled = false,
			UseDataAnnotations = false,
			UseCustomValidation = false,
			StopOnFirstError = true,
			BypassValidationForTypes = bypassTypes,
		};

		// Assert
		options.Enabled.ShouldBeFalse();
		options.UseDataAnnotations.ShouldBeFalse();
		options.UseCustomValidation.ShouldBeFalse();
		options.StopOnFirstError.ShouldBeTrue();
		options.BypassValidationForTypes.ShouldBe(bypassTypes);
	}

	#endregion

	#region Real-World Scenario Tests

	[Fact]
	public void Options_ForPerformance_StopsOnFirstError()
	{
		// Act
		var options = new ValidationOptions
		{
			StopOnFirstError = true,
		};

		// Assert
		options.StopOnFirstError.ShouldBeTrue();
	}

	[Fact]
	public void Options_ForDataAnnotationsOnly_DisablesCustomValidation()
	{
		// Act
		var options = new ValidationOptions
		{
			UseDataAnnotations = true,
			UseCustomValidation = false,
		};

		// Assert
		options.UseDataAnnotations.ShouldBeTrue();
		options.UseCustomValidation.ShouldBeFalse();
	}

	#endregion
}
