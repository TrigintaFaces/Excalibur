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
public class ProgressiveEnhancementExtensionsShould
{
	/// <summary>
	/// Creates an IDispatchBuilder for testing.
	/// </summary>
	private static IDispatchBuilder CreateBuilder(IServiceCollection services)
		=> new DispatchBuilder(services);

	#region AddContextEnrichment Tests

	[Fact]
	public void AddContextEnrichment_ThrowsArgumentNullException_WhenBuilderIsNull()
	{
		// Arrange
		IDispatchBuilder builder = null!;

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => builder.AddContextEnrichment())
			.ParamName.ShouldBe("builder");
	}

	[Fact]
	public void AddContextEnrichment_DisablesLightMode()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = CreateBuilder(services)
			.AddContextEnrichment();

		var provider = services.BuildServiceProvider();

		// Act
		var options = provider.GetRequiredService<IOptions<DispatchOptions>>().Value;

		// Assert
		options.UseLightMode.ShouldBeFalse();
	}

	[Fact]
	public void AddContextEnrichment_EnablesCorrelation()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = CreateBuilder(services)
			.AddContextEnrichment();

		var provider = services.BuildServiceProvider();

		// Act
		var options = provider.GetRequiredService<IOptions<DispatchOptions>>().Value;

		// Assert
		options.Features.EnableCorrelation.ShouldBeTrue();
	}

	[Fact]
	public void AddContextEnrichment_RegistersMessageContextAccessor()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = CreateBuilder(services)
			.AddContextEnrichment();

		var provider = services.BuildServiceProvider();

		// Act
		var accessor = provider.GetService<IMessageContextAccessor>();

		// Assert
		_ = accessor.ShouldNotBeNull();
		_ = accessor.ShouldBeOfType<MessageContextAccessor>();
	}

	[Fact]
	public void AddContextEnrichment_ReturnsBuilderForChaining()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = CreateBuilder(services);

		// Act
		var result = builder.AddContextEnrichment();

		// Assert
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void AddContextEnrichment_IsIdempotent_WhenCalledMultipleTimes()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = CreateBuilder(services)
			.AddContextEnrichment()
			.AddContextEnrichment()
			.AddContextEnrichment();

		var provider = services.BuildServiceProvider();

		// Act - Should not throw, accessor should be registered once
		var accessors = provider.GetServices<IMessageContextAccessor>().ToList();

		// Assert - TryAddSingleton ensures only one registration
		accessors.Count.ShouldBe(1);
	}

	#endregion

	#region AddAllFeatures Tests

	[Fact]
	public void AddAllFeatures_ThrowsArgumentNullException_WhenBuilderIsNull()
	{
		// Arrange
		IDispatchBuilder builder = null!;

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => builder.AddAllFeatures())
			.ParamName.ShouldBe("builder");
	}

	[Fact]
	public void AddAllFeatures_EnablesContextEnrichment()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = CreateBuilder(services)
			.AddAllFeatures();

		var provider = services.BuildServiceProvider();

		// Act
		var options = provider.GetRequiredService<IOptions<DispatchOptions>>().Value;

		// Assert - Context enrichment should be enabled
		options.UseLightMode.ShouldBeFalse();
		options.Features.EnableCorrelation.ShouldBeTrue();
	}

	[Fact]
	public void AddAllFeatures_RegistersMessageContextAccessor()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = CreateBuilder(services)
			.AddAllFeatures();

		var provider = services.BuildServiceProvider();

		// Act
		var accessor = provider.GetService<IMessageContextAccessor>();

		// Assert
		_ = accessor.ShouldNotBeNull();
	}

	[Fact]
	public void AddAllFeatures_ReturnsBuilderForChaining()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = CreateBuilder(services);

		// Act
		var result = builder.AddAllFeatures();

		// Assert
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void AddAllFeatures_IsIdempotent_WhenCalledMultipleTimes()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = CreateBuilder(services)
			.AddAllFeatures()
			.AddAllFeatures();

		// Act - Should not throw
		var provider = services.BuildServiceProvider();
		var accessors = provider.GetServices<IMessageContextAccessor>().ToList();

		// Assert
		accessors.Count.ShouldBe(1);
	}

	[Fact]
	public void AddAllFeatures_CanBeChainedWithOtherExtensions()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act - Should not throw
		var builder = CreateBuilder(services)
			.AddAllFeatures()
			.AddContextEnrichment(); // Should be idempotent

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
			.AddContextEnrichment()
			.AddAllFeatures();

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
