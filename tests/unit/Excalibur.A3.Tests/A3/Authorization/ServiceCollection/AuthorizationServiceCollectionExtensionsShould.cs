// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3.Authorization;

using Microsoft.AspNetCore.Authorization;

namespace Excalibur.Tests.A3.Authorization.ServiceCollection;

/// <summary>
/// Unit tests for <see cref="AuthorizationServiceCollectionExtensions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "A3")]
public sealed class AuthorizationServiceCollectionExtensionsShould
{
	[Fact]
	public void AddDispatchAuthorization_RegistersRequiredServices()
	{
		// Arrange
		var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();

		// Act
		services.AddDispatchAuthorization();

		// Assert
		services.ShouldContain(sd => sd.ServiceType == typeof(IAuthorizationHandler));
		services.ShouldContain(sd => sd.ServiceType == typeof(IDispatchAuthorizationService));
		services.ShouldContain(sd => sd.ServiceType == typeof(AttributeAuthorizationCache));
		services.ShouldContain(sd => sd.ServiceType == typeof(IDispatchMiddleware));
	}

	[Fact]
	public void AddDispatchAuthorization_ReturnsSameServiceCollection()
	{
		// Arrange
		var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();

		// Act
		var result = services.AddDispatchAuthorization();

		// Assert
		result.ShouldBeSameAs(services);
	}

	[Fact]
	public void AddDispatchAuthorization_IsIdempotent()
	{
		// Arrange
		var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();

		// Act
		services.AddDispatchAuthorization();
		services.AddDispatchAuthorization();

		// Assert — TryAddEnumerable and TryAddSingleton should prevent duplicates
		var handlerCount = services.Count(sd => sd.ServiceType == typeof(IAuthorizationHandler));
		handlerCount.ShouldBe(1);

		var authServiceCount = services.Count(sd => sd.ServiceType == typeof(IDispatchAuthorizationService));
		authServiceCount.ShouldBe(1);
	}
}
