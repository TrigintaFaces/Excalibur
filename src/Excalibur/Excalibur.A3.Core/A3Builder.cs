// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.A3.Authorization;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Excalibur.A3;

/// <summary>
/// Default implementation of <see cref="IA3Builder"/>.
/// </summary>
/// <remarks>
/// Follows the Microsoft ASP.NET Core Identity <c>IdentityBuilder</c> pattern.
/// </remarks>
internal sealed class A3Builder : IA3Builder
{
	/// <summary>
	/// Initializes a new instance of the <see cref="A3Builder"/> class.
	/// </summary>
	/// <param name="services">The service collection being configured.</param>
	public A3Builder(IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);
		Services = services;
	}

	/// <inheritdoc />
	public IServiceCollection Services { get; }

	/// <inheritdoc />
	public IA3Builder UseGrantStore<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TStore>() where TStore : class, IGrantStore
	{
		Services.Replace(ServiceDescriptor.Scoped<IGrantStore, TStore>());

		// A store that also carries the activity-group grant operations is registered for that contract too,
		// resolving THE SAME instance rather than constructing a second one -- a second in-memory store would
		// hold a different set of grants from the one everything else writes to. Without this the grant sync
		// could not be composed at all on a provider whose grant store carries both, because nothing else
		// registers the contract it takes. A provider that ships a separate activity-group grant store
		// registers that one itself and is left alone here.
		if (typeof(IActivityGroupGrantStore).IsAssignableFrom(typeof(TStore)))
		{
			Services.Replace(ServiceDescriptor.Scoped<IActivityGroupGrantStore>(
				static provider => (IActivityGroupGrantStore)provider.GetRequiredService<IGrantStore>()));
		}

		return this;
	}

	/// <inheritdoc />
	public IA3Builder RequireDurableGrants()
	{
		// The gate is installed HERE, in the package that owns both the option and the validator, so a host
		// that takes only this package can reach it. It was previously reachable only from a sibling package
		// the standalone consumer does not take -- so the option's own documentation promised a fail-fast
		// check that could not fire for them.
		_ = Services.AddGrantDurabilityGate();
		return this;
	}

	/// <inheritdoc />
	public IA3Builder UseActivityGroupStore<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TStore>() where TStore : class, IActivityGroupStore
	{
		Services.Replace(ServiceDescriptor.Scoped<IActivityGroupStore, TStore>());
		return this;
	}
}
