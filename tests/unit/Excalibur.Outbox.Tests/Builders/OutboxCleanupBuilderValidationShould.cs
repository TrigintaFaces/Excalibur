// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Outbox.Tests.Builders;

/// <summary>
/// Unit tests for <see cref="IOutboxCleanupBuilder"/> argument validation.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class OutboxCleanupBuilderValidationShould : UnitTestBase
{
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

	[Theory]
	[InlineData(1)]
	[InlineData(7)]
	[InlineData(30)]
	[InlineData(365)]
	public void RetentionPeriod_AcceptsValidDays(int days)
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddExcaliburOutbox(builder =>
		{
			_ = builder.WithCleanup(c => c.RetentionPeriod(TimeSpan.FromDays(days)));
		});

		// Assert - no exception thrown
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

	[Theory]
	[InlineData(1)]
	[InlineData(6)]
	[InlineData(24)]
	public void CleanupInterval_AcceptsValidHours(int hours)
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddExcaliburOutbox(builder =>
		{
			_ = builder.WithCleanup(c => c.CleanupInterval(TimeSpan.FromHours(hours)));
		});

		// Assert - no exception thrown
	}

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public void EnableAutoCleanup_AcceptsBooleanValues(bool enable)
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddExcaliburOutbox(builder =>
		{
			_ = builder.WithCleanup(c => c.EnableAutoCleanup(enable));
		});

		// Assert - no exception thrown
	}
}
