// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.LeaderElection;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Excalibur.LeaderElection.InMemory;

/// <summary>
/// Registers the singleton in-memory election and the outbox leader gate that fences on it.
/// </summary>
/// <remarks>
/// The in-memory provider previously registered only an <see cref="ILeaderElectionFactory"/>. The outbox
/// leader gate is built from <c>GetRequiredService&lt;ILeaderElection&gt;()</c>, so a factory-only
/// registration left the gate unsatisfiable — and the startup prerequisite validator, which requires a
/// keyed <c>"default"</c> election, refused host startup while naming this very provider in its remedy text.
/// </remarks>
internal static class InMemoryElectionRegistration
{
	/// <summary>
	/// The resource this provider's singleton election contends for. The in-memory election coordinates
	/// through per-process shared state keyed by this name, so a fixed name is correct here and carries none
	/// of the cross-application collision risk a fixed name would carry on a distributed provider.
	/// </summary>
	internal const string SingletonResourceName = "excalibur-default";

	internal static void RegisterSingletonElectionAndGate(IServiceCollection services)
	{
		services.TryAddKeyedSingleton<ILeaderElection>("inmemory", (sp, _) =>
			sp.GetRequiredService<InMemoryLeaderElectionFactory>().CreateElection(SingletonResourceName, null));
		services.TryAddKeyedSingleton<ILeaderElection>("default", (sp, _) =>
			sp.GetRequiredKeyedService<ILeaderElection>("inmemory"));

		// Also register unkeyed. The outbox leader gate — and any consumer of a single election — resolves
		// ILeaderElection directly, and a keyed registration does not satisfy an unkeyed request. TryAdd, so
		// a consumer's own unkeyed registration still wins.
		services.TryAddSingleton<ILeaderElection>(sp =>
			sp.GetRequiredKeyedService<ILeaderElection>("default"));

		// The gate must be registered on the same path that makes the election resolvable. The outbox startup
		// invariant treats a resolvable election as the multi-instance signal, so registering the election
		// without the gate would turn a host that starts today into one that refuses to start.
		OutboxBuilderLeaderElectionExtensions.RegisterOutboxLeaderGate(services);
	}
}
