// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Outbox;

using Microsoft.Extensions.DependencyInjection;

using Excalibur.Data.InMemory;
namespace Excalibur.Data.Tests.InMemory.Builders;

/// <summary>
/// Unit tests for <see cref="IInMemoryOutboxBuilder"/> argument validation.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class InMemoryOutboxBuilderValidationShould : UnitTestBase
{
	[Theory]
	[InlineData(-1)]
	[InlineData(-100)]
	[InlineData(int.MinValue)]
	public void MaxMessages_ThrowsOnNegative(int invalidValue)
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() =>
			services.AddExcaliburOutbox(builder =>
			{
				_ = builder.UseInMemory(inmemory =>
				{
					_ = inmemory.MaxMessages(invalidValue);
				});
			}));
	}

	[Theory]
	[InlineData(0)]  // Zero is valid (means unlimited)
	[InlineData(1)]
	[InlineData(100)]
	[InlineData(10000)]
	public void MaxMessages_AcceptsValidValues(int validValue)
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddExcaliburOutbox(builder =>
		{
			_ = builder.UseInMemory(inmemory =>
			{
				_ = inmemory.MaxMessages(validValue);
			});
		});

		// Assert - no exception thrown
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
				_ = builder.UseInMemory(inmemory =>
				{
					_ = inmemory.RetentionPeriod(TimeSpan.Zero);
				});
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
				_ = builder.UseInMemory(inmemory =>
				{
					_ = inmemory.RetentionPeriod(TimeSpan.FromHours(-1));
				});
			}));
	}

	[Theory]
	[InlineData(1)]   // 1 minute
	[InlineData(60)]  // 1 hour
	[InlineData(1440)] // 1 day
	public void RetentionPeriod_AcceptsValidMinutes(int minutes)
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddExcaliburOutbox(builder =>
		{
			_ = builder.UseInMemory(inmemory =>
			{
				_ = inmemory.RetentionPeriod(TimeSpan.FromMinutes(minutes));
			});
		});

		// Assert - no exception thrown
	}
}
