// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Configuration;
using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Validation;

using IDispatchMiddleware = global::Excalibur.Dispatch.Abstractions.IDispatchMiddleware;
using Excalibur.Dispatch.Middleware.Validation;

using ValidationBuilderExt = Excalibur.Dispatch.Validation.ValidationDispatchBuilderExtensions;

namespace Excalibur.Dispatch.Tests.Validation;

/// <summary>
/// Unit tests for <see cref="ValidationDispatchBuilderExtensions"/>.
/// Verifies the Sprint 720 fix (rak4tc) where UseValidation now
/// registers ValidationMiddleware into the pipeline.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class ValidationDispatchBuilderExtensionsShould
{
	[Fact]
	public void UseValidation_ThrowsArgumentNullException_WhenBuilderIsNull()
	{
		// Arrange
		IDispatchBuilder builder = null!;

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => ValidationBuilderExt.UseValidation(builder))
			.ParamName.ShouldBe("builder");
	}

	[Fact]
	public void UseValidation_ReturnsBuilderForFluentChaining()
	{
		// Arrange
		var services = new ServiceCollection();
		using var builder = new DispatchBuilder(services);

		// Act
		var result = ValidationBuilderExt.UseValidation(builder);

		// Assert
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void UseValidation_RegistersValidationMiddlewareInPipeline()
	{
		// Arrange
		var services = new ServiceCollection();
		using var builder = new DispatchBuilder(services);

		// Act
		ValidationBuilderExt.UseValidation(builder);

		// Assert - UseMiddleware<T> registers the middleware type as scoped
		services.Any(sd =>
			sd.ServiceType == typeof(ValidationMiddleware) &&
			sd.Lifetime == ServiceLifetime.Scoped)
			.ShouldBeTrue("UseValidation should register ValidationMiddleware via UseMiddleware<T>");
	}

	[Fact]
	public void UseValidation_RegistersValidatorResolver()
	{
		// Arrange
		var services = new ServiceCollection();
		using var builder = new DispatchBuilder(services);

		// Act
		ValidationBuilderExt.UseValidation(builder);

		// Assert
		services.Any(sd =>
			sd.ServiceType == typeof(IValidatorResolver))
			.ShouldBeTrue("UseValidation should register IValidatorResolver");
	}

	[Fact]
	public void UseValidation_RegistersValidationService()
	{
		// Arrange
		var services = new ServiceCollection();
		using var builder = new DispatchBuilder(services);

		// Act
		ValidationBuilderExt.UseValidation(builder);

		// Assert
		services.Any(sd =>
			sd.ServiceType == typeof(IValidationService))
			.ShouldBeTrue("UseValidation should register IValidationService");
	}

	[Fact]
	public void UseValidation_IsIdempotent_WhenCalledMultipleTimes()
	{
		// Arrange
		var services = new ServiceCollection();
		using var builder = new DispatchBuilder(services);

		// Act
		var b = ValidationBuilderExt.UseValidation(builder);
		ValidationBuilderExt.UseValidation(b);
		ValidationBuilderExt.UseValidation(b);

		// Assert - IValidatorResolver should use TryAdd, so only one registration
		var resolverCount = services.Count(sd =>
			sd.ServiceType == typeof(IValidatorResolver));
		resolverCount.ShouldBe(1);
	}

	[Fact]
	public void WithCustomValidators_ThrowsArgumentNullException_WhenBuilderIsNull()
	{
		// Arrange
		IDispatchBuilder builder = null!;

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			ValidationBuilderExt.WithCustomValidators(builder, _ => { }))
			.ParamName.ShouldBe("builder");
	}

	[Fact]
	public void WithCustomValidators_ThrowsArgumentNullException_WhenDelegateIsNull()
	{
		// Arrange
		var services = new ServiceCollection();
		using var builder = new DispatchBuilder(services);

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			ValidationBuilderExt.WithCustomValidators(builder, null!))
			.ParamName.ShouldBe("registerValidators");
	}

	[Fact]
	public void WithCustomValidators_InvokesDelegateWithBuilderServices()
	{
		// Arrange
		var services = new ServiceCollection();
		using var builder = new DispatchBuilder(services);
		IServiceCollection? capturedServices = null;

		// Act
		ValidationBuilderExt.WithCustomValidators(builder, svc => capturedServices = svc);

		// Assert
		capturedServices.ShouldBeSameAs(services);
	}
}
