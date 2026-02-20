// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Outbox.Tests.Builders;

/// <summary>
/// Unit tests for <see cref="IOutboxProcessingBuilder"/> argument validation.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class OutboxProcessingBuilderValidationShould : UnitTestBase
{
	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	[InlineData(-100)]
	public void BatchSize_ThrowsOnInvalidValue(int invalidValue)
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() =>
			services.AddExcaliburOutbox(builder =>
			{
				_ = builder.WithProcessing(p => p.BatchSize(invalidValue));
			}));
	}

	[Theory]
	[InlineData(1)]
	[InlineData(100)]
	[InlineData(10000)]
	public void BatchSize_AcceptsValidValues(int validValue)
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddExcaliburOutbox(builder =>
		{
			_ = builder.WithProcessing(p => p.BatchSize(validValue));
		});

		// Assert - no exception thrown
	}

	[Fact]
	public void PollingInterval_ThrowsOnZero()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() =>
			services.AddExcaliburOutbox(builder =>
			{
				_ = builder.WithProcessing(p => p.PollingInterval(TimeSpan.Zero));
			}));
	}

	[Fact]
	public void PollingInterval_ThrowsOnNegative()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() =>
			services.AddExcaliburOutbox(builder =>
			{
				_ = builder.WithProcessing(p => p.PollingInterval(TimeSpan.FromSeconds(-1)));
			}));
	}

	[Theory]
	[InlineData(1)]
	[InlineData(5)]
	[InlineData(60)]
	public void PollingInterval_AcceptsValidValues(int seconds)
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddExcaliburOutbox(builder =>
		{
			_ = builder.WithProcessing(p => p.PollingInterval(TimeSpan.FromSeconds(seconds)));
		});

		// Assert - no exception thrown
	}

	[Theory]
	[InlineData(-1)]
	[InlineData(-10)]
	public void MaxRetryCount_ThrowsOnNegative(int invalidValue)
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() =>
			services.AddExcaliburOutbox(builder =>
			{
				_ = builder.WithProcessing(p => p.MaxRetryCount(invalidValue));
			}));
	}

	[Theory]
	[InlineData(0)]  // Zero retries is valid (no retries)
	[InlineData(1)]
	[InlineData(10)]
	public void MaxRetryCount_AcceptsValidValues(int validValue)
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddExcaliburOutbox(builder =>
		{
			_ = builder.WithProcessing(p => p.MaxRetryCount(validValue));
		});

		// Assert - no exception thrown
	}

	[Fact]
	public void RetryDelay_ThrowsOnZero()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() =>
			services.AddExcaliburOutbox(builder =>
			{
				_ = builder.WithProcessing(p => p.RetryDelay(TimeSpan.Zero));
			}));
	}

	[Fact]
	public void RetryDelay_ThrowsOnNegative()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() =>
			services.AddExcaliburOutbox(builder =>
			{
				_ = builder.WithProcessing(p => p.RetryDelay(TimeSpan.FromMinutes(-1)));
			}));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void ProcessorId_ThrowsOnInvalidValue(string? invalidValue)
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			services.AddExcaliburOutbox(builder =>
			{
				_ = builder.WithProcessing(p => p.ProcessorId(invalidValue));
			}));
	}

	[Theory]
	[InlineData("processor-1")]
	[InlineData("instance-abc")]
	[InlineData("p")]
	public void ProcessorId_AcceptsValidValues(string validValue)
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddExcaliburOutbox(builder =>
		{
			_ = builder.WithProcessing(p => p.ProcessorId(validValue));
		});

		// Assert - no exception thrown
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	[InlineData(-100)]
	public void EnableParallelProcessing_ThrowsOnInvalidParallelism(int invalidValue)
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() =>
			services.AddExcaliburOutbox(builder =>
			{
				_ = builder.WithProcessing(p => p.EnableParallelProcessing(invalidValue));
			}));
	}

	[Theory]
	[InlineData(1)]
	[InlineData(4)]
	[InlineData(16)]
	public void EnableParallelProcessing_AcceptsValidValues(int validValue)
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddExcaliburOutbox(builder =>
		{
			_ = builder.WithProcessing(p => p.EnableParallelProcessing(validValue));
		});

		// Assert - no exception thrown
	}
}
