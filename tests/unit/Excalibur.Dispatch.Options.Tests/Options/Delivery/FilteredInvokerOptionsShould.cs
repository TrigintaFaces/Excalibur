// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Options.Delivery;

namespace Excalibur.Dispatch.Tests.Options.Delivery;

/// <summary>
/// Unit tests for <see cref="FilteredInvokerOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Options")]
[Trait("Priority", "0")]
public sealed class FilteredInvokerOptionsShould
{
	#region Default Value Tests

	[Fact]
	public void Default_EnableCaching_IsTrue()
	{
		// Arrange & Act
		var options = new FilteredInvokerOptions();

		// Assert
		options.EnableCaching.ShouldBeTrue();
	}

	[Fact]
	public void Default_IncludeMiddlewareOnFilterError_IsFalse()
	{
		// Arrange & Act
		var options = new FilteredInvokerOptions();

		// Assert
		options.IncludeMiddlewareOnFilterError.ShouldBeFalse();
	}

	[Fact]
	public void Default_MaxCachedEntries_Is64()
	{
		// Arrange & Act
		var options = new FilteredInvokerOptions();

		// Assert
		options.MaxCachedEntries.ShouldBe(64);
	}

	#endregion

	#region Property Setter Tests

	[Fact]
	public void EnableCaching_CanBeSet()
	{
		// Arrange
		var options = new FilteredInvokerOptions();

		// Act
		options.EnableCaching = false;

		// Assert
		options.EnableCaching.ShouldBeFalse();
	}

	[Fact]
	public void IncludeMiddlewareOnFilterError_CanBeSet()
	{
		// Arrange
		var options = new FilteredInvokerOptions();

		// Act
		options.IncludeMiddlewareOnFilterError = true;

		// Assert
		options.IncludeMiddlewareOnFilterError.ShouldBeTrue();
	}

	[Fact]
	public void MaxCachedEntries_CanBeSet()
	{
		// Arrange
		var options = new FilteredInvokerOptions();

		// Act
		options.MaxCachedEntries = 128;

		// Assert
		options.MaxCachedEntries.ShouldBe(128);
	}

	#endregion

	#region Object Initializer Tests

	[Fact]
	public void ObjectInitializer_SetsAllProperties()
	{
		// Act
		var options = new FilteredInvokerOptions
		{
			EnableCaching = false,
			IncludeMiddlewareOnFilterError = true,
			MaxCachedEntries = 256,
		};

		// Assert
		options.EnableCaching.ShouldBeFalse();
		options.IncludeMiddlewareOnFilterError.ShouldBeTrue();
		options.MaxCachedEntries.ShouldBe(256);
	}

	#endregion

	#region Real-World Scenario Tests

	[Fact]
	public void Options_ForThroughput_EnablesCaching()
	{
		// Act
		var options = new FilteredInvokerOptions
		{
			EnableCaching = true,
			MaxCachedEntries = 256,
		};

		// Assert
		options.EnableCaching.ShouldBeTrue();
		options.MaxCachedEntries.ShouldBeGreaterThan(64);
	}

	[Fact]
	public void Options_ForFailSafe_DisablesMiddlewareOnError()
	{
		// Act
		var options = new FilteredInvokerOptions
		{
			IncludeMiddlewareOnFilterError = false,
		};

		// Assert
		options.IncludeMiddlewareOnFilterError.ShouldBeFalse();
	}

	[Fact]
	public void Options_ForMinimalMemory_HasSmallCache()
	{
		// Act
		var options = new FilteredInvokerOptions
		{
			MaxCachedEntries = 16,
			EnableCaching = true,
		};

		// Assert
		options.MaxCachedEntries.ShouldBeLessThan(64);
	}

	#endregion
}
