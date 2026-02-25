// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Options.Configuration;

namespace Excalibur.Dispatch.Tests.Options.Configuration;

/// <summary>
/// Unit tests for <see cref="DeduplicationOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Options")]
[Trait("Priority", "0")]
public sealed class DeduplicationOptionsShould
{
	#region Default Value Tests

	[Fact]
	public void Default_Enabled_IsFalse()
	{
		// Arrange & Act
		var options = new DeduplicationOptions();

		// Assert
		options.Enabled.ShouldBeFalse();
	}

	[Fact]
	public void Default_ExpiryHours_IsTwentyFour()
	{
		// Arrange & Act
		var options = new DeduplicationOptions();

		// Assert
		options.ExpiryHours.ShouldBe(24);
	}

	[Fact]
	public void Default_CleanupInterval_IsFiveMinutes()
	{
		// Arrange & Act
		var options = new DeduplicationOptions();

		// Assert
		options.CleanupInterval.ShouldBe(TimeSpan.FromMinutes(5));
	}

	[Fact]
	public void Default_WindowSeconds_IsThreeHundred()
	{
		// Arrange & Act
		var options = new DeduplicationOptions();

		// Assert
		options.WindowSeconds.ShouldBe(300);
	}

	[Fact]
	public void Default_MaxCacheSize_IsTenThousand()
	{
		// Arrange & Act
		var options = new DeduplicationOptions();

		// Assert
		options.MaxCacheSize.ShouldBe(10000);
	}

	#endregion

	#region Property Setter Tests

	[Fact]
	public void Enabled_CanBeSet()
	{
		// Arrange
		var options = new DeduplicationOptions();

		// Act
		options.Enabled = true;

		// Assert
		options.Enabled.ShouldBeTrue();
	}

	[Fact]
	public void ExpiryHours_CanBeSet()
	{
		// Arrange
		var options = new DeduplicationOptions();

		// Act
		options.ExpiryHours = 48;

		// Assert
		options.ExpiryHours.ShouldBe(48);
	}

	[Fact]
	public void CleanupInterval_CanBeSet()
	{
		// Arrange
		var options = new DeduplicationOptions();

		// Act
		options.CleanupInterval = TimeSpan.FromMinutes(15);

		// Assert
		options.CleanupInterval.ShouldBe(TimeSpan.FromMinutes(15));
	}

	[Fact]
	public void WindowSeconds_CanBeSet()
	{
		// Arrange
		var options = new DeduplicationOptions();

		// Act
		options.WindowSeconds = 600;

		// Assert
		options.WindowSeconds.ShouldBe(600);
	}

	[Fact]
	public void MaxCacheSize_CanBeSet()
	{
		// Arrange
		var options = new DeduplicationOptions();

		// Act
		options.MaxCacheSize = 50000;

		// Assert
		options.MaxCacheSize.ShouldBe(50000);
	}

	#endregion

	#region Object Initializer Tests

	[Fact]
	public void ObjectInitializer_SetsAllProperties()
	{
		// Act
		var options = new DeduplicationOptions
		{
			Enabled = true,
			ExpiryHours = 12,
			CleanupInterval = TimeSpan.FromMinutes(10),
			WindowSeconds = 120,
			MaxCacheSize = 5000,
		};

		// Assert
		options.Enabled.ShouldBeTrue();
		options.ExpiryHours.ShouldBe(12);
		options.CleanupInterval.ShouldBe(TimeSpan.FromMinutes(10));
		options.WindowSeconds.ShouldBe(120);
		options.MaxCacheSize.ShouldBe(5000);
	}

	#endregion

	#region Real-World Scenario Tests

	[Fact]
	public void Options_ForHighVolumeScenario_HasLargeCacheSize()
	{
		// Act
		var options = new DeduplicationOptions
		{
			Enabled = true,
			MaxCacheSize = 100000,
			WindowSeconds = 60,
		};

		// Assert
		options.MaxCacheSize.ShouldBeGreaterThan(10000);
		options.WindowSeconds.ShouldBeLessThan(300);
	}

	[Fact]
	public void Options_ForLongRunningDeduplication_HasLongerExpiry()
	{
		// Act
		var options = new DeduplicationOptions
		{
			Enabled = true,
			ExpiryHours = 72,
			WindowSeconds = 600,
		};

		// Assert
		options.ExpiryHours.ShouldBeGreaterThan(24);
		options.WindowSeconds.ShouldBeGreaterThan(300);
	}

	[Fact]
	public void Options_ForAggressiveCleanup_HasShortCleanupInterval()
	{
		// Act
		var options = new DeduplicationOptions
		{
			Enabled = true,
			CleanupInterval = TimeSpan.FromMinutes(1),
		};

		// Assert
		options.CleanupInterval.ShouldBeLessThan(TimeSpan.FromMinutes(5));
	}

	#endregion
}
