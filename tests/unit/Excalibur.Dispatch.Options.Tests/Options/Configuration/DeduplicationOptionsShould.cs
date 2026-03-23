// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Options.Delivery;

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
	public void Default_Enabled_IsTrue()
	{
		// Arrange & Act
		var options = new DeduplicationOptions();

		// Assert
		options.Enabled.ShouldBeTrue();
	}

	[Fact]
	public void Default_DefaultExpiry_IsTwentyFourHours()
	{
		// Arrange & Act
		var options = new DeduplicationOptions();

		// Assert
		options.DefaultExpiry.ShouldBe(TimeSpan.FromHours(24));
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
	public void Default_DeduplicationWindow_IsFiveMinutes()
	{
		// Arrange & Act
		var options = new DeduplicationOptions();

		// Assert
		options.DeduplicationWindow.ShouldBe(TimeSpan.FromMinutes(5));
	}

	[Fact]
	public void Default_MaxMemoryEntries_IsTenThousand()
	{
		// Arrange & Act
		var options = new DeduplicationOptions();

		// Assert
		options.MaxMemoryEntries.ShouldBe(10000);
	}

	[Fact]
	public void Default_Strategy_IsMessageId()
	{
		// Arrange & Act
		var options = new DeduplicationOptions();

		// Assert
		options.Strategy.ShouldBe(DeduplicationStrategy.MessageId);
	}

	#endregion

	#region Property Setter Tests

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
	public void DeduplicationWindow_CanBeSet()
	{
		// Arrange
		var options = new DeduplicationOptions();

		// Act
		options.DeduplicationWindow = TimeSpan.FromMinutes(10);

		// Assert
		options.DeduplicationWindow.ShouldBe(TimeSpan.FromMinutes(10));
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

	#endregion

	#region Object Initializer Tests

	[Fact]
	public void ObjectInitializer_SetsAllProperties()
	{
		// Act
		var options = new DeduplicationOptions
		{
			Enabled = false,
			DefaultExpiry = TimeSpan.FromHours(12),
			CleanupInterval = TimeSpan.FromMinutes(10),
			DeduplicationWindow = TimeSpan.FromMinutes(2),
			MaxMemoryEntries = 5000,
			Strategy = DeduplicationStrategy.ContentHash,
		};

		// Assert
		options.Enabled.ShouldBeFalse();
		options.DefaultExpiry.ShouldBe(TimeSpan.FromHours(12));
		options.CleanupInterval.ShouldBe(TimeSpan.FromMinutes(10));
		options.DeduplicationWindow.ShouldBe(TimeSpan.FromMinutes(2));
		options.MaxMemoryEntries.ShouldBe(5000);
		options.Strategy.ShouldBe(DeduplicationStrategy.ContentHash);
	}

	#endregion

	#region Real-World Scenario Tests

	[Fact]
	public void Options_ForHighVolumeScenario_HasLargeMemoryEntries()
	{
		// Act
		var options = new DeduplicationOptions
		{
			Enabled = true,
			MaxMemoryEntries = 100000,
			DeduplicationWindow = TimeSpan.FromMinutes(1),
		};

		// Assert
		options.MaxMemoryEntries.ShouldBeGreaterThan(10000);
		options.DeduplicationWindow.ShouldBeLessThan(TimeSpan.FromMinutes(5));
	}

	[Fact]
	public void Options_ForLongRunningDeduplication_HasLongerExpiry()
	{
		// Act
		var options = new DeduplicationOptions
		{
			Enabled = true,
			DefaultExpiry = TimeSpan.FromHours(72),
			DeduplicationWindow = TimeSpan.FromMinutes(10),
		};

		// Assert
		options.DefaultExpiry.ShouldBeGreaterThan(TimeSpan.FromHours(24));
		options.DeduplicationWindow.ShouldBeGreaterThan(TimeSpan.FromMinutes(5));
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
