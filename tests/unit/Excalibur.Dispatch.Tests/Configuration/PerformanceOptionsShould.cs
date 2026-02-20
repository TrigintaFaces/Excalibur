// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Options.Performance;
using Excalibur.Dispatch.Options.Threading;

namespace Excalibur.Dispatch.Tests.Configuration;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class PerformanceOptionsShould
{
	// --- LeakTrackingOptions ---

	[Fact]
	public void LeakTrackingOptions_DefaultValues_AreCorrect()
	{
		// Act
		var options = new LeakTrackingOptions();

		// Assert
		options.MaximumRetained.ShouldBe(Environment.ProcessorCount * 2);
		options.MinimumRetained.ShouldBe(Environment.ProcessorCount);
		options.LeakTimeout.ShouldBe(TimeSpan.FromMinutes(5));
		options.LeakDetectionInterval.ShouldBe(TimeSpan.FromSeconds(30));
		options.TrackStackTraces.ShouldBeFalse();
	}

	[Fact]
	public void LeakTrackingOptions_AllProperties_AreSettable()
	{
		// Act
		var options = new LeakTrackingOptions
		{
			MaximumRetained = 100,
			MinimumRetained = 10,
			LeakTimeout = TimeSpan.FromMinutes(10),
			LeakDetectionInterval = TimeSpan.FromMinutes(1),
			TrackStackTraces = true,
		};

		// Assert
		options.MaximumRetained.ShouldBe(100);
		options.MinimumRetained.ShouldBe(10);
		options.LeakTimeout.ShouldBe(TimeSpan.FromMinutes(10));
		options.LeakDetectionInterval.ShouldBe(TimeSpan.FromMinutes(1));
		options.TrackStackTraces.ShouldBeTrue();
	}

	// --- MicroBatchOptions ---

	[Fact]
	public void MicroBatchOptions_DefaultValues_AreCorrect()
	{
		// Act
		var options = new MicroBatchOptions();

		// Assert
		options.MaxBatchSize.ShouldBe(100);
		options.MaxBatchDelay.ShouldBe(TimeSpan.FromMilliseconds(100));
	}

	[Fact]
	public void MicroBatchOptions_AllProperties_AreSettable()
	{
		// Act
		var options = new MicroBatchOptions
		{
			MaxBatchSize = 500,
			MaxBatchDelay = TimeSpan.FromMilliseconds(500),
		};

		// Assert
		options.MaxBatchSize.ShouldBe(500);
		options.MaxBatchDelay.ShouldBe(TimeSpan.FromMilliseconds(500));
	}

	// --- ShardedExecutorOptions ---

	[Fact]
	public void ShardedExecutorOptions_DefaultValues_AreCorrect()
	{
		// Act
		var options = new ShardedExecutorOptions();

		// Assert
		options.ShardCount.ShouldBe(0);
		options.MaxQueueDepth.ShouldBe(1000);
		options.EnableCpuAffinity.ShouldBeTrue();
		options.EnableMetrics.ShouldBeTrue();
	}

	[Fact]
	public void ShardedExecutorOptions_AllProperties_AreSettable()
	{
		// Act
		var options = new ShardedExecutorOptions
		{
			ShardCount = 8,
			MaxQueueDepth = 5000,
			EnableCpuAffinity = false,
			EnableMetrics = false,
		};

		// Assert
		options.ShardCount.ShouldBe(8);
		options.MaxQueueDepth.ShouldBe(5000);
		options.EnableCpuAffinity.ShouldBeFalse();
		options.EnableMetrics.ShouldBeFalse();
	}

	// --- TunedArrayPoolOptions ---

	[Fact]
	public void TunedArrayPoolOptions_DefaultValues_AreCorrect()
	{
		// Act
		var options = new TunedArrayPoolOptions();

		// Assert
		options.PreWarmPools.ShouldBeTrue();
		options.ClearOnReturn.ShouldBeFalse();
		options.MaxArraysPerBucket.ShouldBe(50);
	}

	[Fact]
	public void TunedArrayPoolOptions_AllProperties_AreSettable()
	{
		// Act
		var options = new TunedArrayPoolOptions
		{
			PreWarmPools = false,
			ClearOnReturn = true,
			MaxArraysPerBucket = 100,
		};

		// Assert
		options.PreWarmPools.ShouldBeFalse();
		options.ClearOnReturn.ShouldBeTrue();
		options.MaxArraysPerBucket.ShouldBe(100);
	}

	// --- ZeroAllocOptions ---

	[Fact]
	public void ZeroAllocOptions_DefaultValues_AreCorrect()
	{
		// Act
		var options = new ZeroAllocOptions();

		// Assert
		options.ContextPoolSize.ShouldBe(1024);
		options.MaxBufferSize.ShouldBe(1024 * 1024);
		options.MaxBuffersPerBucket.ShouldBe(50);
		options.EnableAggressiveInlining.ShouldBeTrue();
		options.UseStructResults.ShouldBeTrue();
		options.PreCompileHandlers.ShouldBeTrue();
	}

	[Fact]
	public void ZeroAllocOptions_AllProperties_AreSettable()
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

	// --- ThreadingOptions ---

	[Fact]
	public void ThreadingOptions_DefaultValues_AreCorrect()
	{
		// Act
		var options = new ThreadingOptions();

		// Assert
		options.DefaultMaxDegreeOfParallelism.ShouldBe(0);
		options.PrefetchBufferSize.ShouldBe(0);
	}

	[Fact]
	public void ThreadingOptions_AllProperties_AreSettable()
	{
		// Act
		var options = new ThreadingOptions
		{
			DefaultMaxDegreeOfParallelism = 16,
			PrefetchBufferSize = 64,
		};

		// Assert
		options.DefaultMaxDegreeOfParallelism.ShouldBe(16);
		options.PrefetchBufferSize.ShouldBe(64);
	}
}
