// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Options.Configuration;

namespace Excalibur.Dispatch.Tests.Options.Configuration;

/// <summary>
/// Unit tests for <see cref="PerformanceOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Options")]
[Trait("Priority", "0")]
public sealed class PerformanceOptionsShould
{
	#region Default Value Tests

	[Fact]
	public void Default_EnableCacheMiddleware_IsTrue()
	{
		// Arrange & Act
		var options = new PerformanceOptions();

		// Assert
		options.EnableCacheMiddleware.ShouldBeTrue();
	}

	[Fact]
	public void Default_EnableTypeMetadataCaching_IsTrue()
	{
		// Arrange & Act
		var options = new PerformanceOptions();

		// Assert
		options.EnableTypeMetadataCaching.ShouldBeTrue();
	}

	[Fact]
	public void Default_MessagePoolSize_IsOneThousand()
	{
		// Arrange & Act
		var options = new PerformanceOptions();

		// Assert
		options.MessagePoolSize.ShouldBe(1000);
	}

	[Fact]
	public void Default_UseAllocationFreeExecution_IsTrue()
	{
		// Arrange & Act
		var options = new PerformanceOptions();

		// Assert
		options.UseAllocationFreeExecution.ShouldBeTrue();
	}

	[Fact]
	public void Default_AutoFreezeOnStart_IsTrue()
	{
		// Arrange & Act
		var options = new PerformanceOptions();

		// Assert
		options.AutoFreezeOnStart.ShouldBeTrue();
	}

	#endregion

	#region Property Setter Tests

	[Fact]
	public void EnableCacheMiddleware_CanBeSet()
	{
		// Arrange
		var options = new PerformanceOptions();

		// Act
		options.EnableCacheMiddleware = false;

		// Assert
		options.EnableCacheMiddleware.ShouldBeFalse();
	}

	[Fact]
	public void EnableTypeMetadataCaching_CanBeSet()
	{
		// Arrange
		var options = new PerformanceOptions();

		// Act
		options.EnableTypeMetadataCaching = false;

		// Assert
		options.EnableTypeMetadataCaching.ShouldBeFalse();
	}

	[Fact]
	public void MessagePoolSize_CanBeSet()
	{
		// Arrange
		var options = new PerformanceOptions();

		// Act
		options.MessagePoolSize = 5000;

		// Assert
		options.MessagePoolSize.ShouldBe(5000);
	}

	[Fact]
	public void UseAllocationFreeExecution_CanBeSet()
	{
		// Arrange
		var options = new PerformanceOptions();

		// Act
		options.UseAllocationFreeExecution = false;

		// Assert
		options.UseAllocationFreeExecution.ShouldBeFalse();
	}

	[Fact]
	public void AutoFreezeOnStart_CanBeSet()
	{
		// Arrange
		var options = new PerformanceOptions();

		// Act
		options.AutoFreezeOnStart = false;

		// Assert
		options.AutoFreezeOnStart.ShouldBeFalse();
	}

	#endregion

	#region Object Initializer Tests

	[Fact]
	public void ObjectInitializer_SetsAllProperties()
	{
		// Act
		var options = new PerformanceOptions
		{
			EnableCacheMiddleware = false,
			EnableTypeMetadataCaching = false,
			MessagePoolSize = 2000,
			UseAllocationFreeExecution = false,
			AutoFreezeOnStart = false,
		};

		// Assert
		options.EnableCacheMiddleware.ShouldBeFalse();
		options.EnableTypeMetadataCaching.ShouldBeFalse();
		options.MessagePoolSize.ShouldBe(2000);
		options.UseAllocationFreeExecution.ShouldBeFalse();
		options.AutoFreezeOnStart.ShouldBeFalse();
	}

	#endregion

	#region Real-World Scenario Tests

	[Fact]
	public void Options_ForProduction_HasAllOptimizationsEnabled()
	{
		// Act
		var options = new PerformanceOptions
		{
			EnableCacheMiddleware = true,
			EnableTypeMetadataCaching = true,
			UseAllocationFreeExecution = true,
			AutoFreezeOnStart = true,
		};

		// Assert
		options.EnableCacheMiddleware.ShouldBeTrue();
		options.UseAllocationFreeExecution.ShouldBeTrue();
		options.AutoFreezeOnStart.ShouldBeTrue();
	}

	[Fact]
	public void Options_ForDevelopment_DisablesAutoFreeze()
	{
		// Act
		var options = new PerformanceOptions
		{
			AutoFreezeOnStart = false,
		};

		// Assert
		options.AutoFreezeOnStart.ShouldBeFalse();
	}

	[Fact]
	public void Options_ForHighThroughput_HasLargePoolSize()
	{
		// Act
		var options = new PerformanceOptions
		{
			MessagePoolSize = 10000,
			UseAllocationFreeExecution = true,
		};

		// Assert
		options.MessagePoolSize.ShouldBeGreaterThan(5000);
		options.UseAllocationFreeExecution.ShouldBeTrue();
	}

	#endregion
}
