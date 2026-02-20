// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Options.Performance;

namespace Excalibur.Dispatch.Tests.Options.Performance;

/// <summary>
/// Unit tests for <see cref="LeakTrackingOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Options")]
[Trait("Priority", "0")]
public sealed class LeakTrackingOptionsShould
{
	#region Default Value Tests

	[Fact]
	public void Default_MaximumRetained_IsProcessorCountTimesTwo()
	{
		// Arrange & Act
		var options = new LeakTrackingOptions();

		// Assert
		options.MaximumRetained.ShouldBe(Environment.ProcessorCount * 2);
	}

	[Fact]
	public void Default_MinimumRetained_IsProcessorCount()
	{
		// Arrange & Act
		var options = new LeakTrackingOptions();

		// Assert
		options.MinimumRetained.ShouldBe(Environment.ProcessorCount);
	}

	[Fact]
	public void Default_LeakTimeout_IsFiveMinutes()
	{
		// Arrange & Act
		var options = new LeakTrackingOptions();

		// Assert
		options.LeakTimeout.ShouldBe(TimeSpan.FromMinutes(5));
	}

	[Fact]
	public void Default_LeakDetectionInterval_IsThirtySeconds()
	{
		// Arrange & Act
		var options = new LeakTrackingOptions();

		// Assert
		options.LeakDetectionInterval.ShouldBe(TimeSpan.FromSeconds(30));
	}

	[Fact]
	public void Default_TrackStackTraces_IsFalse()
	{
		// Arrange & Act
		var options = new LeakTrackingOptions();

		// Assert
		options.TrackStackTraces.ShouldBeFalse();
	}

	#endregion

	#region Property Setter Tests

	[Fact]
	public void MaximumRetained_CanBeSet()
	{
		// Arrange
		var options = new LeakTrackingOptions();

		// Act
		options.MaximumRetained = 100;

		// Assert
		options.MaximumRetained.ShouldBe(100);
	}

	[Fact]
	public void MinimumRetained_CanBeSet()
	{
		// Arrange
		var options = new LeakTrackingOptions();

		// Act
		options.MinimumRetained = 10;

		// Assert
		options.MinimumRetained.ShouldBe(10);
	}

	[Fact]
	public void LeakTimeout_CanBeSet()
	{
		// Arrange
		var options = new LeakTrackingOptions();

		// Act
		options.LeakTimeout = TimeSpan.FromMinutes(10);

		// Assert
		options.LeakTimeout.ShouldBe(TimeSpan.FromMinutes(10));
	}

	[Fact]
	public void LeakDetectionInterval_CanBeSet()
	{
		// Arrange
		var options = new LeakTrackingOptions();

		// Act
		options.LeakDetectionInterval = TimeSpan.FromSeconds(60);

		// Assert
		options.LeakDetectionInterval.ShouldBe(TimeSpan.FromSeconds(60));
	}

	[Fact]
	public void TrackStackTraces_CanBeSet()
	{
		// Arrange
		var options = new LeakTrackingOptions();

		// Act
		options.TrackStackTraces = true;

		// Assert
		options.TrackStackTraces.ShouldBeTrue();
	}

	#endregion

	#region Object Initializer Tests

	[Fact]
	public void ObjectInitializer_SetsAllProperties()
	{
		// Act
		var options = new LeakTrackingOptions
		{
			MaximumRetained = 50,
			MinimumRetained = 5,
			LeakTimeout = TimeSpan.FromMinutes(2),
			LeakDetectionInterval = TimeSpan.FromSeconds(15),
			TrackStackTraces = true,
		};

		// Assert
		options.MaximumRetained.ShouldBe(50);
		options.MinimumRetained.ShouldBe(5);
		options.LeakTimeout.ShouldBe(TimeSpan.FromMinutes(2));
		options.LeakDetectionInterval.ShouldBe(TimeSpan.FromSeconds(15));
		options.TrackStackTraces.ShouldBeTrue();
	}

	#endregion

	#region Real-World Scenario Tests

	[Fact]
	public void Options_ForDebugging_EnablesStackTraces()
	{
		// Act
		var options = new LeakTrackingOptions
		{
			TrackStackTraces = true,
			LeakTimeout = TimeSpan.FromMinutes(1),
			LeakDetectionInterval = TimeSpan.FromSeconds(10),
		};

		// Assert
		options.TrackStackTraces.ShouldBeTrue();
		options.LeakTimeout.ShouldBeLessThan(TimeSpan.FromMinutes(5));
		options.LeakDetectionInterval.ShouldBeLessThan(TimeSpan.FromSeconds(30));
	}

	[Fact]
	public void Options_ForProduction_HasConservativeSettings()
	{
		// Act
		var options = new LeakTrackingOptions
		{
			TrackStackTraces = false,
			LeakTimeout = TimeSpan.FromMinutes(10),
			LeakDetectionInterval = TimeSpan.FromMinutes(1),
		};

		// Assert
		options.TrackStackTraces.ShouldBeFalse();
		options.LeakTimeout.ShouldBeGreaterThan(TimeSpan.FromMinutes(5));
	}

	[Fact]
	public void Options_ForHighVolume_HasLargeRetentionLimits()
	{
		// Act
		var options = new LeakTrackingOptions
		{
			MaximumRetained = 1000,
			MinimumRetained = 100,
		};

		// Assert
		options.MaximumRetained.ShouldBeGreaterThan(Environment.ProcessorCount * 2);
		options.MinimumRetained.ShouldBeGreaterThan(Environment.ProcessorCount);
	}

	#endregion
}
