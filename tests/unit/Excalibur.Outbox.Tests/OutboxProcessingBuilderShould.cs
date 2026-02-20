// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Outbox.Tests;

/// <summary>
/// Unit tests for <see cref="IOutboxProcessingBuilder"/> implementation.
/// </summary>
[Trait("Category", "Unit")]
public sealed class OutboxProcessingBuilderShould : UnitTestBase
{
	#region BatchSize Tests

	[Theory]
	[InlineData(1)]
	[InlineData(100)]
	[InlineData(1000)]
	[InlineData(10000)]
	public void BatchSize_AcceptsValidValues(int batchSize)
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddExcaliburOutbox(builder =>
		{
			_ = builder.WithProcessing(p => p.BatchSize(batchSize));
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<OutboxOptions>();
		options.BatchSize.ShouldBe(batchSize);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	[InlineData(-100)]
	public void BatchSize_ThrowsOnInvalidValues(int batchSize)
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() =>
			services.AddExcaliburOutbox(builder =>
			{
				_ = builder.WithProcessing(p => p.BatchSize(batchSize));
			}));
	}

	#endregion

	#region PollingInterval Tests

	[Theory]
	[InlineData(1)]
	[InlineData(1000)]
	[InlineData(60000)]
	public void PollingInterval_AcceptsValidValues(int milliseconds)
	{
		// Arrange
		var services = new ServiceCollection();
		var interval = TimeSpan.FromMilliseconds(milliseconds);

		// Act
		_ = services.AddExcaliburOutbox(builder =>
		{
			_ = builder.WithProcessing(p => p.PollingInterval(interval));
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<OutboxOptions>();
		options.PollingInterval.ShouldBe(interval);
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

	#endregion

	#region MaxRetryCount Tests

	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	[InlineData(5)]
	[InlineData(100)]
	public void MaxRetryCount_AcceptsValidValues(int retryCount)
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddExcaliburOutbox(builder =>
		{
			_ = builder.WithProcessing(p => p.MaxRetryCount(retryCount));
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<OutboxOptions>();
		options.MaxRetryCount.ShouldBe(retryCount);
	}

	[Theory]
	[InlineData(-1)]
	[InlineData(-100)]
	public void MaxRetryCount_ThrowsOnNegative(int retryCount)
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() =>
			services.AddExcaliburOutbox(builder =>
			{
				_ = builder.WithProcessing(p => p.MaxRetryCount(retryCount));
			}));
	}

	#endregion

	#region RetryDelay Tests

	[Theory]
	[InlineData(1)]
	[InlineData(60)]
	[InlineData(3600)]
	public void RetryDelay_AcceptsValidValues(int seconds)
	{
		// Arrange
		var services = new ServiceCollection();
		var delay = TimeSpan.FromSeconds(seconds);

		// Act
		_ = services.AddExcaliburOutbox(builder =>
		{
			_ = builder.WithProcessing(p => p.RetryDelay(delay));
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<OutboxOptions>();
		options.RetryDelay.ShouldBe(delay);
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

	#endregion

	#region ProcessorId Tests

	[Theory]
	[InlineData("worker-1")]
	[InlineData("processor-abc-123")]
	[InlineData("instance")]
	public void ProcessorId_AcceptsValidValues(string processorId)
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddExcaliburOutbox(builder =>
		{
			_ = builder.WithProcessing(p => p.ProcessorId(processorId));
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<OutboxOptions>();
		options.ProcessorId.ShouldBe(processorId);
	}

	[Fact]
	public void ProcessorId_ThrowsOnNull()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			services.AddExcaliburOutbox(builder =>
			{
				_ = builder.WithProcessing(p => p.ProcessorId(null!));
			}));
	}

	[Fact]
	public void ProcessorId_ThrowsOnEmpty()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			services.AddExcaliburOutbox(builder =>
			{
				_ = builder.WithProcessing(p => p.ProcessorId(""));
			}));
	}

	[Fact]
	public void ProcessorId_ThrowsOnWhitespace()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			services.AddExcaliburOutbox(builder =>
			{
				_ = builder.WithProcessing(p => p.ProcessorId("   "));
			}));
	}

	#endregion

	#region EnableParallelProcessing Tests

	[Theory]
	[InlineData(1)]
	[InlineData(4)]
	[InlineData(8)]
	[InlineData(16)]
	[InlineData(32)]
	public void EnableParallelProcessing_AcceptsValidDegrees(int degree)
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddExcaliburOutbox(builder =>
		{
			_ = builder.WithProcessing(p => p.EnableParallelProcessing(degree));
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<OutboxOptions>();
		options.EnableParallelProcessing.ShouldBeTrue();
		options.MaxDegreeOfParallelism.ShouldBe(degree);
	}

	[Fact]
	public void EnableParallelProcessing_DefaultDegreeIsFour()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddExcaliburOutbox(builder =>
		{
			_ = builder.WithProcessing(p => p.EnableParallelProcessing());
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<OutboxOptions>();
		options.EnableParallelProcessing.ShouldBeTrue();
		options.MaxDegreeOfParallelism.ShouldBe(4);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	public void EnableParallelProcessing_ThrowsOnInvalidDegree(int degree)
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() =>
			services.AddExcaliburOutbox(builder =>
			{
				_ = builder.WithProcessing(p => p.EnableParallelProcessing(degree));
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
			_ = builder.WithProcessing(p => p
				.BatchSize(100)
				.PollingInterval(TimeSpan.FromSeconds(5))
				.MaxRetryCount(5)
				.RetryDelay(TimeSpan.FromMinutes(1))
				.ProcessorId("test")
				.EnableParallelProcessing(4));
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<OutboxOptions>();
		options.BatchSize.ShouldBe(100);
		options.PollingInterval.ShouldBe(TimeSpan.FromSeconds(5));
		options.MaxRetryCount.ShouldBe(5);
		options.RetryDelay.ShouldBe(TimeSpan.FromMinutes(1));
		options.ProcessorId.ShouldBe("test");
		options.EnableParallelProcessing.ShouldBeTrue();
		options.MaxDegreeOfParallelism.ShouldBe(4);
	}

	[Fact]
	public void LastValueWins_WhenSamePropertySetMultipleTimes()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddExcaliburOutbox(builder =>
		{
			_ = builder.WithProcessing(p => p
				.BatchSize(100)
				.BatchSize(200)
				.BatchSize(300));
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<OutboxOptions>();
		options.BatchSize.ShouldBe(300);
	}

	#endregion
}
