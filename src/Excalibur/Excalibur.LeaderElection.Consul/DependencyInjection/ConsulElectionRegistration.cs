// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.LeaderElection;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Excalibur.LeaderElection.Consul;

/// <summary>
/// Registers the singleton Consul election and the outbox leader gate that fences on it, for a consumer
/// who has named the resource to contend for.
/// </summary>
/// <remarks>
/// <para>
/// The Consul provider registers an <see cref="ILeaderElectionFactory"/>, which is the right shape for a
/// provider whose elections are per-resource. But the outbox leader gate is built from
/// <c>GetRequiredService&lt;ILeaderElection&gt;()</c>, so on a factory-only registration the gate could not
/// be constructed at all and the fencing guarantee had no delivery mechanism.
/// </para>
/// <para>
/// The election is registered only when the consumer has named the resource. A Consul KV key is shared
/// across every application pointed at the same cluster, so a framework-chosen default name would put two
/// unrelated applications into contention for one lock — each stalling the other's outbox. Naming is the
/// consumer's call, exactly as the SQL Server provider requires an explicit lock resource. When no name is
/// given the registration stays factory-only, which is what it has always been, so nothing that works today
/// changes behaviour.
/// </para>
/// </remarks>
internal static class ConsulElectionRegistration
{
	internal static void RegisterSingletonElectionAndGate(IServiceCollection services, string? resourceName)
	{
		if (string.IsNullOrWhiteSpace(resourceName))
		{
			// The consumer never named a resource, so there is no singleton election to register and no
			// safe name to invent. Elections remain available through the factory and through the
			// AddConsulLeaderElectionForResource overloads, which take the name as an argument.
			return;
		}

		services.TryAddKeyedSingleton<ILeaderElection>("consul", (sp, _) =>
			sp.GetRequiredService<ILeaderElectionFactory>().CreateElection(resourceName, null));
		services.TryAddKeyedSingleton<ILeaderElection>("default", (sp, _) =>
			sp.GetRequiredKeyedService<ILeaderElection>("consul"));

		// Also register unkeyed. The outbox leader gate resolves ILeaderElection directly, and a keyed
		// registration does not satisfy an unkeyed request. TryAdd, so a consumer's own registration wins.
		services.TryAddSingleton<ILeaderElection>(sp =>
			sp.GetRequiredKeyedService<ILeaderElection>("default"));

		// The gate must be registered on the same path that makes the election resolvable: the outbox startup
		// invariant treats a resolvable election as the multi-instance signal, so an election registered
		// without a gate would turn a host that starts today into one that refuses to start.
		OutboxBuilderLeaderElectionExtensions.RegisterOutboxLeaderGate(services);
	}
}
