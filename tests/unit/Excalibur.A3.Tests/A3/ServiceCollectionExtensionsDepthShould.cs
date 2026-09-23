// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

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
	public void AddA3DispatchServices_DoesNotRegisterAuditMiddleware()
	{
		// This arm was INVERTED, and the inversion is the point rather than a maintenance edit. It used to
		// assert that composing authorization also composes audit. That coupling made an opt-in feature a
		// prerequisite: AuditMiddleware takes an IAuditMessagePublisher, which nothing in this framework
		// registers, so every A3 consumer had to supply an audit destination to build a container at all --
		// for a middleware that returns straight through for anything that is not IAmAuditable.
		var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();

		services.AddA3DispatchServices();

		services.ShouldNotContain(sd => sd.ServiceType == typeof(IDispatchMiddleware)
			&& sd.ImplementationType == typeof(AuditMiddleware));
	}

	[Fact]
	public async Task AddExcaliburAudit_RegistersAuditMiddleware()
	{
		// LIVENESS, and it is what stops the arm above from being satisfied by deleting the feature. The
		// capability still exists and still reaches the pipeline; what changed is which call composes it.
		//
		// Asserts the PROPERTY (an AuditMiddleware reaches the pipeline) rather than the MECHANISM
		// (a descriptor whose ImplementationType is AuditMiddleware). The registration is now a factory,
		// so ImplementationType is null and the old shape-keyed assertion reported absence for a
		// middleware that is present and resolvable -- a false RED on a working feature.
		var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddSingleton(A.Fake<IAuditMessagePublisher>());

		services.AddExcaliburAudit();

		// Async disposal throughout: DefaultOutboxDispatcher implements IAsyncDisposable only, so a
		// synchronous using on the scope throws at teardown even when the assertion has already passed.
		await using var provider = services.BuildServiceProvider();
		await using var scope = provider.CreateAsyncScope();
		scope.ServiceProvider.GetServices<IDispatchMiddleware>()
			.OfType<AuditMiddleware>()
			.Count()
			.ShouldBe(1);
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

		// Assert on the middleware this arm is NAMED for, rather than on how many middlewares happen to be
		// registered. The previous assertion counted IDispatchMiddleware descriptors and required at least
		// two, which passed for the wrong reason: it was satisfied by the audit middleware this composition
		// no longer registers, and it would have kept passing had the authorization middleware been dropped
		// while some third one was added. The name said authorization; the predicate said "two of anything".
		services.ShouldContain(sd => sd.ServiceType == typeof(IDispatchMiddleware)
			&& sd.ImplementationType == typeof(A3AuthorizationMiddleware));
	}
}
