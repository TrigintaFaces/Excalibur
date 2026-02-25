// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Options.Delivery;

namespace Excalibur.Dispatch.Tests.Options.Delivery;

/// <summary>
/// Unit tests for <see cref="InMemoryInboxOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Options")]
[Trait("Priority", "0")]
public sealed class InMemoryInboxOptionsShould
{
	#region Default Value Tests

	[Fact]
	public void Default_MaxEntries_Is10000()
	{
		// Arrange & Act
		var options = new InMemoryInboxOptions();

		// Assert
		options.MaxEntries.ShouldBe(10_000);
	}

	[Fact]
	public void Default_EnableAutomaticCleanup_IsTrue()
	{
		// Arrange & Act
		var options = new InMemoryInboxOptions();

		// Assert
		options.EnableAutomaticCleanup.ShouldBeTrue();
	}

	[Fact]
	public void Default_CleanupInterval_Is5Minutes()
	{
		// Arrange & Act
		var options = new InMemoryInboxOptions();

		// Assert
		options.CleanupInterval.ShouldBe(TimeSpan.FromMinutes(5));
	}

	[Fact]
	public void Default_RetentionPeriod_Is1Hour()
	{
		// Arrange & Act
		var options = new InMemoryInboxOptions();

		// Assert
		options.RetentionPeriod.ShouldBe(TimeSpan.FromHours(1));
	}

	[Fact]
	public void Default_CleanupBatchSize_Is100()
	{
		// Arrange & Act
		var options = new InMemoryInboxOptions();

		// Assert
		options.CleanupBatchSize.ShouldBe(100);
	}

	#endregion

	#region Property Setter Tests

	[Fact]
	public void MaxEntries_CanBeSet()
	{
		// Arrange
		var options = new InMemoryInboxOptions();

		// Act
		options.MaxEntries = 50_000;

		// Assert
		options.MaxEntries.ShouldBe(50_000);
	}

	[Fact]
	public void EnableAutomaticCleanup_CanBeSet()
	{
		// Arrange
		var options = new InMemoryInboxOptions();

		// Act
		options.EnableAutomaticCleanup = false;

		// Assert
		options.EnableAutomaticCleanup.ShouldBeFalse();
	}

	[Fact]
	public void CleanupInterval_CanBeSet()
	{
		// Arrange
		var options = new InMemoryInboxOptions();

		// Act
		options.CleanupInterval = TimeSpan.FromMinutes(10);

		// Assert
		options.CleanupInterval.ShouldBe(TimeSpan.FromMinutes(10));
	}

	[Fact]
	public void RetentionPeriod_CanBeSet()
	{
		// Arrange
		var options = new InMemoryInboxOptions();

		// Act
		options.RetentionPeriod = TimeSpan.FromHours(2);

		// Assert
		options.RetentionPeriod.ShouldBe(TimeSpan.FromHours(2));
	}

	[Fact]
	public void CleanupBatchSize_CanBeSet()
	{
		// Arrange
		var options = new InMemoryInboxOptions();

		// Act
		options.CleanupBatchSize = 500;

		// Assert
		options.CleanupBatchSize.ShouldBe(500);
	}

	#endregion

	#region Object Initializer Tests

	[Fact]
	public void ObjectInitializer_SetsAllProperties()
	{
		// Act
		var options = new InMemoryInboxOptions
		{
			MaxEntries = 25_000,
			EnableAutomaticCleanup = false,
			CleanupInterval = TimeSpan.FromMinutes(15),
			RetentionPeriod = TimeSpan.FromHours(4),
			CleanupBatchSize = 200,
		};

		// Assert
		options.MaxEntries.ShouldBe(25_000);
		options.EnableAutomaticCleanup.ShouldBeFalse();
		options.CleanupInterval.ShouldBe(TimeSpan.FromMinutes(15));
		options.RetentionPeriod.ShouldBe(TimeSpan.FromHours(4));
		options.CleanupBatchSize.ShouldBe(200);
	}

	#endregion

	#region Real-World Scenario Tests

	[Fact]
	public void Options_ForHighVolume_HasLargeMaxEntries()
	{
		// Act
		var options = new InMemoryInboxOptions
		{
			MaxEntries = 100_000,
			CleanupBatchSize = 1000,
		};

		// Assert
		options.MaxEntries.ShouldBeGreaterThan(10_000);
		options.CleanupBatchSize.ShouldBeGreaterThan(100);
	}

	[Fact]
	public void Options_ForLongRetention_HasExtendedPeriod()
	{
		// Act
		var options = new InMemoryInboxOptions
		{
			RetentionPeriod = TimeSpan.FromHours(24),
		};

		// Assert
		options.RetentionPeriod.ShouldBeGreaterThan(TimeSpan.FromHours(1));
	}

	[Fact]
	public void Options_ForAggressiveCleanup_HasShortIntervals()
	{
		// Act
		var options = new InMemoryInboxOptions
		{
			CleanupInterval = TimeSpan.FromMinutes(1),
			RetentionPeriod = TimeSpan.FromMinutes(15),
			CleanupBatchSize = 500,
		};

		// Assert
		options.CleanupInterval.ShouldBeLessThan(TimeSpan.FromMinutes(5));
		options.RetentionPeriod.ShouldBeLessThan(TimeSpan.FromHours(1));
	}

	#endregion
}
