// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Options.Configuration;

namespace Excalibur.Dispatch.Tests.Options.Configuration;

/// <summary>
/// Unit tests for <see cref="InboxOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Options")]
[Trait("Priority", "0")]
public sealed class InboxOptionsShould
{
	#region Default Value Tests

	[Fact]
	public void Default_Enabled_IsFalse()
	{
		// Arrange & Act
		var options = new InboxOptions();

		// Assert
		options.Enabled.ShouldBeFalse();
	}

	[Fact]
	public void Default_DeduplicationExpiryHours_IsTwentyFour()
	{
		// Arrange & Act
		var options = new InboxOptions();

		// Assert
		options.DeduplicationExpiryHours.ShouldBe(24);
	}

	[Fact]
	public void Default_AckAfterHandle_IsTrue()
	{
		// Arrange & Act
		var options = new InboxOptions();

		// Assert
		options.AckAfterHandle.ShouldBeTrue();
	}

	[Fact]
	public void Default_MaxRetries_IsThree()
	{
		// Arrange & Act
		var options = new InboxOptions();

		// Assert
		options.MaxRetries.ShouldBe(3);
	}

	[Fact]
	public void Default_RetryDelayMinutes_IsFive()
	{
		// Arrange & Act
		var options = new InboxOptions();

		// Assert
		options.RetryDelayMinutes.ShouldBe(5);
	}

	[Fact]
	public void Default_MaxRetention_IsSevenDays()
	{
		// Arrange & Act
		var options = new InboxOptions();

		// Assert
		options.MaxRetention.ShouldBe(TimeSpan.FromDays(7));
	}

	[Fact]
	public void Default_CleanupInterval_IsOneHour()
	{
		// Arrange & Act
		var options = new InboxOptions();

		// Assert
		options.CleanupInterval.ShouldBe(TimeSpan.FromHours(1));
	}

	[Fact]
	public void Default_CleanupIntervalSeconds_IsThirtySixHundred()
	{
		// Arrange & Act
		var options = new InboxOptions();

		// Assert
		options.CleanupIntervalSeconds.ShouldBe(3600);
	}

	[Fact]
	public void Default_RetentionDays_IsSeven()
	{
		// Arrange & Act
		var options = new InboxOptions();

		// Assert
		options.RetentionDays.ShouldBe(7);
	}

	#endregion

	#region Property Setter Tests

	[Fact]
	public void Enabled_CanBeSet()
	{
		// Arrange
		var options = new InboxOptions();

		// Act
		options.Enabled = true;

		// Assert
		options.Enabled.ShouldBeTrue();
	}

	[Fact]
	public void DeduplicationExpiryHours_CanBeSet()
	{
		// Arrange
		var options = new InboxOptions();

		// Act
		options.DeduplicationExpiryHours = 48;

		// Assert
		options.DeduplicationExpiryHours.ShouldBe(48);
	}

	[Fact]
	public void AckAfterHandle_CanBeSet()
	{
		// Arrange
		var options = new InboxOptions();

		// Act
		options.AckAfterHandle = false;

		// Assert
		options.AckAfterHandle.ShouldBeFalse();
	}

	[Fact]
	public void MaxRetries_CanBeSet()
	{
		// Arrange
		var options = new InboxOptions();

		// Act
		options.MaxRetries = 5;

		// Assert
		options.MaxRetries.ShouldBe(5);
	}

	[Fact]
	public void RetryDelayMinutes_CanBeSet()
	{
		// Arrange
		var options = new InboxOptions();

		// Act
		options.RetryDelayMinutes = 10;

		// Assert
		options.RetryDelayMinutes.ShouldBe(10);
	}

	[Fact]
	public void MaxRetention_CanBeSet()
	{
		// Arrange
		var options = new InboxOptions();

		// Act
		options.MaxRetention = TimeSpan.FromDays(14);

		// Assert
		options.MaxRetention.ShouldBe(TimeSpan.FromDays(14));
	}

	[Fact]
	public void CleanupInterval_CanBeSet()
	{
		// Arrange
		var options = new InboxOptions();

		// Act
		options.CleanupInterval = TimeSpan.FromMinutes(30);

		// Assert
		options.CleanupInterval.ShouldBe(TimeSpan.FromMinutes(30));
	}

	[Fact]
	public void CleanupIntervalSeconds_CanBeSet()
	{
		// Arrange
		var options = new InboxOptions();

		// Act
		options.CleanupIntervalSeconds = 1800;

		// Assert
		options.CleanupIntervalSeconds.ShouldBe(1800);
	}

	[Fact]
	public void RetentionDays_CanBeSet()
	{
		// Arrange
		var options = new InboxOptions();

		// Act
		options.RetentionDays = 14;

		// Assert
		options.RetentionDays.ShouldBe(14);
	}

	#endregion

	#region Object Initializer Tests

	[Fact]
	public void ObjectInitializer_SetsAllProperties()
	{
		// Act
		var options = new InboxOptions
		{
			Enabled = true,
			DeduplicationExpiryHours = 12,
			AckAfterHandle = false,
			MaxRetries = 5,
			RetryDelayMinutes = 10,
			MaxRetention = TimeSpan.FromDays(30),
			CleanupInterval = TimeSpan.FromMinutes(15),
			CleanupIntervalSeconds = 900,
			RetentionDays = 30,
		};

		// Assert
		options.Enabled.ShouldBeTrue();
		options.DeduplicationExpiryHours.ShouldBe(12);
		options.AckAfterHandle.ShouldBeFalse();
		options.MaxRetries.ShouldBe(5);
		options.RetryDelayMinutes.ShouldBe(10);
		options.MaxRetention.ShouldBe(TimeSpan.FromDays(30));
		options.CleanupInterval.ShouldBe(TimeSpan.FromMinutes(15));
		options.CleanupIntervalSeconds.ShouldBe(900);
		options.RetentionDays.ShouldBe(30);
	}

	#endregion

	#region Real-World Scenario Tests

	[Fact]
	public void Options_ForProduction_HasInboxEnabled()
	{
		// Act
		var options = new InboxOptions
		{
			Enabled = true,
			MaxRetries = 5,
			MaxRetention = TimeSpan.FromDays(14),
		};

		// Assert
		options.Enabled.ShouldBeTrue();
		options.MaxRetries.ShouldBeGreaterThan(3);
	}

	[Fact]
	public void Options_ForLightMode_HasLongerDeduplicationExpiry()
	{
		// Act
		var options = new InboxOptions
		{
			Enabled = false,
			DeduplicationExpiryHours = 48,
		};

		// Assert
		options.Enabled.ShouldBeFalse();
		options.DeduplicationExpiryHours.ShouldBeGreaterThan(24);
	}

	[Fact]
	public void Options_ForAggressiveCleanup_HasShortRetention()
	{
		// Act
		var options = new InboxOptions
		{
			RetentionDays = 1,
			CleanupIntervalSeconds = 600,
			CleanupInterval = TimeSpan.FromMinutes(10),
		};

		// Assert
		options.RetentionDays.ShouldBeLessThan(7);
		options.CleanupIntervalSeconds.ShouldBeLessThan(3600);
	}

	#endregion
}
