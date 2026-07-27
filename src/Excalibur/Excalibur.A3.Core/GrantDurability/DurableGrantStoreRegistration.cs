// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Excalibur.A3.Authorization;

/// <summary>
/// Installs the boot-time gate that verifies the configured grant store keeps authorization grants across
/// process restarts.
/// </summary>
/// <remarks>
/// Durability is discovered from the store itself: a durable store answers for
/// <see cref="IDurableGrantStore" /> through <see cref="IServiceProvider.GetService(Type)" />, so no separate
/// attestation is registered and a store cannot be advertised as durable without being it.
/// </remarks>
public static class DurableGrantStoreRegistration
{
	/// <summary>
	/// Adds the boot-time gate that fails startup when authorization is left on a volatile grant store
	/// without the host having accepted that explicitly.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <returns> The same <see cref="IServiceCollection" /> for chaining. </returns>
	/// <exception cref="ArgumentNullException"> <paramref name="services" /> is <see langword="null" />. </exception>
	/// <remarks>
	/// <para>
	/// Grant durability is an <b>explicit opt-in</b>: there is no framework composition the runtime can
	/// trigger on, because requiring durable grants is a consumer intent, not something the presence of a
	/// store implies. Call this method when grants must survive a restart. Absent this call, a volatile
	/// (in-memory) grant store is accepted silently — the correct behaviour for a dev/test host.
	/// </para>
	/// <para>
	/// Once installed, silence within a gated host means the durability requirement applies (a volatile store
	/// fails startup); accepting a volatile grant store then requires setting
	/// <see cref="GrantDurabilityOptions.AllowVolatileGrantStore" /> deliberately.
	/// </para>
	/// </remarks>
	public static IServiceCollection AddGrantDurabilityGate(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<GrantDurabilityOptions>, GrantDurabilityValidator>());

		_ = services.AddOptions<GrantDurabilityOptions>().ValidateOnStart();

		return services;
	}
}
