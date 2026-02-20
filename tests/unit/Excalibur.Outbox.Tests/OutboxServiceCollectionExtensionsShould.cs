// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Delivery;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Outbox.Tests;

/// <summary>
/// Unit tests for <see cref="OutboxServiceCollectionExtensions"/>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class OutboxServiceCollectionExtensionsShould : UnitTestBase
{
	#region Null Argument Tests

	[Fact]
	public void AddExcaliburOutbox_WithNullServices_ThrowsArgumentNullException()
	{
		// Arrange
		IServiceCollection services = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => services.AddExcaliburOutbox());
	}

	[Fact]
	public void AddExcaliburOutbox_WithBuilderAction_ThrowsOnNullServices()
	{
		// Arrange
		IServiceCollection services = null!;
		Action<IOutboxBuilder> configure = _ => { };

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => services.AddExcaliburOutbox(configure));
	}

	[Fact]
	public void AddExcaliburOutbox_WithBuilderAction_ThrowsOnNullConfigure()
	{
		// Arrange
		var services = new ServiceCollection();
		Action<IOutboxBuilder> configure = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => services.AddExcaliburOutbox(configure));
	}

	[Fact]
	public void AddExcaliburOutbox_WithPresetOptions_ThrowsOnNullServices()
	{
		// Arrange
		IServiceCollection services = null!;
		var options = OutboxOptions.Balanced().Build();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => services.AddExcaliburOutbox(options));
	}

	[Fact]
	public void AddExcaliburOutbox_WithPresetOptions_ThrowsOnNullOptions()
	{
		// Arrange
		var services = new ServiceCollection();
		OutboxOptions options = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => services.AddExcaliburOutbox(options));
	}

	#endregion

	#region Default Registration Tests

	[Fact]
	public void AddExcaliburOutbox_RegistersBalancedOptionsAsDefault()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddExcaliburOutbox();
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetService<OutboxOptions>();
		_ = options.ShouldNotBeNull();
		options.BatchSize.ShouldBe(100);
		options.Preset.ShouldBe(OutboxPreset.Balanced);
	}

	#endregion

	#region Preset-Based Registration Tests

	[Fact]
	public void AddExcaliburOutbox_WithHighThroughputPreset_RegistersCorrectOptions()
	{
		// Arrange
		var services = new ServiceCollection();
		var presetOptions = OutboxOptions.HighThroughput().Build();

		// Act
		_ = services.AddExcaliburOutbox(presetOptions);
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<OutboxOptions>();
		options.BatchSize.ShouldBe(1000);
		options.PollingInterval.ShouldBe(TimeSpan.FromMilliseconds(100));
		options.MaxDegreeOfParallelism.ShouldBe(8);
		options.Preset.ShouldBe(OutboxPreset.HighThroughput);
	}

	[Fact]
	public void AddExcaliburOutbox_WithHighReliabilityPreset_RegistersCorrectOptions()
	{
		// Arrange
		var services = new ServiceCollection();
		var presetOptions = OutboxOptions.HighReliability().Build();

		// Act
		_ = services.AddExcaliburOutbox(presetOptions);
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<OutboxOptions>();
		options.BatchSize.ShouldBe(10);
		options.PollingInterval.ShouldBe(TimeSpan.FromSeconds(5));
		options.MaxRetryCount.ShouldBe(10);
		options.Preset.ShouldBe(OutboxPreset.HighReliability);
	}

	[Fact]
	public void AddExcaliburOutbox_WithPresetOverrides_AppliesOverrides()
	{
		// Arrange
		var services = new ServiceCollection();
		var presetOptions = OutboxOptions.HighThroughput()
			.WithBatchSize(2000)
			.WithProcessorId("worker-1")
			.Build();

		// Act
		_ = services.AddExcaliburOutbox(presetOptions);
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<OutboxOptions>();
		options.BatchSize.ShouldBe(2000);
		options.ProcessorId.ShouldBe("worker-1");
		// Other preset values should be preserved
		options.PollingInterval.ShouldBe(TimeSpan.FromMilliseconds(100));
	}

	#endregion

	#region Fluent Builder Registration Tests

	[Fact]
	public void AddExcaliburOutbox_WithBuilderAction_AppliesProcessingConfiguration()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddExcaliburOutbox(outbox =>
		{
			_ = outbox.WithProcessing(p => p
				.BatchSize(200)
				.PollingInterval(TimeSpan.FromSeconds(10)));
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<OutboxOptions>();
		options.BatchSize.ShouldBe(200);
		options.PollingInterval.ShouldBe(TimeSpan.FromSeconds(10));
	}

	[Fact]
	public void AddExcaliburOutbox_WithBuilderAction_AppliesCleanupConfiguration()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddExcaliburOutbox(outbox =>
		{
			_ = outbox.WithCleanup(c => c
				.EnableAutoCleanup(false)
				.RetentionPeriod(TimeSpan.FromDays(30)));
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<OutboxOptions>();
		options.EnableAutomaticCleanup.ShouldBeFalse();
		options.MessageRetentionPeriod.ShouldBe(TimeSpan.FromDays(30));
	}

	[Fact]
	public void AddExcaliburOutbox_WithBuilderAction_AppliesFullConfiguration()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddExcaliburOutbox(outbox =>
		{
			_ = outbox
				.WithProcessing(p => p
					.BatchSize(500)
					.PollingInterval(TimeSpan.FromSeconds(2))
					.MaxRetryCount(5)
					.RetryDelay(TimeSpan.FromMinutes(2))
					.ProcessorId("my-processor")
					.EnableParallelProcessing(8))
				.WithCleanup(c => c
					.EnableAutoCleanup(true)
					.RetentionPeriod(TimeSpan.FromDays(14))
					.CleanupInterval(TimeSpan.FromHours(6)))
				.EnableBackgroundProcessing();
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<OutboxOptions>();
		options.BatchSize.ShouldBe(500);
		options.PollingInterval.ShouldBe(TimeSpan.FromSeconds(2));
		options.MaxRetryCount.ShouldBe(5);
		options.RetryDelay.ShouldBe(TimeSpan.FromMinutes(2));
		options.ProcessorId.ShouldBe("my-processor");
		options.EnableParallelProcessing.ShouldBeTrue();
		options.MaxDegreeOfParallelism.ShouldBe(8);
		options.EnableAutomaticCleanup.ShouldBeTrue();
		options.MessageRetentionPeriod.ShouldBe(TimeSpan.FromDays(14));
		options.CleanupInterval.ShouldBe(TimeSpan.FromHours(6));
		options.EnableBackgroundProcessing.ShouldBeTrue();
	}

	#endregion

	#region HasExcaliburOutbox Tests

	[Fact]
	public void HasExcaliburOutbox_ReturnsFalse_WhenNotRegistered()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.HasExcaliburOutbox();

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void HasExcaliburOutbox_ReturnsTrue_WhenRegistered()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddExcaliburOutbox();

		// Act
		var result = services.HasExcaliburOutbox();

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void HasExcaliburOutbox_ReturnsTrue_WhenRegisteredWithPreset()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddExcaliburOutbox(OutboxOptions.HighThroughput().Build());

		// Act
		var result = services.HasExcaliburOutbox();

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void HasExcaliburOutbox_ThrowsOnNullServices()
	{
		// Arrange
		IServiceCollection services = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => services.HasExcaliburOutbox());
	}

	#endregion

	#region Method Chaining Tests

	[Fact]
	public void AddExcaliburOutbox_ReturnsServiceCollection_ForChaining()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddExcaliburOutbox();

		// Assert
		result.ShouldBeSameAs(services);
	}

	[Fact]
	public void AddExcaliburOutbox_WithBuilderAction_ReturnsServiceCollection_ForChaining()
	{
		// Arrange
		var services = new ServiceCollection();
		Action<IOutboxBuilder> configure = _ => { };

		// Act
		var result = services.AddExcaliburOutbox(configure);

		// Assert
		result.ShouldBeSameAs(services);
	}

	[Fact]
	public void AddExcaliburOutbox_WithPresetOptions_ReturnsServiceCollection_ForChaining()
	{
		// Arrange
		var services = new ServiceCollection();
		var options = OutboxOptions.Balanced().Build();

		// Act
		var result = services.AddExcaliburOutbox(options);

		// Assert
		result.ShouldBeSameAs(services);
	}

	#endregion

	#region Hosted Service Tests

	[Fact]
	public void AddOutboxHostedService_RegistersBackgroundService()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddOutboxHostedService();

		// Assert
		services.Any(s => s.ImplementationType == typeof(Excalibur.Outbox.Outbox.OutboxBackgroundService)).ShouldBeTrue();
	}

	[Fact]
	public void AddInboxHostedService_RegistersBackgroundService()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddInboxHostedService();

		// Assert
		services.Any(s => s.ImplementationType == typeof(InboxService)).ShouldBeTrue();
	}

	#endregion
}
