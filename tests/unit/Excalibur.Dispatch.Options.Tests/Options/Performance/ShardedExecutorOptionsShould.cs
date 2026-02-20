// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Options.Performance;

namespace Excalibur.Dispatch.Tests.Options.Performance;

/// <summary>
/// Unit tests for <see cref="ShardedExecutorOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Options")]
[Trait("Priority", "0")]
public sealed class ShardedExecutorOptionsShould
{
	#region Default Value Tests

	[Fact]
	public void Default_ShardCount_IsZero()
	{
		// Arrange & Act
		var options = new ShardedExecutorOptions();

		// Assert
		options.ShardCount.ShouldBe(0);
	}

	[Fact]
	public void Default_MaxQueueDepth_Is1000()
	{
		// Arrange & Act
		var options = new ShardedExecutorOptions();

		// Assert
		options.MaxQueueDepth.ShouldBe(1000);
	}

	[Fact]
	public void Default_EnableCpuAffinity_IsTrue()
	{
		// Arrange & Act
		var options = new ShardedExecutorOptions();

		// Assert
		options.EnableCpuAffinity.ShouldBeTrue();
	}

	[Fact]
	public void Default_EnableMetrics_IsTrue()
	{
		// Arrange & Act
		var options = new ShardedExecutorOptions();

		// Assert
		options.EnableMetrics.ShouldBeTrue();
	}

	#endregion

	#region Property Setter Tests

	[Fact]
	public void ShardCount_CanBeSet()
	{
		// Arrange
		var options = new ShardedExecutorOptions();

		// Act
		options.ShardCount = 16;

		// Assert
		options.ShardCount.ShouldBe(16);
	}

	[Fact]
	public void MaxQueueDepth_CanBeSet()
	{
		// Arrange
		var options = new ShardedExecutorOptions();

		// Act
		options.MaxQueueDepth = 5000;

		// Assert
		options.MaxQueueDepth.ShouldBe(5000);
	}

	[Fact]
	public void EnableCpuAffinity_CanBeSet()
	{
		// Arrange
		var options = new ShardedExecutorOptions();

		// Act
		options.EnableCpuAffinity = false;

		// Assert
		options.EnableCpuAffinity.ShouldBeFalse();
	}

	[Fact]
	public void EnableMetrics_CanBeSet()
	{
		// Arrange
		var options = new ShardedExecutorOptions();

		// Act
		options.EnableMetrics = false;

		// Assert
		options.EnableMetrics.ShouldBeFalse();
	}

	#endregion

	#region Object Initializer Tests

	[Fact]
	public void ObjectInitializer_SetsAllProperties()
	{
		// Act
		var options = new ShardedExecutorOptions
		{
			ShardCount = 8,
			MaxQueueDepth = 2000,
			EnableCpuAffinity = false,
			EnableMetrics = false,
		};

		// Assert
		options.ShardCount.ShouldBe(8);
		options.MaxQueueDepth.ShouldBe(2000);
		options.EnableCpuAffinity.ShouldBeFalse();
		options.EnableMetrics.ShouldBeFalse();
	}

	#endregion

	#region Real-World Scenario Tests

	[Fact]
	public void Options_ForHighThroughput_HasManyShards()
	{
		// Act
		var options = new ShardedExecutorOptions
		{
			ShardCount = Environment.ProcessorCount * 2,
			MaxQueueDepth = 5000,
			EnableCpuAffinity = true,
			EnableMetrics = true,
		};

		// Assert
		options.ShardCount.ShouldBeGreaterThan(Environment.ProcessorCount);
		options.MaxQueueDepth.ShouldBeGreaterThan(1000);
	}

	[Fact]
	public void Options_ForMinimalResources_HasFewShards()
	{
		// Act
		var options = new ShardedExecutorOptions
		{
			ShardCount = 2,
			MaxQueueDepth = 100,
			EnableCpuAffinity = false,
			EnableMetrics = false,
		};

		// Assert
		options.ShardCount.ShouldBe(2);
		options.MaxQueueDepth.ShouldBeLessThan(1000);
		options.EnableCpuAffinity.ShouldBeFalse();
	}

	[Fact]
	public void Options_ForNUMAOptimization_EnablesCpuAffinity()
	{
		// Act
		var options = new ShardedExecutorOptions
		{
			EnableCpuAffinity = true,
			ShardCount = Environment.ProcessorCount,
		};

		// Assert
		options.EnableCpuAffinity.ShouldBeTrue();
		options.ShardCount.ShouldBe(Environment.ProcessorCount);
	}

	#endregion
}
