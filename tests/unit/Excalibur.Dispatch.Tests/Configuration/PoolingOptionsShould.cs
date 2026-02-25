// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Pooling.Configuration;
using Excalibur.Dispatch.Options.Pooling;

namespace Excalibur.Dispatch.Tests.Configuration;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class PoolingOptionsShould
{
	// --- PoolOptions ---

	[Fact]
	public void PoolOptions_DefaultValues_AreCorrect()
	{
		// Act
		var options = new PoolOptions();

		// Assert
		options.BufferPool.ShouldNotBeNull();
		options.MessagePool.ShouldNotBeNull();
		options.Global.ShouldNotBeNull();
	}

	[Fact]
	public void PoolOptions_SubOptions_AreSettable()
	{
		// Arrange
		var bufferPool = new BufferPoolOptions { Enabled = false };
		var messagePool = new MessagePoolOptions { Enabled = false };
		var global = new GlobalPoolOptions { EnableTelemetry = true };

		// Act
		var options = new PoolOptions
		{
			BufferPool = bufferPool,
			MessagePool = messagePool,
			Global = global,
		};

		// Assert
		options.BufferPool.Enabled.ShouldBeFalse();
		options.MessagePool.Enabled.ShouldBeFalse();
		options.Global.EnableTelemetry.ShouldBeTrue();
	}

	// --- BufferPoolOptions ---

	[Fact]
	public void BufferPoolOptions_DefaultValues_AreCorrect()
	{
		// Act
		var options = new BufferPoolOptions();

		// Assert
		options.Enabled.ShouldBeTrue();
		options.SizeBuckets.ShouldNotBeNull();
		options.MaxBuffersPerBucket.ShouldBe(Environment.ProcessorCount * 4);
		options.ClearOnReturn.ShouldBeFalse();
		options.EnableThreadLocalCache.ShouldBeTrue();
		options.ThreadLocalCacheSize.ShouldBe(2);
		options.TrimBehavior.ShouldBe(TrimBehavior.Adaptive);
		options.TrimPercentage.ShouldBe(50);
	}

	[Fact]
	public void BufferPoolOptions_AllProperties_AreSettable()
	{
		// Act
		var options = new BufferPoolOptions
		{
			Enabled = false,
			SizeBuckets = new SizeBucketOptions { TinySize = 32 },
			MaxBuffersPerBucket = 100,
			ClearOnReturn = true,
			EnableThreadLocalCache = false,
			ThreadLocalCacheSize = 4,
			TrimBehavior = TrimBehavior.Aggressive,
			TrimPercentage = 75,
		};

		// Assert
		options.Enabled.ShouldBeFalse();
		options.SizeBuckets.TinySize.ShouldBe(32);
		options.MaxBuffersPerBucket.ShouldBe(100);
		options.ClearOnReturn.ShouldBeTrue();
		options.EnableThreadLocalCache.ShouldBeFalse();
		options.ThreadLocalCacheSize.ShouldBe(4);
		options.TrimBehavior.ShouldBe(TrimBehavior.Aggressive);
		options.TrimPercentage.ShouldBe(75);
	}

	// --- SizeBucketOptions ---

	[Fact]
	public void SizeBucketOptions_DefaultValues_AreCorrect()
	{
		// Act
		var options = new SizeBucketOptions();

		// Assert
		options.TinySize.ShouldBe(64);
		options.SmallSize.ShouldBe(256);
		options.MediumSize.ShouldBe(4096);
		options.LargeSize.ShouldBe(65536);
		options.HugeSize.ShouldBe(1048576);
	}

	[Fact]
	public void SizeBucketOptions_AllProperties_AreSettable()
	{
		// Act
		var options = new SizeBucketOptions
		{
			TinySize = 32,
			SmallSize = 128,
			MediumSize = 2048,
			LargeSize = 32768,
			HugeSize = 524288,
		};

		// Assert
		options.TinySize.ShouldBe(32);
		options.SmallSize.ShouldBe(128);
		options.MediumSize.ShouldBe(2048);
		options.LargeSize.ShouldBe(32768);
		options.HugeSize.ShouldBe(524288);
	}

	// --- MessagePoolOptions ---

	[Fact]
	public void MessagePoolOptions_DefaultValues_AreCorrect()
	{
		// Act
		var options = new MessagePoolOptions();

		// Assert
		options.Enabled.ShouldBeTrue();
		options.MaxPoolSizePerType.ShouldBe(Environment.ProcessorCount * 8);
		options.AggressivePooling.ShouldBeTrue();
		options.TypeConfigurations.ShouldNotBeNull();
		options.TypeConfigurations.ShouldBeEmpty();
		options.DefaultResetStrategy.ShouldBe(ResetStrategy.Auto);
		options.TrimBehavior.ShouldBe(TrimBehavior.Adaptive);
		options.MaxTrackedTypes.ShouldBe(100);
	}

	[Fact]
	public void MessagePoolOptions_AllProperties_AreSettable()
	{
		// Act
		var options = new MessagePoolOptions
		{
			Enabled = false,
			MaxPoolSizePerType = 32,
			AggressivePooling = false,
			DefaultResetStrategy = ResetStrategy.Interface,
			TrimBehavior = TrimBehavior.Fixed,
			MaxTrackedTypes = 50,
		};

		// Assert
		options.Enabled.ShouldBeFalse();
		options.MaxPoolSizePerType.ShouldBe(32);
		options.AggressivePooling.ShouldBeFalse();
		options.DefaultResetStrategy.ShouldBe(ResetStrategy.Interface);
		options.TrimBehavior.ShouldBe(TrimBehavior.Fixed);
		options.MaxTrackedTypes.ShouldBe(50);
	}

	[Fact]
	public void MessagePoolOptions_TypeConfigurations_CanAddEntries()
	{
		// Arrange
		var options = new MessagePoolOptions();

		// Act
		options.TypeConfigurations["OrderCreated"] = new TypePoolOptions { MaxPoolSize = 50 };

		// Assert
		options.TypeConfigurations.Count.ShouldBe(1);
		options.TypeConfigurations["OrderCreated"].MaxPoolSize.ShouldBe(50);
	}

	// --- TypePoolOptions ---

	[Fact]
	public void TypePoolOptions_DefaultValues_AreCorrect()
	{
		// Act
		var options = new TypePoolOptions();

		// Assert
		options.MaxPoolSize.ShouldBe(0);
		options.Enabled.ShouldBeTrue();
		options.ResetStrategy.ShouldBe(ResetStrategy.Auto);
		options.PreWarm.ShouldBeFalse();
		options.PreWarmCount.ShouldBe(0);
	}

	[Fact]
	public void TypePoolOptions_AllProperties_AreSettable()
	{
		// Act
		var options = new TypePoolOptions
		{
			MaxPoolSize = 100,
			Enabled = false,
			ResetStrategy = ResetStrategy.SourceGenerated,
			PreWarm = true,
			PreWarmCount = 10,
		};

		// Assert
		options.MaxPoolSize.ShouldBe(100);
		options.Enabled.ShouldBeFalse();
		options.ResetStrategy.ShouldBe(ResetStrategy.SourceGenerated);
		options.PreWarm.ShouldBeTrue();
		options.PreWarmCount.ShouldBe(10);
	}

	// --- GlobalPoolOptions ---

	[Fact]
	public void GlobalPoolOptions_DefaultValues_AreCorrect()
	{
		// Act
		var options = new GlobalPoolOptions();

		// Assert
		options.EnableTelemetry.ShouldBeFalse();
		options.EnableDetailedMetrics.ShouldBeFalse();
		options.EnableDiagnostics.ShouldBeTrue();
		options.DiagnosticsInterval.ShouldBe(TimeSpan.FromMinutes(5));
		options.MemoryPressureThreshold.ShouldBe(0.8);
		options.EnableAdaptiveSizing.ShouldBeTrue();
		options.AdaptationInterval.ShouldBe(TimeSpan.FromMinutes(1));
	}

	[Fact]
	public void GlobalPoolOptions_AllProperties_AreSettable()
	{
		// Act
		var options = new GlobalPoolOptions
		{
			EnableTelemetry = true,
			EnableDetailedMetrics = true,
			EnableDiagnostics = false,
			DiagnosticsInterval = TimeSpan.FromMinutes(10),
			MemoryPressureThreshold = 0.9,
			EnableAdaptiveSizing = false,
			AdaptationInterval = TimeSpan.FromMinutes(5),
		};

		// Assert
		options.EnableTelemetry.ShouldBeTrue();
		options.EnableDetailedMetrics.ShouldBeTrue();
		options.EnableDiagnostics.ShouldBeFalse();
		options.DiagnosticsInterval.ShouldBe(TimeSpan.FromMinutes(10));
		options.MemoryPressureThreshold.ShouldBe(0.9);
		options.EnableAdaptiveSizing.ShouldBeFalse();
		options.AdaptationInterval.ShouldBe(TimeSpan.FromMinutes(5));
	}

	// --- TrimBehavior ---

	[Fact]
	public void TrimBehavior_HaveExpectedValues()
	{
		// Assert
		TrimBehavior.None.ShouldBe((TrimBehavior)0);
		TrimBehavior.Fixed.ShouldBe((TrimBehavior)1);
		TrimBehavior.Adaptive.ShouldBe((TrimBehavior)2);
		TrimBehavior.Aggressive.ShouldBe((TrimBehavior)3);
	}

	[Fact]
	public void TrimBehavior_HaveFourValues()
	{
		// Act
		var values = Enum.GetValues<TrimBehavior>();

		// Assert
		values.Length.ShouldBe(4);
	}

	// --- ResetStrategy ---

	[Fact]
	public void ResetStrategy_HaveExpectedValues()
	{
		// Assert
		ResetStrategy.Auto.ShouldBe((ResetStrategy)0);
		ResetStrategy.SourceGenerated.ShouldBe((ResetStrategy)1);
		ResetStrategy.Interface.ShouldBe((ResetStrategy)2);
		ResetStrategy.None.ShouldBe((ResetStrategy)3);
		ResetStrategy.Disabled.ShouldBe((ResetStrategy)4);
	}

	[Fact]
	public void ResetStrategy_HaveFiveValues()
	{
		// Act
		var values = Enum.GetValues<ResetStrategy>();

		// Assert
		values.Length.ShouldBe(5);
	}
}
