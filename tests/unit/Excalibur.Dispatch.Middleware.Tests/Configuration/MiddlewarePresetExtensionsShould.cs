// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Configuration;
using Excalibur.Dispatch.Middleware;
using Excalibur.Dispatch.Middleware.Auth;
using Excalibur.Dispatch.Middleware.Resilience;
using Excalibur.Dispatch.Middleware.Timeout;
using Excalibur.Dispatch.Middleware.Validation;

namespace Excalibur.Dispatch.Middleware.Tests.Configuration;

/// <summary>
/// Unit tests for MiddlewarePresetExtensions including fine-grained stacks (Sprint 656 Q.2).
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class MiddlewarePresetExtensionsShould : UnitTestBase
{
	[Fact]
	public void UseDevelopmentMiddleware_ThrowsArgumentNullException_WhenBuilderIsNull()
	{
		// Arrange
		IDispatchBuilder builder = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => builder.UseDevelopmentMiddleware());
	}

	[Fact]
	public void UseProductionMiddleware_ThrowsArgumentNullException_WhenBuilderIsNull()
	{
		// Arrange
		IDispatchBuilder builder = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => builder.UseProductionMiddleware());
	}

	[Fact]
	public void UseFullMiddleware_ThrowsArgumentNullException_WhenBuilderIsNull()
	{
		// Arrange
		IDispatchBuilder builder = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => builder.UseFullMiddleware());
	}

	#region Q.2: UseSecurityStack

	[Fact]
	public void UseSecurityStack_ThrowsArgumentNullException_WhenBuilderIsNull()
	{
		// Arrange
		IDispatchBuilder builder = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => builder.UseSecurityStack());
	}

	[Fact]
	public void UseSecurityStack_RegistersSecurityMiddlewareViaAddDispatch()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddDispatch(builder => builder.UseSecurityStack());

		// Assert -- the security stack registers Auth, Authz, and TenantIdentity middleware
		services.ShouldContain(d => d.ServiceType == typeof(AuthenticationMiddleware));
		services.ShouldContain(d => d.ServiceType == typeof(AuthorizationMiddleware));
		services.ShouldContain(d => d.ServiceType == typeof(TenantIdentityMiddleware));
	}

	[Fact]
	public void UseSecurityStack_CanComposeWithOtherStacks()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act -- compose security + resilience stacks
		services.AddDispatch(builder =>
		{
			_ = builder.UseSecurityStack();
			_ = builder.UseResilienceStack();
		});

		// Assert -- both stacks' middleware registered
		services.ShouldContain(d => d.ServiceType == typeof(AuthenticationMiddleware));
		services.ShouldContain(d => d.ServiceType == typeof(RetryMiddleware));
	}

	#endregion

	#region Q.2: UseResilienceStack

	[Fact]
	public void UseResilienceStack_ThrowsArgumentNullException_WhenBuilderIsNull()
	{
		// Arrange
		IDispatchBuilder builder = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => builder.UseResilienceStack());
	}

	[Fact]
	public void UseResilienceStack_RegistersResilienceMiddlewareViaAddDispatch()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddDispatch(builder => builder.UseResilienceStack());

		// Assert -- the resilience stack registers Timeout, Retry, and CircuitBreaker middleware
		services.ShouldContain(d => d.ServiceType == typeof(TimeoutMiddleware));
		services.ShouldContain(d => d.ServiceType == typeof(RetryMiddleware));
		services.ShouldContain(d => d.ServiceType == typeof(CircuitBreakerMiddleware));
	}

	#endregion

	#region Q.2: UseValidationStack

	[Fact]
	public void UseValidationStack_ThrowsArgumentNullException_WhenBuilderIsNull()
	{
		// Arrange
		IDispatchBuilder builder = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => builder.UseValidationStack());
	}

	[Fact]
	public void UseValidationStack_RegistersValidationMiddlewareViaAddDispatch()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddDispatch(builder => builder.UseValidationStack());

		// Assert -- the validation stack registers ValidationMiddleware as singleton
		services.ShouldContain(d =>
			d.ServiceType == typeof(ValidationMiddleware) &&
			d.Lifetime == ServiceLifetime.Singleton);
	}

	[Fact]
	public void UseValidationStack_IsIdempotent_WhenCalledMultipleTimes()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act -- calling twice should not duplicate registrations (TryAddSingleton)
		services.AddDispatch(builder =>
		{
			_ = builder.UseValidationStack();
			_ = builder.UseValidationStack();
		});

		// Assert
		var validationRegistrations = services.Count(d =>
			d.ServiceType == typeof(ValidationMiddleware) &&
			d.Lifetime == ServiceLifetime.Singleton);
		validationRegistrations.ShouldBe(1);
	}

	#endregion
}
