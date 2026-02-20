// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Options.Delivery;

namespace Excalibur.Dispatch.Tests.Options.Delivery;

/// <summary>
/// Unit tests for <see cref="InMemoryDeduplicatorOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Options")]
[Trait("Priority", "0")]
public sealed class InMemoryDeduplicatorOptionsShould
{
	#region Default Value Tests

	[Fact]
	public void Default_MaxEntries_Is100000()
	{
		// Arrange & Act
		var options = new InMemoryDeduplicatorOptions();

		// Assert
		options.MaxEntries.ShouldBe(100_000);
	}

	[Fact]
	public void Default_DefaultExpiry_Is24Hours()
	{
		// Arrange & Act
		var options = new InMemoryDeduplicatorOptions();

		// Assert
		options.DefaultExpiry.ShouldBe(TimeSpan.FromHours(24));
	}

	[Fact]
	public void Default_EnableAutomaticCleanup_IsTrue()
	{
		// Arrange & Act
		var options = new InMemoryDeduplicatorOptions();

		// Assert
		options.EnableAutomaticCleanup.ShouldBeTrue();
	}

	[Fact]
	public void Default_CleanupInterval_Is30Minutes()
	{
		// Arrange & Act
		var options = new InMemoryDeduplicatorOptions();

		// Assert
		options.CleanupInterval.ShouldBe(TimeSpan.FromMinutes(30));
	}

	#endregion

	#region Property Setter Tests

	[Fact]
	public void MaxEntries_CanBeSet()
	{
		// Arrange
		var options = new InMemoryDeduplicatorOptions();

		// Act
		options.MaxEntries = 500_000;

		// Assert
		options.MaxEntries.ShouldBe(500_000);
	}

	[Fact]
	public void DefaultExpiry_CanBeSet()
	{
		// Arrange
		var options = new InMemoryDeduplicatorOptions();

		// Act
		options.DefaultExpiry = TimeSpan.FromHours(48);

		// Assert
		options.DefaultExpiry.ShouldBe(TimeSpan.FromHours(48));
	}

	[Fact]
	public void EnableAutomaticCleanup_CanBeSet()
	{
		// Arrange
		var options = new InMemoryDeduplicatorOptions();

		// Act
		options.EnableAutomaticCleanup = false;

		// Assert
		options.EnableAutomaticCleanup.ShouldBeFalse();
	}

	[Fact]
	public void CleanupInterval_CanBeSet()
	{
		// Arrange
		var options = new InMemoryDeduplicatorOptions();

		// Act
		options.CleanupInterval = TimeSpan.FromHours(1);

		// Assert
		options.CleanupInterval.ShouldBe(TimeSpan.FromHours(1));
	}

	#endregion

	#region Object Initializer Tests

	[Fact]
	public void ObjectInitializer_SetsAllProperties()
	{
		// Act
		var options = new InMemoryDeduplicatorOptions
		{
			MaxEntries = 200_000,
			DefaultExpiry = TimeSpan.FromHours(12),
			EnableAutomaticCleanup = false,
			CleanupInterval = TimeSpan.FromMinutes(15),
		};

		// Assert
		options.MaxEntries.ShouldBe(200_000);
		options.DefaultExpiry.ShouldBe(TimeSpan.FromHours(12));
		options.EnableAutomaticCleanup.ShouldBeFalse();
		options.CleanupInterval.ShouldBe(TimeSpan.FromMinutes(15));
	}

	#endregion

	#region Real-World Scenario Tests

	[Fact]
	public void Options_ForHighVolume_HasLargeMaxEntries()
	{
		// Act
		var options = new InMemoryDeduplicatorOptions
		{
			MaxEntries = 1_000_000,
			DefaultExpiry = TimeSpan.FromHours(48),
		};

		// Assert
		options.MaxEntries.ShouldBeGreaterThan(100_000);
	}

	[Fact]
	public void Options_ForAggressiveCleanup_HasShortIntervals()
	{
		// Act
		var options = new InMemoryDeduplicatorOptions
		{
			CleanupInterval = TimeSpan.FromMinutes(5),
			DefaultExpiry = TimeSpan.FromHours(1),
		};

		// Assert
		options.CleanupInterval.ShouldBeLessThan(TimeSpan.FromMinutes(30));
		options.DefaultExpiry.ShouldBeLessThan(TimeSpan.FromHours(24));
	}

	[Fact]
	public void Options_ForMemoryConstrained_HasSmallMaxEntries()
	{
		// Act
		var options = new InMemoryDeduplicatorOptions
		{
			MaxEntries = 10_000,
			EnableAutomaticCleanup = true,
			CleanupInterval = TimeSpan.FromMinutes(5),
		};

		// Assert
		options.MaxEntries.ShouldBeLessThan(100_000);
		options.EnableAutomaticCleanup.ShouldBeTrue();
	}

	#endregion
}
