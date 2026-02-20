// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Options.Performance;

namespace Excalibur.Dispatch.Tests.Options.Performance;

/// <summary>
/// Unit tests for <see cref="ZeroAllocOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Options")]
[Trait("Priority", "0")]
public sealed class ZeroAllocOptionsShould
{
	#region Default Value Tests

	[Fact]
	public void Default_ContextPoolSize_Is1024()
	{
		// Arrange & Act
		var options = new ZeroAllocOptions();

		// Assert
		options.ContextPoolSize.ShouldBe(1024);
	}

	[Fact]
	public void Default_MaxBufferSize_Is1MB()
	{
		// Arrange & Act
		var options = new ZeroAllocOptions();

		// Assert
		options.MaxBufferSize.ShouldBe(1024 * 1024);
	}

	[Fact]
	public void Default_MaxBuffersPerBucket_Is50()
	{
		// Arrange & Act
		var options = new ZeroAllocOptions();

		// Assert
		options.MaxBuffersPerBucket.ShouldBe(50);
	}

	[Fact]
	public void Default_EnableAggressiveInlining_IsTrue()
	{
		// Arrange & Act
		var options = new ZeroAllocOptions();

		// Assert
		options.EnableAggressiveInlining.ShouldBeTrue();
	}

	[Fact]
	public void Default_UseStructResults_IsTrue()
	{
		// Arrange & Act
		var options = new ZeroAllocOptions();

		// Assert
		options.UseStructResults.ShouldBeTrue();
	}

	[Fact]
	public void Default_PreCompileHandlers_IsTrue()
	{
		// Arrange & Act
		var options = new ZeroAllocOptions();

		// Assert
		options.PreCompileHandlers.ShouldBeTrue();
	}

	#endregion

	#region Property Setter Tests

	[Fact]
	public void ContextPoolSize_CanBeSet()
	{
		// Arrange
		var options = new ZeroAllocOptions();

		// Act
		options.ContextPoolSize = 2048;

		// Assert
		options.ContextPoolSize.ShouldBe(2048);
	}

	[Fact]
	public void MaxBufferSize_CanBeSet()
	{
		// Arrange
		var options = new ZeroAllocOptions();

		// Act
		options.MaxBufferSize = 2 * 1024 * 1024;

		// Assert
		options.MaxBufferSize.ShouldBe(2 * 1024 * 1024);
	}

	[Fact]
	public void MaxBuffersPerBucket_CanBeSet()
	{
		// Arrange
		var options = new ZeroAllocOptions();

		// Act
		options.MaxBuffersPerBucket = 100;

		// Assert
		options.MaxBuffersPerBucket.ShouldBe(100);
	}

	[Fact]
	public void EnableAggressiveInlining_CanBeSet()
	{
		// Arrange
		var options = new ZeroAllocOptions();

		// Act
		options.EnableAggressiveInlining = false;

		// Assert
		options.EnableAggressiveInlining.ShouldBeFalse();
	}

	[Fact]
	public void UseStructResults_CanBeSet()
	{
		// Arrange
		var options = new ZeroAllocOptions();

		// Act
		options.UseStructResults = false;

		// Assert
		options.UseStructResults.ShouldBeFalse();
	}

	[Fact]
	public void PreCompileHandlers_CanBeSet()
	{
		// Arrange
		var options = new ZeroAllocOptions();

		// Act
		options.PreCompileHandlers = false;

		// Assert
		options.PreCompileHandlers.ShouldBeFalse();
	}

	#endregion

	#region Object Initializer Tests

	[Fact]
	public void ObjectInitializer_SetsAllProperties()
	{
		// Act
		var options = new ZeroAllocOptions
		{
			ContextPoolSize = 512,
			MaxBufferSize = 512 * 1024,
			MaxBuffersPerBucket = 25,
			EnableAggressiveInlining = false,
			UseStructResults = false,
			PreCompileHandlers = false,
		};

		// Assert
		options.ContextPoolSize.ShouldBe(512);
		options.MaxBufferSize.ShouldBe(512 * 1024);
		options.MaxBuffersPerBucket.ShouldBe(25);
		options.EnableAggressiveInlining.ShouldBeFalse();
		options.UseStructResults.ShouldBeFalse();
		options.PreCompileHandlers.ShouldBeFalse();
	}

	#endregion

	#region Real-World Scenario Tests

	[Fact]
	public void Options_ForMaxPerformance_HasLargePools()
	{
		// Act
		var options = new ZeroAllocOptions
		{
			ContextPoolSize = 4096,
			MaxBufferSize = 4 * 1024 * 1024,
			MaxBuffersPerBucket = 100,
			EnableAggressiveInlining = true,
			UseStructResults = true,
			PreCompileHandlers = true,
		};

		// Assert
		options.ContextPoolSize.ShouldBeGreaterThan(1024);
		options.MaxBufferSize.ShouldBeGreaterThan(1024 * 1024);
		options.EnableAggressiveInlining.ShouldBeTrue();
	}

	[Fact]
	public void Options_ForMemoryConstrained_HasSmallPools()
	{
		// Act
		var options = new ZeroAllocOptions
		{
			ContextPoolSize = 256,
			MaxBufferSize = 256 * 1024,
			MaxBuffersPerBucket = 10,
		};

		// Assert
		options.ContextPoolSize.ShouldBeLessThan(1024);
		options.MaxBufferSize.ShouldBeLessThan(1024 * 1024);
		options.MaxBuffersPerBucket.ShouldBeLessThan(50);
	}

	[Fact]
	public void Options_ForDebugging_DisablesOptimizations()
	{
		// Act
		var options = new ZeroAllocOptions
		{
			EnableAggressiveInlining = false,
			UseStructResults = false,
			PreCompileHandlers = false,
		};

		// Assert
		options.EnableAggressiveInlining.ShouldBeFalse();
		options.UseStructResults.ShouldBeFalse();
		options.PreCompileHandlers.ShouldBeFalse();
	}

	#endregion
}
