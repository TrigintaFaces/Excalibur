// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Options.Threading;

namespace Excalibur.Dispatch.Tests.Options.Threading;

/// <summary>
/// Unit tests for <see cref="ThreadingOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Options")]
[Trait("Priority", "0")]
public sealed class ThreadingOptionsShould
{
	#region Default Value Tests

	[Fact]
	public void Default_DefaultMaxDegreeOfParallelism_IsZero()
	{
		// Arrange & Act
		var options = new ThreadingOptions();

		// Assert
		options.DefaultMaxDegreeOfParallelism.ShouldBe(0);
	}

	[Fact]
	public void Default_PrefetchBufferSize_IsZero()
	{
		// Arrange & Act
		var options = new ThreadingOptions();

		// Assert
		options.PrefetchBufferSize.ShouldBe(0);
	}

	#endregion

	#region Property Setter Tests

	[Fact]
	public void DefaultMaxDegreeOfParallelism_CanBeSet()
	{
		// Arrange
		var options = new ThreadingOptions();

		// Act
		options.DefaultMaxDegreeOfParallelism = 8;

		// Assert
		options.DefaultMaxDegreeOfParallelism.ShouldBe(8);
	}

	[Fact]
	public void PrefetchBufferSize_CanBeSet()
	{
		// Arrange
		var options = new ThreadingOptions();

		// Act
		options.PrefetchBufferSize = 100;

		// Assert
		options.PrefetchBufferSize.ShouldBe(100);
	}

	#endregion

	#region Object Initializer Tests

	[Fact]
	public void ObjectInitializer_SetsAllProperties()
	{
		// Act
		var options = new ThreadingOptions
		{
			DefaultMaxDegreeOfParallelism = 16,
			PrefetchBufferSize = 200,
		};

		// Assert
		options.DefaultMaxDegreeOfParallelism.ShouldBe(16);
		options.PrefetchBufferSize.ShouldBe(200);
	}

	#endregion

	#region Real-World Scenario Tests

	[Fact]
	public void Options_ForCpuBoundWorkload_UsesProcessorCount()
	{
		// Act
		var options = new ThreadingOptions
		{
			DefaultMaxDegreeOfParallelism = Environment.ProcessorCount,
			PrefetchBufferSize = Environment.ProcessorCount * 2,
		};

		// Assert
		options.DefaultMaxDegreeOfParallelism.ShouldBe(Environment.ProcessorCount);
		options.PrefetchBufferSize.ShouldBeGreaterThan(options.DefaultMaxDegreeOfParallelism);
	}

	[Fact]
	public void Options_ForIoBoundWorkload_UsesHigherParallelism()
	{
		// Act
		var options = new ThreadingOptions
		{
			DefaultMaxDegreeOfParallelism = Environment.ProcessorCount * 4,
			PrefetchBufferSize = Environment.ProcessorCount * 8,
		};

		// Assert
		options.DefaultMaxDegreeOfParallelism.ShouldBeGreaterThan(Environment.ProcessorCount);
	}

	[Fact]
	public void Options_ForLimitedResources_UsesConservativeSettings()
	{
		// Act
		var options = new ThreadingOptions
		{
			DefaultMaxDegreeOfParallelism = 2,
			PrefetchBufferSize = 5,
		};

		// Assert
		options.DefaultMaxDegreeOfParallelism.ShouldBeLessThanOrEqualTo(2);
		options.PrefetchBufferSize.ShouldBeLessThanOrEqualTo(5);
	}

	#endregion
}
