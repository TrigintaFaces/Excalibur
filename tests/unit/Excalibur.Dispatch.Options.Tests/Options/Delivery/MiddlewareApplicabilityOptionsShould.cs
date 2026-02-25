// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Options.Delivery;

namespace Excalibur.Dispatch.Tests.Options.Delivery;

/// <summary>
/// Unit tests for <see cref="MiddlewareApplicabilityOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Options")]
[Trait("Priority", "0")]
public sealed class MiddlewareApplicabilityOptionsShould
{
	#region Default Value Tests

	[Fact]
	public void Default_IncludeOnError_IsFalse()
	{
		// Arrange & Act
		var options = new MiddlewareApplicabilityOptions();

		// Assert
		options.IncludeOnError.ShouldBeFalse();
	}

	[Fact]
	public void Default_EnableCaching_IsTrue()
	{
		// Arrange & Act
		var options = new MiddlewareApplicabilityOptions();

		// Assert
		options.EnableCaching.ShouldBeTrue();
	}

	#endregion

	#region Property Setter Tests

	[Fact]
	public void IncludeOnError_CanBeSet()
	{
		// Arrange
		var options = new MiddlewareApplicabilityOptions();

		// Act
		options.IncludeOnError = true;

		// Assert
		options.IncludeOnError.ShouldBeTrue();
	}

	[Fact]
	public void EnableCaching_CanBeSet()
	{
		// Arrange
		var options = new MiddlewareApplicabilityOptions();

		// Act
		options.EnableCaching = false;

		// Assert
		options.EnableCaching.ShouldBeFalse();
	}

	#endregion

	#region Object Initializer Tests

	[Fact]
	public void ObjectInitializer_SetsAllProperties()
	{
		// Act
		var options = new MiddlewareApplicabilityOptions
		{
			IncludeOnError = true,
			EnableCaching = false,
		};

		// Assert
		options.IncludeOnError.ShouldBeTrue();
		options.EnableCaching.ShouldBeFalse();
	}

	#endregion

	#region Real-World Scenario Tests

	[Fact]
	public void Options_ForFailSafe_ExcludesMiddlewareOnError()
	{
		// Act
		var options = new MiddlewareApplicabilityOptions
		{
			IncludeOnError = false,
		};

		// Assert
		options.IncludeOnError.ShouldBeFalse();
	}

	[Fact]
	public void Options_ForThroughput_EnablesCaching()
	{
		// Act
		var options = new MiddlewareApplicabilityOptions
		{
			EnableCaching = true,
		};

		// Assert
		options.EnableCaching.ShouldBeTrue();
	}

	[Fact]
	public void Options_ForDebugging_DisablesCaching()
	{
		// Act
		var options = new MiddlewareApplicabilityOptions
		{
			EnableCaching = false,
			IncludeOnError = true,
		};

		// Assert
		options.EnableCaching.ShouldBeFalse();
		options.IncludeOnError.ShouldBeTrue();
	}

	#endregion
}
