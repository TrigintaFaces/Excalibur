// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Resilience;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Tests.Resilience;

/// <summary>
/// Locks that a plain host actually gets a circuit breaker on the outbox and inbox drains.
/// </summary>
/// <remarks>
/// The registry had no registration in core, so those drains resolved nothing and fell back to a
/// null registry whose breakers do nothing. Nothing failed and nothing logged: the drains simply ran
/// unprotected unless the host had taken the opt-in resilience package.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class OutboxBreakerIsReachableShould
{
	[Fact]
	public void ResolveARealRegistryFromAPlainContainer()
	{
		// SAFETY: resolving the null registry here is the defect, and it is indistinguishable from
		// working code at runtime.
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddDispatchPipeline();

		using var provider = services.BuildServiceProvider();
		var registry = provider.GetService<ITransportCircuitBreakerRegistry>();

		registry.ShouldNotBeNull("a host that never opted into the resilience package still needs a breaker");
		registry.GetType().Name.ShouldNotBe(
			"NullTransportCircuitBreakerRegistry",
			"the null registry hands out breakers that never open");
	}

	[Fact]
	public void HandOutABreakerThatTracksItsOwnName()
	{
		// LIVENESS: a registry that resolves but returns nothing usable would satisfy the arm above.
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddDispatchPipeline();

		using var provider = services.BuildServiceProvider();
		var registry = provider.GetRequiredService<ITransportCircuitBreakerRegistry>();

		var first = registry.GetOrCreate("orders");
		var second = registry.GetOrCreate("orders");
		var other = registry.GetOrCreate("shipments");

		first.ShouldNotBeNull();
		second.ShouldBeSameAs(first, "one name is one shared circuit, or the breaker protects nothing");
		other.ShouldNotBeSameAs(first, "two names are two circuits");
	}
}
