// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.InMemory.Inbox;

namespace Excalibur.Data.Tests.InMemory;

/// <summary>
/// Unit tests for <see cref="InMemoryInboxOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Data")]
public sealed class InMemoryInboxOptionsShould : UnitTestBase
{
	#region Default Values Tests

	[Fact]
	public void HaveDefaultMaxEntries()
	{
		// Arrange & Act
		var options = new InMemoryInboxOptions();

		// Assert
		options.MaxEntries.ShouldBe(10_000);
	}

	[Fact]
	public void HaveDefaultEnableAutomaticCleanup()
	{
		// Arrange & Act
		var options = new InMemoryInboxOptions();

		// Assert
		options.EnableAutomaticCleanup.ShouldBeTrue();
	}

	[Fact]
	public void HaveDefaultCleanupInterval()
	{
		// Arrange & Act
		var options = new InMemoryInboxOptions();

		// Assert
		options.CleanupInterval.ShouldBe(TimeSpan.FromMinutes(5));
	}

	[Fact]
	public void HaveDefaultRetentionPeriod()
	{
		// Arrange & Act
		var options = new InMemoryInboxOptions();

		// Assert
		options.RetentionPeriod.ShouldBe(TimeSpan.FromDays(7));
	}

	#endregion Default Values Tests

	#region Property Setting Tests

	[Fact]
	public void AllowMaxEntriesToBeSet()
	{
		// Arrange & Act
		var options = new InMemoryInboxOptions { MaxEntries = 50_000 };

		// Assert
		options.MaxEntries.ShouldBe(50_000);
	}

	[Fact]
	public void AllowZeroMaxEntriesForUnlimited()
	{
		// Arrange & Act
		var options = new InMemoryInboxOptions { MaxEntries = 0 };

		// Assert
		options.MaxEntries.ShouldBe(0);
	}

	[Fact]
	public void AllowEnableAutomaticCleanupToBeSet()
	{
		// Arrange & Act
		var options = new InMemoryInboxOptions { EnableAutomaticCleanup = false };

		// Assert
		options.EnableAutomaticCleanup.ShouldBeFalse();
	}

	[Fact]
	public void AllowCleanupIntervalToBeSet()
	{
		// Arrange & Act
		var options = new InMemoryInboxOptions { CleanupInterval = TimeSpan.FromMinutes(10) };

		// Assert
		options.CleanupInterval.ShouldBe(TimeSpan.FromMinutes(10));
	}

	[Fact]
	public void AllowRetentionPeriodToBeSet()
	{
		// Arrange & Act
		var options = new InMemoryInboxOptions { RetentionPeriod = TimeSpan.FromDays(30) };

		// Assert
		options.RetentionPeriod.ShouldBe(TimeSpan.FromDays(30));
	}

	#endregion Property Setting Tests

	#region Configuration Scenario Tests

	[Fact]
	public void CreateHighCapacityConfiguration()
	{
		// Arrange & Act
		var options = new InMemoryInboxOptions
		{
			MaxEntries = 100_000,
			EnableAutomaticCleanup = true,
			CleanupInterval = TimeSpan.FromMinutes(1),
			RetentionPeriod = TimeSpan.FromHours(6),
		};

		// Assert
		options.MaxEntries.ShouldBe(100_000);
		options.EnableAutomaticCleanup.ShouldBeTrue();
		options.CleanupInterval.ShouldBe(TimeSpan.FromMinutes(1));
		options.RetentionPeriod.ShouldBe(TimeSpan.FromHours(6));
	}

	[Fact]
	public void CreateLongRetentionConfiguration()
	{
		// Arrange & Act
		var options = new InMemoryInboxOptions
		{
			MaxEntries = 5_000,
			RetentionPeriod = TimeSpan.FromDays(90),
			CleanupInterval = TimeSpan.FromHours(1),
		};

		// Assert
		options.RetentionPeriod.ShouldBe(TimeSpan.FromDays(90));
	}

	[Fact]
	public void CreateNoAutomaticCleanupConfiguration()
	{
		// Arrange & Act
		var options = new InMemoryInboxOptions
		{
			EnableAutomaticCleanup = false,
			MaxEntries = 0, // unlimited
		};

		// Assert
		options.EnableAutomaticCleanup.ShouldBeFalse();
		options.MaxEntries.ShouldBe(0);
	}

	[Fact]
	public void CreateAggressiveCleanupConfiguration()
	{
		// Arrange & Act
		var options = new InMemoryInboxOptions
		{
			EnableAutomaticCleanup = true,
			CleanupInterval = TimeSpan.FromSeconds(30),
			RetentionPeriod = TimeSpan.FromMinutes(30),
			MaxEntries = 1_000,
		};

		// Assert
		options.CleanupInterval.ShouldBe(TimeSpan.FromSeconds(30));
		options.RetentionPeriod.ShouldBe(TimeSpan.FromMinutes(30));
	}

	[Fact]
	public void CreateMinimalConfiguration()
	{
		// Arrange & Act
		var options = new InMemoryInboxOptions
		{
			MaxEntries = 100,
			CleanupInterval = TimeSpan.FromSeconds(10),
			RetentionPeriod = TimeSpan.FromMinutes(5),
		};

		// Assert
		options.MaxEntries.ShouldBe(100);
	}

	#endregion Configuration Scenario Tests
}
