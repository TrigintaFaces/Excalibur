// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Options.Delivery;

namespace Excalibur.Dispatch.Tests.Options.Delivery;

/// <summary>
/// Unit tests for <see cref="DeduplicationOptions"/> in the Delivery namespace.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Options")]
[Trait("Priority", "0")]
public sealed class DeliveryDeduplicationOptionsShould
{
	#region Default Value Tests

	[Fact]
	public void Default_DefaultExpiry_Is24Hours()
	{
		// Arrange & Act
		var options = new DeduplicationOptions();

		// Assert
		options.DefaultExpiry.ShouldBe(TimeSpan.FromHours(24));
	}

	[Fact]
	public void Default_Enabled_IsTrue()
	{
		// Arrange & Act
		var options = new DeduplicationOptions();

		// Assert
		options.Enabled.ShouldBeTrue();
	}

	[Fact]
	public void Default_Strategy_IsMessageId()
	{
		// Arrange & Act
		var options = new DeduplicationOptions();

		// Assert
		options.Strategy.ShouldBe(DeduplicationStrategy.MessageId);
	}

	[Fact]
	public void Default_MaxMemoryEntries_Is10000()
	{
		// Arrange & Act
		var options = new DeduplicationOptions();

		// Assert
		options.MaxMemoryEntries.ShouldBe(10000);
	}

	[Fact]
	public void Default_CleanupInterval_Is5Minutes()
	{
		// Arrange & Act
		var options = new DeduplicationOptions();

		// Assert
		options.CleanupInterval.ShouldBe(TimeSpan.FromMinutes(5));
	}

	[Fact]
	public void Default_DeduplicationWindow_Is5Minutes()
	{
		// Arrange & Act
		var options = new DeduplicationOptions();

		// Assert
		options.DeduplicationWindow.ShouldBe(TimeSpan.FromMinutes(5));
	}

	#endregion

	#region Property Setter Tests

	[Fact]
	public void DefaultExpiry_CanBeSet()
	{
		// Arrange
		var options = new DeduplicationOptions();

		// Act
		options.DefaultExpiry = TimeSpan.FromHours(48);

		// Assert
		options.DefaultExpiry.ShouldBe(TimeSpan.FromHours(48));
	}

	[Fact]
	public void Enabled_CanBeSet()
	{
		// Arrange
		var options = new DeduplicationOptions();

		// Act
		options.Enabled = false;

		// Assert
		options.Enabled.ShouldBeFalse();
	}

	[Fact]
	public void Strategy_CanBeSet()
	{
		// Arrange
		var options = new DeduplicationOptions();

		// Act
		options.Strategy = DeduplicationStrategy.ContentHash;

		// Assert
		options.Strategy.ShouldBe(DeduplicationStrategy.ContentHash);
	}

	[Fact]
	public void MaxMemoryEntries_CanBeSet()
	{
		// Arrange
		var options = new DeduplicationOptions();

		// Act
		options.MaxMemoryEntries = 50000;

		// Assert
		options.MaxMemoryEntries.ShouldBe(50000);
	}

	[Fact]
	public void CleanupInterval_CanBeSet()
	{
		// Arrange
		var options = new DeduplicationOptions();

		// Act
		options.CleanupInterval = TimeSpan.FromMinutes(10);

		// Assert
		options.CleanupInterval.ShouldBe(TimeSpan.FromMinutes(10));
	}

	[Fact]
	public void DeduplicationWindow_CanBeSet()
	{
		// Arrange
		var options = new DeduplicationOptions();

		// Act
		options.DeduplicationWindow = TimeSpan.FromMinutes(10);

		// Assert
		options.DeduplicationWindow.ShouldBe(TimeSpan.FromMinutes(10));
	}

	#endregion

	#region Object Initializer Tests

	[Fact]
	public void ObjectInitializer_SetsAllProperties()
	{
		// Act
		var options = new DeduplicationOptions
		{
			DefaultExpiry = TimeSpan.FromHours(12),
			Enabled = false,
			Strategy = DeduplicationStrategy.ContentHash,
			MaxMemoryEntries = 25000,
			CleanupInterval = TimeSpan.FromMinutes(15),
			DeduplicationWindow = TimeSpan.FromMinutes(15),
		};

		// Assert
		options.DefaultExpiry.ShouldBe(TimeSpan.FromHours(12));
		options.Enabled.ShouldBeFalse();
		options.Strategy.ShouldBe(DeduplicationStrategy.ContentHash);
		options.MaxMemoryEntries.ShouldBe(25000);
		options.CleanupInterval.ShouldBe(TimeSpan.FromMinutes(15));
		options.DeduplicationWindow.ShouldBe(TimeSpan.FromMinutes(15));
	}

	#endregion

	#region Real-World Scenario Tests

	[Fact]
	public void Options_ForHighVolume_HasLargeMaxEntries()
	{
		// Act
		var options = new DeduplicationOptions
		{
			MaxMemoryEntries = 100000,
			DefaultExpiry = TimeSpan.FromHours(48),
		};

		// Assert
		options.MaxMemoryEntries.ShouldBeGreaterThan(10000);
	}

	[Fact]
	public void Options_ForContentBasedDedup_UsesContentHashStrategy()
	{
		// Act
		var options = new DeduplicationOptions
		{
			Strategy = DeduplicationStrategy.ContentHash,
			DeduplicationWindow = TimeSpan.FromMinutes(30),
		};

		// Assert
		options.Strategy.ShouldBe(DeduplicationStrategy.ContentHash);
	}

	[Fact]
	public void Options_ForDisabled_HasEnabledFalse()
	{
		// Act
		var options = new DeduplicationOptions
		{
			Enabled = false,
		};

		// Assert
		options.Enabled.ShouldBeFalse();
	}

	[Fact]
	public void Options_ForAggressiveCleanup_HasShortIntervals()
	{
		// Act
		var options = new DeduplicationOptions
		{
			CleanupInterval = TimeSpan.FromMinutes(1),
			DefaultExpiry = TimeSpan.FromHours(1),
		};

		// Assert
		options.CleanupInterval.ShouldBeLessThan(TimeSpan.FromMinutes(5));
		options.DefaultExpiry.ShouldBeLessThan(TimeSpan.FromHours(24));
	}

	#endregion
}
