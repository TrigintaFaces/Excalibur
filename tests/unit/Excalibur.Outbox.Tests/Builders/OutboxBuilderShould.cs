// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Outbox.Tests.Builders;

/// <summary>
/// Unit tests for <see cref="IOutboxBuilder"/> fluent API.
/// </summary>
/// <remarks>
/// These tests validate the ADR-098 Microsoft-style fluent builder pattern implementation.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class OutboxBuilderShould : UnitTestBase
{
	[Fact]
	public void AddExcaliburOutbox_WithFluentBuilder_ThrowsOnNullServices()
	{
		// Arrange
		IServiceCollection services = null!;
		Action<IOutboxBuilder> configure = _ => { };

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => services.AddExcaliburOutbox(configure));
	}

	[Fact]
	public void AddExcaliburOutbox_WithFluentBuilder_ThrowsOnNullConfigure()
	{
		// Arrange
		var services = new ServiceCollection();
		Action<IOutboxBuilder> configure = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => services.AddExcaliburOutbox(configure));
	}

	[Fact]
	public void AddExcaliburOutbox_WithFluentBuilder_ReturnsServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddExcaliburOutbox((IOutboxBuilder _) => { });

		// Assert
		result.ShouldBeSameAs(services);
	}

	[Fact]
	public void AddExcaliburOutbox_WithFluentBuilder_RegistersDefaultOptions()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddExcaliburOutbox((IOutboxBuilder _) => { });
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetService<OutboxOptions>();
		_ = options.ShouldNotBeNull();
		options.BatchSize.ShouldBe(100);
		options.PollingInterval.ShouldBe(TimeSpan.FromSeconds(5));
	}

	[Fact]
	public void AddExcaliburOutbox_WithFluentBuilder_ProvidesBuilderWithServices()
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
		_ = capturedServices.ShouldNotBeNull();
		capturedServices.ShouldBeSameAs(services);
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

	[Fact]
	public void WithProcessing_ConfiguresBatchSize()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddExcaliburOutbox(builder =>
		{
			_ = builder.WithProcessing(processing =>
			{
				_ = processing.BatchSize(200);
			});
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<OutboxOptions>();
		options.BatchSize.ShouldBe(200);
	}

	[Fact]
	public void WithProcessing_ConfiguresPollingInterval()
	{
		// Arrange
		var services = new ServiceCollection();
		var expectedInterval = TimeSpan.FromSeconds(15);

		// Act
		_ = services.AddExcaliburOutbox(builder =>
		{
			_ = builder.WithProcessing(processing =>
			{
				_ = processing.PollingInterval(expectedInterval);
			});
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<OutboxOptions>();
		options.PollingInterval.ShouldBe(expectedInterval);
	}

	[Fact]
	public void WithProcessing_ConfiguresMaxRetryCount()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddExcaliburOutbox(builder =>
		{
			_ = builder.WithProcessing(processing =>
			{
				_ = processing.MaxRetryCount(10);
			});
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<OutboxOptions>();
		options.MaxRetryCount.ShouldBe(10);
	}

	[Fact]
	public void WithProcessing_ConfiguresRetryDelay()
	{
		// Arrange
		var services = new ServiceCollection();
		var expectedDelay = TimeSpan.FromMinutes(5);

		// Act
		_ = services.AddExcaliburOutbox(builder =>
		{
			_ = builder.WithProcessing(processing =>
			{
				_ = processing.RetryDelay(expectedDelay);
			});
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<OutboxOptions>();
		options.RetryDelay.ShouldBe(expectedDelay);
	}

	[Fact]
	public void WithProcessing_ConfiguresProcessorId()
	{
		// Arrange
		var services = new ServiceCollection();
		const string processorId = "instance-1";

		// Act
		_ = services.AddExcaliburOutbox(builder =>
		{
			_ = builder.WithProcessing(processing =>
			{
				_ = processing.ProcessorId(processorId);
			});
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<OutboxOptions>();
		options.ProcessorId.ShouldBe(processorId);
	}

	[Fact]
	public void WithProcessing_EnablesParallelProcessing()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddExcaliburOutbox(builder =>
		{
			_ = builder.WithProcessing(processing =>
			{
				_ = processing.EnableParallelProcessing(8);
			});
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<OutboxOptions>();
		options.EnableParallelProcessing.ShouldBeTrue();
		options.MaxDegreeOfParallelism.ShouldBe(8);
	}

	[Fact]
	public void WithProcessing_SupportsFluentChaining()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddExcaliburOutbox(builder =>
		{
			_ = builder.WithProcessing(processing =>
			{
				_ = processing
					.BatchSize(250)
					.PollingInterval(TimeSpan.FromSeconds(10))
					.MaxRetryCount(5)
					.RetryDelay(TimeSpan.FromMinutes(2))
					.ProcessorId("test-processor")
					.EnableParallelProcessing(4);
			});
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<OutboxOptions>();
		options.BatchSize.ShouldBe(250);
		options.PollingInterval.ShouldBe(TimeSpan.FromSeconds(10));
		options.MaxRetryCount.ShouldBe(5);
		options.RetryDelay.ShouldBe(TimeSpan.FromMinutes(2));
		options.ProcessorId.ShouldBe("test-processor");
		options.EnableParallelProcessing.ShouldBeTrue();
		options.MaxDegreeOfParallelism.ShouldBe(4);
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

	[Fact]
	public void WithCleanup_EnablesAutoCleanup()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddExcaliburOutbox(builder =>
		{
			_ = builder.WithCleanup(cleanup =>
			{
				_ = cleanup.EnableAutoCleanup(true);
			});
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<OutboxOptions>();
		options.EnableAutomaticCleanup.ShouldBeTrue();
	}

	[Fact]
	public void WithCleanup_DisablesAutoCleanup()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddExcaliburOutbox(builder =>
		{
			_ = builder.WithCleanup(cleanup =>
			{
				_ = cleanup.EnableAutoCleanup(false);
			});
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<OutboxOptions>();
		options.EnableAutomaticCleanup.ShouldBeFalse();
	}

	[Fact]
	public void WithCleanup_ConfiguresRetentionPeriod()
	{
		// Arrange
		var services = new ServiceCollection();
		var expectedPeriod = TimeSpan.FromDays(30);

		// Act
		_ = services.AddExcaliburOutbox(builder =>
		{
			_ = builder.WithCleanup(cleanup =>
			{
				_ = cleanup.RetentionPeriod(expectedPeriod);
			});
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<OutboxOptions>();
		options.MessageRetentionPeriod.ShouldBe(expectedPeriod);
	}

	[Fact]
	public void WithCleanup_ConfiguresCleanupInterval()
	{
		// Arrange
		var services = new ServiceCollection();
		var expectedInterval = TimeSpan.FromHours(6);

		// Act
		_ = services.AddExcaliburOutbox(builder =>
		{
			_ = builder.WithCleanup(cleanup =>
			{
				_ = cleanup.CleanupInterval(expectedInterval);
			});
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<OutboxOptions>();
		options.CleanupInterval.ShouldBe(expectedInterval);
	}

	[Fact]
	public void WithCleanup_SupportsFluentChaining()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddExcaliburOutbox(builder =>
		{
			_ = builder.WithCleanup(cleanup =>
			{
				_ = cleanup
					.EnableAutoCleanup(true)
					.RetentionPeriod(TimeSpan.FromDays(14))
					.CleanupInterval(TimeSpan.FromHours(2));
			});
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<OutboxOptions>();
		options.EnableAutomaticCleanup.ShouldBeTrue();
		options.MessageRetentionPeriod.ShouldBe(TimeSpan.FromDays(14));
		options.CleanupInterval.ShouldBe(TimeSpan.FromHours(2));
	}

	[Fact]
	public void EnableBackgroundProcessing_SetsOptionFlag()
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
	public void Builder_SupportsFullFluentChaining()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddExcaliburOutbox(builder =>
		{
			_ = builder
				.WithProcessing(p => p.BatchSize(100).PollingInterval(TimeSpan.FromSeconds(10)))
				.WithCleanup(c => c.EnableAutoCleanup(true).RetentionPeriod(TimeSpan.FromDays(7)))
				.EnableBackgroundProcessing();
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<OutboxOptions>();
		options.BatchSize.ShouldBe(100);
		options.PollingInterval.ShouldBe(TimeSpan.FromSeconds(10));
		options.EnableAutomaticCleanup.ShouldBeTrue();
		options.MessageRetentionPeriod.ShouldBe(TimeSpan.FromDays(7));
		options.EnableBackgroundProcessing.ShouldBeTrue();
	}
}
