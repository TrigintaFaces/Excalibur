// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3;
using Excalibur.A3.Audit;
using Excalibur.A3.Authorization;

namespace Excalibur.Tests.A3;

/// <summary>
/// Depth unit tests for the main A3 <see cref="ServiceCollectionExtensions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "A3")]
public sealed class ServiceCollectionExtensionsDepthShould
{
	[Fact]
	public void AddA3DispatchServices_RegistersAuditMiddleware()
	{
		// Arrange
		var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();

		// Act
		services.AddA3DispatchServices();

		// Assert — AuditMiddleware should be registered as IDispatchMiddleware
		services.ShouldContain(sd => sd.ServiceType == typeof(IDispatchMiddleware)
			&& sd.ImplementationType == typeof(AuditMiddleware));
	}

	[Fact]
	public void AddA3DispatchServices_RegistersAuthorizationServices()
	{
		// Arrange
		var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();

		// Act
		services.AddA3DispatchServices();

		// Assert
		services.ShouldContain(sd => sd.ServiceType == typeof(IDispatchAuthorizationService));
		services.ShouldContain(sd => sd.ServiceType == typeof(AttributeAuthorizationCache));
	}

	[Fact]
	public void AddA3DispatchServices_ReturnsSameServiceCollection()
	{
		// Arrange
		var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();

		// Act
		var result = services.AddA3DispatchServices();

		// Assert
		result.ShouldBeSameAs(services);
	}

	[Fact]
	public void AddA3DispatchServices_RegistersAuthorizationMiddleware()
	{
		// Arrange
		var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();

		// Act
		services.AddA3DispatchServices();

		// Assert — there should be two IDispatchMiddleware registrations (Audit + Authorization)
		var middlewareRegistrations = services.Where(sd => sd.ServiceType == typeof(IDispatchMiddleware)).ToList();
		middlewareRegistrations.Count.ShouldBeGreaterThanOrEqualTo(2);
	}
}
