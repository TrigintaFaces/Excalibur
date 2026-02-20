// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.InMemory.Outbox;

namespace Excalibur.Data.Tests.InMemory;

/// <summary>
/// Unit tests for <see cref="InMemoryOutboxOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Data")]
public sealed class InMemoryOutboxOptionsShould : UnitTestBase
{
	#region Default Values Tests

	[Fact]
	public void HaveDefaultMaxMessages()
	{
		// Arrange & Act
		var options = new InMemoryOutboxOptions();

		// Assert
		options.MaxMessages.ShouldBe(10000);
	}

	[Fact]
	public void HaveDefaultRetentionPeriod()
	{
		// Arrange & Act
		var options = new InMemoryOutboxOptions();

		// Assert
		options.DefaultRetentionPeriod.ShouldBe(TimeSpan.FromDays(7));
	}

	#endregion Default Values Tests

	#region Property Setting Tests

	[Fact]
	public void AllowMaxMessagesToBeSet()
	{
		// Arrange & Act
		var options = new InMemoryOutboxOptions { MaxMessages = 50000 };

		// Assert
		options.MaxMessages.ShouldBe(50000);
	}

	[Fact]
	public void AllowZeroMaxMessagesForUnlimited()
	{
		// Arrange & Act
		var options = new InMemoryOutboxOptions { MaxMessages = 0 };

		// Assert
		options.MaxMessages.ShouldBe(0);
	}

	[Fact]
	public void AllowDefaultRetentionPeriodToBeSet()
	{
		// Arrange & Act
		var options = new InMemoryOutboxOptions { DefaultRetentionPeriod = TimeSpan.FromDays(30) };

		// Assert
		options.DefaultRetentionPeriod.ShouldBe(TimeSpan.FromDays(30));
	}

	#endregion Property Setting Tests

	#region Configuration Scenario Tests

	[Fact]
	public void CreateHighCapacityConfiguration()
	{
		// Arrange & Act
		var options = new InMemoryOutboxOptions
		{
			MaxMessages = 100000,
			DefaultRetentionPeriod = TimeSpan.FromHours(6),
		};

		// Assert
		options.MaxMessages.ShouldBe(100000);
		options.DefaultRetentionPeriod.ShouldBe(TimeSpan.FromHours(6));
	}

	[Fact]
	public void CreateLongRetentionConfiguration()
	{
		// Arrange & Act
		var options = new InMemoryOutboxOptions
		{
			MaxMessages = 5000,
			DefaultRetentionPeriod = TimeSpan.FromDays(90),
		};

		// Assert
		options.DefaultRetentionPeriod.ShouldBe(TimeSpan.FromDays(90));
	}

	[Fact]
	public void CreateUnlimitedConfiguration()
	{
		// Arrange & Act
		var options = new InMemoryOutboxOptions
		{
			MaxMessages = 0, // unlimited
			DefaultRetentionPeriod = TimeSpan.FromDays(365),
		};

		// Assert
		options.MaxMessages.ShouldBe(0);
		options.DefaultRetentionPeriod.ShouldBe(TimeSpan.FromDays(365));
	}

	[Fact]
	public void CreateShortRetentionConfiguration()
	{
		// Arrange & Act
		var options = new InMemoryOutboxOptions
		{
			MaxMessages = 1000,
			DefaultRetentionPeriod = TimeSpan.FromMinutes(30),
		};

		// Assert
		options.DefaultRetentionPeriod.ShouldBe(TimeSpan.FromMinutes(30));
	}

	[Fact]
	public void CreateMinimalConfiguration()
	{
		// Arrange & Act
		var options = new InMemoryOutboxOptions
		{
			MaxMessages = 100,
			DefaultRetentionPeriod = TimeSpan.FromMinutes(5),
		};

		// Assert
		options.MaxMessages.ShouldBe(100);
		options.DefaultRetentionPeriod.ShouldBe(TimeSpan.FromMinutes(5));
	}

	#endregion Configuration Scenario Tests
}
