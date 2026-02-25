// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Options.Delivery;

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Tests.DependencyInjection;

/// <summary>
/// Tests for <see cref="OutboxPresetServiceCollectionExtensions"/>.
/// Covers all three presets (HighThroughput, Balanced, HighReliability),
/// null guards, custom configure callback, and method chaining.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class OutboxPresetServiceCollectionExtensionsShould
{
	private static OutboxOptions ResolveOptions(IServiceCollection services)
	{
		var provider = services.BuildServiceProvider();
		return provider.GetRequiredService<IOptions<OutboxOptions>>().Value;
	}

	// --- HighThroughput ---

	[Fact]
	public void AddOutboxHighThroughput_ThrowsWhenServicesIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			OutboxPresetServiceCollectionExtensions.AddOutboxHighThroughput(null!));
	}

	[Fact]
	public void AddOutboxHighThroughput_ConfiguresCorrectPreset()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddOutboxHighThroughput();

		// Assert
		var options = ResolveOptions(services);
		options.PerRunTotal.ShouldBe(10000);
		options.ProducerBatchSize.ShouldBe(1000);
		options.ConsumerBatchSize.ShouldBe(1000);
		options.ParallelProcessingDegree.ShouldBe(8);
		options.EnableDynamicBatchSizing.ShouldBeTrue();
		options.DeliveryGuarantee.ShouldBe(OutboxDeliveryGuarantee.AtLeastOnce);
	}

	[Fact]
	public void AddOutboxHighThroughput_AllowsCustomization()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddOutboxHighThroughput(opts =>
		{
			opts.ParallelProcessingDegree = 4;
		});

		// Assert
		var options = ResolveOptions(services);
		options.ParallelProcessingDegree.ShouldBe(4);
		// Other preset values should be preserved
		options.PerRunTotal.ShouldBe(10000);
		options.ProducerBatchSize.ShouldBe(1000);
	}

	[Fact]
	public void AddOutboxHighThroughput_ReturnsSameServiceCollection()
	{
		var services = new ServiceCollection();
		var result = services.AddOutboxHighThroughput();
		result.ShouldBeSameAs(services);
	}

	// --- Balanced ---

	[Fact]
	public void AddOutboxBalanced_ThrowsWhenServicesIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			OutboxPresetServiceCollectionExtensions.AddOutboxBalanced(null!));
	}

	[Fact]
	public void AddOutboxBalanced_ConfiguresCorrectPreset()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddOutboxBalanced();

		// Assert
		var options = ResolveOptions(services);
		options.PerRunTotal.ShouldBe(1000);
		options.ProducerBatchSize.ShouldBe(100);
		options.ConsumerBatchSize.ShouldBe(100);
		options.ParallelProcessingDegree.ShouldBe(4);
		options.EnableDynamicBatchSizing.ShouldBeFalse();
		options.DeliveryGuarantee.ShouldBe(OutboxDeliveryGuarantee.AtLeastOnce);
	}

	[Fact]
	public void AddOutboxBalanced_AllowsCustomization()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddOutboxBalanced(opts =>
		{
			opts.MaxAttempts = 10;
		});

		// Assert
		var options = ResolveOptions(services);
		options.MaxAttempts.ShouldBe(10);
		options.PerRunTotal.ShouldBe(1000);
	}

	[Fact]
	public void AddOutboxBalanced_ReturnsSameServiceCollection()
	{
		var services = new ServiceCollection();
		var result = services.AddOutboxBalanced();
		result.ShouldBeSameAs(services);
	}

	// --- HighReliability ---

	[Fact]
	public void AddOutboxHighReliability_ThrowsWhenServicesIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			OutboxPresetServiceCollectionExtensions.AddOutboxHighReliability(null!));
	}

	[Fact]
	public void AddOutboxHighReliability_ConfiguresCorrectPreset()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddOutboxHighReliability();

		// Assert
		var options = ResolveOptions(services);
		options.PerRunTotal.ShouldBe(100);
		options.ProducerBatchSize.ShouldBe(10);
		options.ConsumerBatchSize.ShouldBe(10);
		options.ParallelProcessingDegree.ShouldBe(1);
		options.EnableDynamicBatchSizing.ShouldBeFalse();
		options.EnableBatchDatabaseOperations.ShouldBeFalse();
		options.DeliveryGuarantee.ShouldBe(OutboxDeliveryGuarantee.MinimizedWindow);
	}

	[Fact]
	public void AddOutboxHighReliability_AllowsCustomization()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddOutboxHighReliability(opts =>
		{
			opts.MaxAttempts = 15;
			opts.BatchProcessingTimeout = TimeSpan.FromMinutes(3);
		});

		// Assert
		var options = ResolveOptions(services);
		options.MaxAttempts.ShouldBe(15);
		options.BatchProcessingTimeout.ShouldBe(TimeSpan.FromMinutes(3));
		// Other preset values preserved
		options.PerRunTotal.ShouldBe(100);
	}

	[Fact]
	public void AddOutboxHighReliability_ReturnsSameServiceCollection()
	{
		var services = new ServiceCollection();
		var result = services.AddOutboxHighReliability();
		result.ShouldBeSameAs(services);
	}

	// --- Null configure callbacks ---

	[Fact]
	public void AddOutboxHighThroughput_NullConfigure_DoesNotThrow()
	{
		var services = new ServiceCollection();
		Should.NotThrow(() => services.AddOutboxHighThroughput(null));
	}

	[Fact]
	public void AddOutboxBalanced_NullConfigure_DoesNotThrow()
	{
		var services = new ServiceCollection();
		Should.NotThrow(() => services.AddOutboxBalanced(null));
	}

	[Fact]
	public void AddOutboxHighReliability_NullConfigure_DoesNotThrow()
	{
		var services = new ServiceCollection();
		Should.NotThrow(() => services.AddOutboxHighReliability(null));
	}
}
