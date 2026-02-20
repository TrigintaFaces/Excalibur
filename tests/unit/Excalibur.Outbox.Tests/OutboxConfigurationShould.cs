// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Outbox.Tests;

/// <summary>
/// Unit tests for <see cref="OutboxConfiguration"/> internal class behavior through public API.
/// </summary>
[Trait("Category", "Unit")]
public sealed class OutboxConfigurationShould : UnitTestBase
{
	#region Default Value Tests

	[Fact]
	public void ToOptions_HasDefaultBatchSize()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddExcaliburOutbox(_ => { });
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<OutboxOptions>();
		options.BatchSize.ShouldBe(100);
	}

	[Fact]
	public void ToOptions_HasDefaultPollingInterval()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddExcaliburOutbox(_ => { });
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<OutboxOptions>();
		options.PollingInterval.ShouldBe(TimeSpan.FromSeconds(5));
	}

	[Fact]
	public void ToOptions_HasDefaultMaxRetryCount()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddExcaliburOutbox(_ => { });
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<OutboxOptions>();
		options.MaxRetryCount.ShouldBe(3);
	}

	[Fact]
	public void ToOptions_HasDefaultRetryDelay()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddExcaliburOutbox(_ => { });
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<OutboxOptions>();
		options.RetryDelay.ShouldBe(TimeSpan.FromMinutes(5));
	}

	[Fact]
	public void ToOptions_HasDefaultMessageRetentionPeriod()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddExcaliburOutbox(_ => { });
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<OutboxOptions>();
		options.MessageRetentionPeriod.ShouldBe(TimeSpan.FromDays(7));
	}

	[Fact]
	public void ToOptions_HasDefaultEnableAutomaticCleanup()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddExcaliburOutbox(_ => { });
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<OutboxOptions>();
		options.EnableAutomaticCleanup.ShouldBeTrue();
	}

	[Fact]
	public void ToOptions_HasDefaultCleanupInterval()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddExcaliburOutbox(_ => { });
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<OutboxOptions>();
		options.CleanupInterval.ShouldBe(TimeSpan.FromHours(1));
	}

	[Fact]
	public void ToOptions_HasDefaultEnableBackgroundProcessing()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddExcaliburOutbox(_ => { });
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<OutboxOptions>();
		options.EnableBackgroundProcessing.ShouldBeTrue();
	}

	[Fact]
	public void ToOptions_HasDefaultProcessorIdAsNull()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddExcaliburOutbox(_ => { });
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<OutboxOptions>();
		options.ProcessorId.ShouldBeNull();
	}

	[Fact]
	public void ToOptions_HasDefaultEnableParallelProcessingAsFalse()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddExcaliburOutbox(_ => { });
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<OutboxOptions>();
		options.EnableParallelProcessing.ShouldBeFalse();
	}

	[Fact]
	public void ToOptions_HasDefaultMaxDegreeOfParallelism()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddExcaliburOutbox(_ => { });
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<OutboxOptions>();
		options.MaxDegreeOfParallelism.ShouldBe(4);
	}

	#endregion

	#region ToOptions Preset Tests

	[Fact]
	public void ToOptions_UsesCustomPreset_WhenBuilderConfigured()
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
		options.Preset.ShouldBe(OutboxPreset.Custom);
	}

	#endregion

	#region Full Configuration Override Tests

	[Fact]
	public void ToOptions_OverridesAllDefaults()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddExcaliburOutbox(builder =>
		{
			_ = builder
				.WithProcessing(p => p
					.BatchSize(500)
					.PollingInterval(TimeSpan.FromSeconds(10))
					.MaxRetryCount(10)
					.RetryDelay(TimeSpan.FromMinutes(15))
					.ProcessorId("custom-processor")
					.EnableParallelProcessing(16))
				.WithCleanup(c => c
					.EnableAutoCleanup(false)
					.RetentionPeriod(TimeSpan.FromDays(30))
					.CleanupInterval(TimeSpan.FromHours(12)));
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<OutboxOptions>();
		options.BatchSize.ShouldBe(500);
		options.PollingInterval.ShouldBe(TimeSpan.FromSeconds(10));
		options.MaxRetryCount.ShouldBe(10);
		options.RetryDelay.ShouldBe(TimeSpan.FromMinutes(15));
		options.ProcessorId.ShouldBe("custom-processor");
		options.EnableParallelProcessing.ShouldBeTrue();
		options.MaxDegreeOfParallelism.ShouldBe(16);
		options.EnableAutomaticCleanup.ShouldBeFalse();
		options.MessageRetentionPeriod.ShouldBe(TimeSpan.FromDays(30));
		options.CleanupInterval.ShouldBe(TimeSpan.FromHours(12));
	}

	#endregion
}
