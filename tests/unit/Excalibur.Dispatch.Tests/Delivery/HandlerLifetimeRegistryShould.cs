// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Delivery;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Tests.Delivery;

/// <summary>
/// The lifetime registry must describe what the container will actually do, because the scope verdict —
/// and therefore whether a dependency-injection scope is created per dispatch — rests entirely on it.
/// </summary>
/// <remarks>
/// <para>
/// Microsoft DI resolves a service type from the <em>last</em> registered descriptor for it; that is why
/// <c>TryAdd*</c> exists as the explicit opt-out. A registry that instead treated Scoped as "sticky"
/// disagreed with the provider it described: a consumer's explicit <c>AddTransient</c> was silently
/// discarded, and a scope was created to contain a captive dependency the container could never produce.
/// </para>
/// <para>
/// These arms bind both directions. The safety arm — a genuinely scoped handler still reports Scoped —
/// is what keeps the captive-dependency protection intact; without it, a change that simply reported the
/// cheapest lifetime everywhere would pass the liveness arm while reintroducing the failure the scope
/// machinery exists to prevent.
/// </para>
/// </remarks>
public sealed class HandlerLifetimeRegistryShould
{
	private sealed class ProbeHandler
	{
	}

	private interface IProbeService
	{
	}

	private sealed class ProbeService : IProbeService
	{
	}

	private static HandlerLifetimeRegistry RegistryFor(Action<ServiceCollection> configure)
	{
		var services = new ServiceCollection();
		configure(services);
		return new HandlerLifetimeRegistry(services);
	}

	[Fact]
	public void HonorALaterRegistrationThatSupersedesAnEarlierScopedOne()
	{
		// LIVENESS. This is the case that regressed: discovery registers a handler Scoped, the consumer
		// then registers it Transient. GetService returns a transient instance, so the earlier descriptor
		// is unreachable and the registry must not keep reporting Scoped.
		var registry = RegistryFor(services =>
		{
			_ = services.AddScoped<ProbeHandler>();
			_ = services.AddTransient<ProbeHandler>();
		});

		registry.TryGetLifetime(typeof(ProbeHandler), out var lifetime).ShouldBeTrue();
		lifetime.ShouldBe(
			ServiceLifetime.Transient,
			"Microsoft DI resolves a service type from its LAST descriptor, so the later AddTransient wins "
			+ "and the registry must agree with the provider rather than reporting a lifetime the container "
			+ "will never use");
	}

	[Fact]
	public void ReportScopedForAHandlerThatIsGenuinelyScoped()
	{
		// SAFETY. The captive-dependency protection depends on this staying true. A registry that reported
		// the cheapest lifetime everywhere would satisfy the liveness arm above and reintroduce the
		// "Cannot resolve scoped service from root provider" failure in consumer apps.
		var registry = RegistryFor(services => services.AddScoped<ProbeHandler>());

		registry.TryGetLifetime(typeof(ProbeHandler), out var lifetime).ShouldBeTrue();
		lifetime.ShouldBe(ServiceLifetime.Scoped);
	}

	[Fact]
	public void FallBackToTheImplementationTypeWhenItIsNeverRegisteredAsAService()
	{
		// The AddScoped<IService, Impl>() shape: GetService(Impl) returns null and the activator constructs
		// the handler directly, so the implementation type carries the only lifetime information available.
		var registry = RegistryFor(services => services.AddScoped<IProbeService, ProbeService>());

		registry.TryGetLifetime(typeof(ProbeService), out var lifetime).ShouldBeTrue(
			"the concrete type is not registered as a service in its own right, so its lifetime must still "
			+ "be discoverable through the registration that names it as an implementation");
		lifetime.ShouldBe(ServiceLifetime.Scoped);
	}

	[Fact]
	public void NotLetAnImplementationTypeFallbackOverrideARealServiceRegistration()
	{
		// The fallback must never outrank a real registration, or last-wins breaks for any handler
		// registered both ways — which is exactly how the regression reached the ultra-local path.
		var registry = RegistryFor(services =>
		{
			_ = services.AddTransient<ProbeService>();
			_ = services.AddScoped<IProbeService, ProbeService>();
		});

		registry.TryGetLifetime(typeof(ProbeService), out var lifetime).ShouldBeTrue();
		lifetime.ShouldBe(
			ServiceLifetime.Transient,
			"GetService(ProbeService) resolves the AddTransient descriptor; the scoped registration binds "
			+ "IProbeService, not the concrete type, so it must not override the concrete type's lifetime");
	}

	[Fact]
	public void ResolveDiscoveredHandlersWithoutAScopeWhenTheyCannotCaptureAnything()
	{
		// This arm replaces a characterisation test that asserted the OPPOSITE, and it changed because the
		// discovery default changed from Scoped to Transient — the migration its own comment demanded:
		// "If this test starts failing, the default changed, and the docs and the published figures must
		// move with it."
		//
		// The old behaviour: discovery registered handlers Scoped, DI refuses to resolve a Scoped service
		// from the root provider, so a scope was created per dispatch even for a handler with no
		// constructor arguments — one that can capture nothing, and for which the scope was pure cost.
		//
		// Why the default moved (SoftwareArchitect ruling, ADR-level): scope ownership belongs to the
		// FRAMEWORK, not to the registration. HandlerScopeResolver walks the handler's constructor
		// dependency graph and opens a scope whenever a Scoped service is reachable — directly,
		// transitively, or unprovably. So Scoped registration was buying protection the resolver already
		// provides, and charging a per-dispatch scope for it. Measured: a Transient handler with a scoped
		// dependency, dispatched twice from the root provider, yields DISTINCT instances — the promise at
		// docs-site/docs/core-concepts/dependency-injection.md holds without the Scoped registration.
		//
		// The partner arm below is what stops this one being satisfied by a resolver that simply reports
		// Root for everything. Both must hold: no scope when nothing can be captured, a scope when
		// something can.
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddDispatch(dispatch =>
			dispatch.AddHandlersFromAssembly(typeof(HandlerLifetimeRegistryShould).Assembly));

		using var provider = services.BuildServiceProvider();
		var resolver = new HandlerScopeResolver(provider);

		resolver.CanCreateScope.ShouldBeTrue(
			"Microsoft DI registers IServiceScopeFactory by default; without it RequiresScope short-circuits "
			+ "to false and this arm would pass vacuously");

		resolver.RequiresScope(typeof(DiscoveryProbeHandler)).ShouldBeFalse(
			"discovery now registers handlers Transient, and this handler takes no constructor arguments, so "
			+ "there is nothing it could capture — creating a dependency-injection scope per dispatch would "
			+ "be pure cost with no safety bought");
	}

	[Fact]
	public void StillResolveDiscoveredHandlersThroughAScopeWhenTheyReachAScopedDependency()
	{
		// SAFETY PARTNER to the arm above. Transient registration must not mean "never scope" — the
		// resolver still has to open one whenever the handler's dependency graph reaches a Scoped service,
		// or the default flip would trade a per-dispatch allocation for a silent captive dependency, which
		// is the far worse defect. Without this arm, a resolver that reported Root unconditionally would
		// satisfy every other assertion in this file.
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddScoped<IProbeService, ProbeService>();
		_ = services.AddDispatch(dispatch =>
			dispatch.AddHandlersFromAssembly(typeof(HandlerLifetimeRegistryShould).Assembly));

		using var provider = services.BuildServiceProvider();
		var resolver = new HandlerScopeResolver(provider);

		resolver.RequiresScope(typeof(ScopedDependencyProbeHandler)).ShouldBeTrue(
			"the handler takes a Scoped dependency, so resolving it from the root provider would produce a "
			+ "captive dependency shared across every dispatch — the resolver must open a scope regardless "
			+ "of the handler's own registration lifetime");
	}

	/// <summary>A handler whose constructor reaches a Scoped service, for the safety arm above.</summary>
	private sealed record ScopedDependencyProbeAction : IDispatchAction;

	private sealed class ScopedDependencyProbeHandler(IProbeService probe) : IActionHandler<ScopedDependencyProbeAction>
	{
		private readonly IProbeService _probe = probe;

		public Task HandleAsync(ScopedDependencyProbeAction message, CancellationToken cancellationToken) =>
			_probe is null ? Task.FromException(new InvalidOperationException("probe missing")) : Task.CompletedTask;
	}

	/// <summary>A dependency-free handler for the discovery arm above to find.</summary>
	private sealed record DiscoveryProbeAction : IDispatchAction;

	private sealed class DiscoveryProbeHandler : IActionHandler<DiscoveryProbeAction>
	{
		public Task HandleAsync(DiscoveryProbeAction message, CancellationToken cancellationToken) =>
			Task.CompletedTask;
	}
}
