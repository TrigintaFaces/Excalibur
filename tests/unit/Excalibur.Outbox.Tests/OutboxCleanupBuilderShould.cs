// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Outbox.Tests;

/// <summary>
/// Unit tests for <see cref="IOutboxCleanupBuilder"/> implementation.
/// </summary>
[Trait("Category", "Unit")]
public sealed class OutboxCleanupBuilderShould : UnitTestBase
{
	#region EnableAutoCleanup Tests

	[Fact]
	public void EnableAutoCleanup_DefaultsToTrue()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddExcaliburOutbox();
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<OutboxOptions>();
		options.EnableAutomaticCleanup.ShouldBeTrue();
	}

	[Fact]
	public void EnableAutoCleanup_CanBeDisabled()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddExcaliburOutbox(builder =>
		{
			_ = builder.WithCleanup(c => c.EnableAutoCleanup(false));
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<OutboxOptions>();
		options.EnableAutomaticCleanup.ShouldBeFalse();
	}

	[Fact]
	public void EnableAutoCleanup_CanBeExplicitlyEnabled()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddExcaliburOutbox(builder =>
		{
			_ = builder.WithCleanup(c => c.EnableAutoCleanup(true));
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<OutboxOptions>();
		options.EnableAutomaticCleanup.ShouldBeTrue();
	}

	[Fact]
	public void EnableAutoCleanup_WithNoArgument_DefaultsToTrue()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddExcaliburOutbox(builder =>
		{
			_ = builder.WithCleanup(c => c.EnableAutoCleanup());
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<OutboxOptions>();
		options.EnableAutomaticCleanup.ShouldBeTrue();
	}

	#endregion

	#region RetentionPeriod Tests

	[Theory]
	[InlineData(1)]
	[InlineData(7)]
	[InlineData(30)]
	[InlineData(365)]
	public void RetentionPeriod_AcceptsValidDays(int days)
	{
		// Arrange
		var services = new ServiceCollection();
		var period = TimeSpan.FromDays(days);

		// Act
		_ = services.AddExcaliburOutbox(builder =>
		{
			_ = builder.WithCleanup(c => c.RetentionPeriod(period));
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<OutboxOptions>();
		options.MessageRetentionPeriod.ShouldBe(period);
	}

	[Theory]
	[InlineData(1)]
	[InlineData(60)]
	[InlineData(3600)]
	public void RetentionPeriod_AcceptsValidSeconds(int seconds)
	{
		// Arrange
		var services = new ServiceCollection();
		var period = TimeSpan.FromSeconds(seconds);

		// Act
		_ = services.AddExcaliburOutbox(builder =>
		{
			_ = builder.WithCleanup(c => c.RetentionPeriod(period));
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<OutboxOptions>();
		options.MessageRetentionPeriod.ShouldBe(period);
	}

	[Fact]
	public void RetentionPeriod_ThrowsOnZero()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() =>
			services.AddExcaliburOutbox(builder =>
			{
				_ = builder.WithCleanup(c => c.RetentionPeriod(TimeSpan.Zero));
			}));
	}

	[Fact]
	public void RetentionPeriod_ThrowsOnNegative()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() =>
			services.AddExcaliburOutbox(builder =>
			{
				_ = builder.WithCleanup(c => c.RetentionPeriod(TimeSpan.FromDays(-1)));
			}));
	}

	#endregion

	#region CleanupInterval Tests

	[Theory]
	[InlineData(1)]
	[InlineData(60)]
	[InlineData(1440)]
	public void CleanupInterval_AcceptsValidMinutes(int minutes)
	{
		// Arrange
		var services = new ServiceCollection();
		var interval = TimeSpan.FromMinutes(minutes);

		// Act
		_ = services.AddExcaliburOutbox(builder =>
		{
			_ = builder.WithCleanup(c => c.CleanupInterval(interval));
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<OutboxOptions>();
		options.CleanupInterval.ShouldBe(interval);
	}

	[Theory]
	[InlineData(1)]
	[InlineData(6)]
	[InlineData(24)]
	public void CleanupInterval_AcceptsValidHours(int hours)
	{
		// Arrange
		var services = new ServiceCollection();
		var interval = TimeSpan.FromHours(hours);

		// Act
		_ = services.AddExcaliburOutbox(builder =>
		{
			_ = builder.WithCleanup(c => c.CleanupInterval(interval));
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<OutboxOptions>();
		options.CleanupInterval.ShouldBe(interval);
	}

	[Fact]
	public void CleanupInterval_ThrowsOnZero()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() =>
			services.AddExcaliburOutbox(builder =>
			{
				_ = builder.WithCleanup(c => c.CleanupInterval(TimeSpan.Zero));
			}));
	}

	[Fact]
	public void CleanupInterval_ThrowsOnNegative()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() =>
			services.AddExcaliburOutbox(builder =>
			{
				_ = builder.WithCleanup(c => c.CleanupInterval(TimeSpan.FromHours(-1)));
			}));
	}

	#endregion

	#region Fluent Chaining Tests

	[Fact]
	public void AllMethods_ReturnBuilder_ForChaining()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddExcaliburOutbox(builder =>
		{
			_ = builder.WithCleanup(c => c
				.EnableAutoCleanup(true)
				.RetentionPeriod(TimeSpan.FromDays(14))
				.CleanupInterval(TimeSpan.FromHours(6)));
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<OutboxOptions>();
		options.EnableAutomaticCleanup.ShouldBeTrue();
		options.MessageRetentionPeriod.ShouldBe(TimeSpan.FromDays(14));
		options.CleanupInterval.ShouldBe(TimeSpan.FromHours(6));
	}

	[Fact]
	public void LastValueWins_WhenSamePropertySetMultipleTimes()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddExcaliburOutbox(builder =>
		{
			_ = builder.WithCleanup(c => c
				.RetentionPeriod(TimeSpan.FromDays(7))
				.RetentionPeriod(TimeSpan.FromDays(14))
				.RetentionPeriod(TimeSpan.FromDays(30)));
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<OutboxOptions>();
		options.MessageRetentionPeriod.ShouldBe(TimeSpan.FromDays(30));
	}

	#endregion

	#region Combined Configuration Tests

	[Fact]
	public void DisabledCleanup_StillAcceptsOtherSettings()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddExcaliburOutbox(builder =>
		{
			_ = builder.WithCleanup(c => c
				.EnableAutoCleanup(false)
				.RetentionPeriod(TimeSpan.FromDays(90))
				.CleanupInterval(TimeSpan.FromHours(12)));
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<OutboxOptions>();
		options.EnableAutomaticCleanup.ShouldBeFalse();
		options.MessageRetentionPeriod.ShouldBe(TimeSpan.FromDays(90));
		options.CleanupInterval.ShouldBe(TimeSpan.FromHours(12));
	}

	#endregion
}
