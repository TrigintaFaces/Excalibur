// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Middleware.Validation;

using IDispatchMiddleware = global::Excalibur.Dispatch.Abstractions.IDispatchMiddleware;

using ValidationBuilderExt = Excalibur.Dispatch.Validation.ValidationDispatchBuilderExtensions;

using static Microsoft.Extensions.DependencyInjection.DispatchServiceCollectionExtensions;

namespace Excalibur.Dispatch.Tests.DependencyInjection;

/// <summary>
/// Tests for the DispatchBuilderSentinel guard pattern (Sprint 720 rak4tc).
/// When AddDispatch(configure) is called, a sentinel is registered to prevent
/// subsequent parameterless AddDispatch() from overwriting the configured middleware invoker.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class DispatchBuilderSentinelShould
{
	[Fact]
	public void BeRegistered_WhenBuilderBasedAddDispatchIsCalled()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddDispatch(dispatch => ValidationBuilderExt.UseValidation(dispatch));

		// Assert
		services.Any(sd => sd.ServiceType == typeof(DispatchBuilderSentinel))
			.ShouldBeTrue("AddDispatch(configure) should register a DispatchBuilderSentinel");
	}

	[Fact]
	public void NotBeRegistered_WhenParameterlessAddDispatchIsCalled()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddDispatch();

		// Assert
		services.Any(sd => sd.ServiceType == typeof(DispatchBuilderSentinel))
			.ShouldBeFalse("Parameterless AddDispatch() should not register a sentinel");
	}

	[Fact]
	public void PreventMiddlewareOverwrite_WhenParameterlessAddDispatchFollowsBuilderBased()
	{
		// Arrange - configure with validation middleware via builder
		var services = new ServiceCollection();
		services.AddDispatch(dispatch => ValidationBuilderExt.UseValidation(dispatch));

		// Act - subsequent parameterless AddDispatch (e.g., from AddExcaliburEventSourcing)
		services.AddDispatch();

		// Assert - ValidationMiddleware should still be registered (sentinel prevented overwrite)
		services.Any(sd =>
			sd.ServiceType == typeof(ValidationMiddleware) &&
			sd.Lifetime == ServiceLifetime.Scoped)
			.ShouldBeTrue("Sentinel guard should prevent parameterless AddDispatch() from replacing configured middleware");
	}

	[Fact]
	public void AllowPipelineCreation_WhenNoSentinelExists()
	{
		// Arrange & Act - parameterless call without prior builder
		var services = new ServiceCollection();
		services.AddDispatch();

		// Assert - should complete without error, sentinel should NOT be registered
		services.Any(sd => sd.ServiceType == typeof(DispatchBuilderSentinel))
			.ShouldBeFalse("Parameterless AddDispatch() should not register a sentinel");
	}
}
