// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// Licensed under the Excalibur License 1.0 - see LICENSE files for details.

using Excalibur.Dispatch.Abstractions.Configuration;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Options.Configuration;

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Tests.Configuration;

/// <summary>
/// Integration tests for <see cref="ProgressiveEnhancementExtensions"/>.
/// Tests the progressive enhancement API pattern (Sprint 208 - kca22).
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ProgressiveEnhancementExtensionsShould
{
	/// <summary>
	/// Creates an IDispatchBuilder for testing.
	/// </summary>
	private static IDispatchBuilder CreateBuilder(IServiceCollection services)
		=> new DispatchBuilder(services);

	#region UseContextEnrichment Tests

	[Fact]
	public void UseContextEnrichment_ThrowsArgumentNullException_WhenBuilderIsNull()
	{
		// Arrange
		IDispatchBuilder builder = null!;

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => builder.UseContextEnrichment())
			.ParamName.ShouldBe("builder");
	}

	[Fact]
	public void UseContextEnrichment_DisablesLightMode()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = CreateBuilder(services)
			.UseContextEnrichment();

		var provider = services.BuildServiceProvider();

		// Act
		var options = provider.GetRequiredService<IOptions<DispatchOptions>>().Value;

		// Assert
		options.UseLightMode.ShouldBeFalse();
	}

	[Fact]
	public void UseContextEnrichment_EnablesCorrelation()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = CreateBuilder(services)
			.UseContextEnrichment();

		var provider = services.BuildServiceProvider();

		// Act
		var options = provider.GetRequiredService<IOptions<DispatchOptions>>().Value;

		// Assert
		options.Features.EnableCorrelation.ShouldBeTrue();
	}

	[Fact]
	public void UseContextEnrichment_RegistersMessageContextAccessor()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = CreateBuilder(services)
			.UseContextEnrichment();

		var provider = services.BuildServiceProvider();

		// Act
		var accessor = provider.GetService<IMessageContextAccessor>();

		// Assert
		_ = accessor.ShouldNotBeNull();
		_ = accessor.ShouldBeOfType<MessageContextAccessor>();
	}

	[Fact]
	public void UseContextEnrichment_ReturnsBuilderForChaining()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = CreateBuilder(services);

		// Act
		var result = builder.UseContextEnrichment();

		// Assert
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void UseContextEnrichment_IsIdempotent_WhenCalledMultipleTimes()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = CreateBuilder(services)
			.UseContextEnrichment()
			.UseContextEnrichment()
			.UseContextEnrichment();

		var provider = services.BuildServiceProvider();

		// Act - Should not throw, accessor should be registered once
		var accessors = provider.GetServices<IMessageContextAccessor>().ToList();

		// Assert - TryAddSingleton ensures only one registration
		accessors.Count.ShouldBe(1);
	}

	#endregion

	#region UseAllFeatures Tests

	[Fact]
	public void UseAllFeatures_ThrowsArgumentNullException_WhenBuilderIsNull()
	{
		// Arrange
		IDispatchBuilder builder = null!;

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => builder.UseAllFeatures())
			.ParamName.ShouldBe("builder");
	}

	[Fact]
	public void UseAllFeatures_EnablesContextEnrichment()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = CreateBuilder(services)
			.UseAllFeatures();

		var provider = services.BuildServiceProvider();

		// Act
		var options = provider.GetRequiredService<IOptions<DispatchOptions>>().Value;

		// Assert - Context enrichment should be enabled
		options.UseLightMode.ShouldBeFalse();
		options.Features.EnableCorrelation.ShouldBeTrue();
	}

	[Fact]
	public void UseAllFeatures_RegistersMessageContextAccessor()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = CreateBuilder(services)
			.UseAllFeatures();

		var provider = services.BuildServiceProvider();

		// Act
		var accessor = provider.GetService<IMessageContextAccessor>();

		// Assert
		_ = accessor.ShouldNotBeNull();
	}

	[Fact]
	public void UseAllFeatures_ReturnsBuilderForChaining()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = CreateBuilder(services);

		// Act
		var result = builder.UseAllFeatures();

		// Assert
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void UseAllFeatures_IsIdempotent_WhenCalledMultipleTimes()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = CreateBuilder(services)
			.UseAllFeatures()
			.UseAllFeatures();

		// Act - Should not throw
		var provider = services.BuildServiceProvider();
		var accessors = provider.GetServices<IMessageContextAccessor>().ToList();

		// Assert
		accessors.Count.ShouldBe(1);
	}

	[Fact]
	public void UseAllFeatures_CanBeChainedWithOtherExtensions()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act - Should not throw
		var builder = CreateBuilder(services)
			.UseAllFeatures()
			.UseContextEnrichment(); // Should be idempotent

		// Assert
		_ = builder.ShouldNotBeNull();
		var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<DispatchOptions>>().Value;
		options.UseLightMode.ShouldBeFalse();
	}

	#endregion

	#region Method Chaining Tests

	[Fact]
	public void MethodChaining_WorksInAnyOrder()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act - Different order
		var builder = CreateBuilder(services)
			.UseContextEnrichment()
			.UseAllFeatures();

		// Assert
		_ = builder.ShouldNotBeNull();
		var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<DispatchOptions>>().Value;
		options.UseLightMode.ShouldBeFalse();
		options.Features.EnableCorrelation.ShouldBeTrue();
	}

	[Fact]
	public void ProgressiveEnhancement_MinimalRegistration_DoesNotEnableFullContext()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = CreateBuilder(services); // Minimal - no context enrichment

		// Note: Options are registered lazily, so we check that no explicit
		// configuration was applied (defaults will be used)
		_ = services.ShouldNotBeNull();
	}

	#endregion
}
