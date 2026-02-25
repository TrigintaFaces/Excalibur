// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Outbox.Tests;

/// <summary>
/// Unit tests for <see cref="IOutboxBuilder"/> implementation.
/// </summary>
[Trait("Category", "Unit")]
public sealed class OutboxBuilderShould : UnitTestBase
{
	#region WithProcessing Tests

	[Fact]
	public void WithProcessing_AppliesBatchSize()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddExcaliburOutbox(builder =>
		{
			_ = builder.WithProcessing(p => p.BatchSize(500));
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<OutboxOptions>();
		options.BatchSize.ShouldBe(500);
	}

	[Fact]
	public void WithProcessing_AppliesPollingInterval()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddExcaliburOutbox(builder =>
		{
			_ = builder.WithProcessing(p => p.PollingInterval(TimeSpan.FromSeconds(30)));
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<OutboxOptions>();
		options.PollingInterval.ShouldBe(TimeSpan.FromSeconds(30));
	}

	[Fact]
	public void WithProcessing_AppliesMaxRetryCount()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddExcaliburOutbox(builder =>
		{
			_ = builder.WithProcessing(p => p.MaxRetryCount(10));
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<OutboxOptions>();
		options.MaxRetryCount.ShouldBe(10);
	}

	[Fact]
	public void WithProcessing_AppliesRetryDelay()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddExcaliburOutbox(builder =>
		{
			_ = builder.WithProcessing(p => p.RetryDelay(TimeSpan.FromMinutes(15)));
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<OutboxOptions>();
		options.RetryDelay.ShouldBe(TimeSpan.FromMinutes(15));
	}

	[Fact]
	public void WithProcessing_AppliesProcessorId()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddExcaliburOutbox(builder =>
		{
			_ = builder.WithProcessing(p => p.ProcessorId("worker-1"));
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<OutboxOptions>();
		options.ProcessorId.ShouldBe("worker-1");
	}

	[Fact]
	public void WithProcessing_AppliesParallelProcessing()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddExcaliburOutbox(builder =>
		{
			_ = builder.WithProcessing(p => p.EnableParallelProcessing(8));
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<OutboxOptions>();
		options.EnableParallelProcessing.ShouldBeTrue();
		options.MaxDegreeOfParallelism.ShouldBe(8);
	}

	[Fact]
	public void WithProcessing_ThrowsOnNullConfigure()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			services.AddExcaliburOutbox(builder =>
			{
				_ = builder.WithProcessing(null!);
			}));
	}

	#endregion

	#region WithCleanup Tests

	[Fact]
	public void WithCleanup_AppliesEnableAutoCleanup()
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
	public void WithCleanup_AppliesRetentionPeriod()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddExcaliburOutbox(builder =>
		{
			_ = builder.WithCleanup(c => c.RetentionPeriod(TimeSpan.FromDays(30)));
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<OutboxOptions>();
		options.MessageRetentionPeriod.ShouldBe(TimeSpan.FromDays(30));
	}

	[Fact]
	public void WithCleanup_AppliesCleanupInterval()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddExcaliburOutbox(builder =>
		{
			_ = builder.WithCleanup(c => c.CleanupInterval(TimeSpan.FromHours(6)));
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<OutboxOptions>();
		options.CleanupInterval.ShouldBe(TimeSpan.FromHours(6));
	}

	[Fact]
	public void WithCleanup_ThrowsOnNullConfigure()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			services.AddExcaliburOutbox(builder =>
			{
				_ = builder.WithCleanup(null!);
			}));
	}

	#endregion

	#region EnableBackgroundProcessing Tests

	[Fact]
	public void EnableBackgroundProcessing_SetsFlag()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddExcaliburOutbox(builder =>
		{
			_ = builder.EnableBackgroundProcessing();
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<OutboxOptions>();
		options.EnableBackgroundProcessing.ShouldBeTrue();
	}

	[Fact]
	public void EnableBackgroundProcessing_RegistersHostedService()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddExcaliburOutbox(builder =>
		{
			_ = builder.EnableBackgroundProcessing();
		});

		// Assert
		services.Any(s => s.ImplementationType == typeof(Excalibur.Outbox.Outbox.OutboxBackgroundService)).ShouldBeTrue();
	}

	#endregion

	#region Fluent Chaining Tests

	[Fact]
	public void WithProcessing_ReturnsBuilder_ForChaining()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert - should compile and chain
		_ = services.AddExcaliburOutbox(builder =>
		{
			_ = builder
				.WithProcessing(p => p.BatchSize(100))
				.WithCleanup(c => c.RetentionPeriod(TimeSpan.FromDays(7)));
		});
	}

	[Fact]
	public void WithCleanup_ReturnsBuilder_ForChaining()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert - should compile and chain
		_ = services.AddExcaliburOutbox(builder =>
		{
			_ = builder
				.WithCleanup(c => c.EnableAutoCleanup(true))
				.WithProcessing(p => p.MaxRetryCount(5));
		});
	}

	[Fact]
	public void EnableBackgroundProcessing_ReturnsBuilder_ForChaining()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert - should compile and chain
		_ = services.AddExcaliburOutbox(builder =>
		{
			_ = builder
				.EnableBackgroundProcessing()
				.WithProcessing(p => p.BatchSize(200));
		});
	}

	[Fact]
	public void FullConfiguration_ChainsAllMethods()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddExcaliburOutbox(builder =>
		{
			_ = builder
				.WithProcessing(p => p
					.BatchSize(500)
					.PollingInterval(TimeSpan.FromSeconds(2))
					.MaxRetryCount(7)
					.RetryDelay(TimeSpan.FromMinutes(10))
					.ProcessorId("worker-1")
					.EnableParallelProcessing(16))
				.WithCleanup(c => c
					.EnableAutoCleanup(true)
					.RetentionPeriod(TimeSpan.FromDays(14))
					.CleanupInterval(TimeSpan.FromHours(4)))
				.EnableBackgroundProcessing();
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<OutboxOptions>();
		options.BatchSize.ShouldBe(500);
		options.PollingInterval.ShouldBe(TimeSpan.FromSeconds(2));
		options.MaxRetryCount.ShouldBe(7);
		options.RetryDelay.ShouldBe(TimeSpan.FromMinutes(10));
		options.ProcessorId.ShouldBe("worker-1");
		options.EnableParallelProcessing.ShouldBeTrue();
		options.MaxDegreeOfParallelism.ShouldBe(16);
		options.EnableAutomaticCleanup.ShouldBeTrue();
		options.MessageRetentionPeriod.ShouldBe(TimeSpan.FromDays(14));
		options.CleanupInterval.ShouldBe(TimeSpan.FromHours(4));
		options.EnableBackgroundProcessing.ShouldBeTrue();
	}

	#endregion

	#region Services Property Tests

	[Fact]
	public void Services_IsAccessibleViaBuilder()
	{
		// Arrange
		var services = new ServiceCollection();
		IServiceCollection? capturedServices = null;

		// Act
		_ = services.AddExcaliburOutbox(builder =>
		{
			capturedServices = builder.Services;
		});

		// Assert
		capturedServices.ShouldBeSameAs(services);
	}

	#endregion
}
