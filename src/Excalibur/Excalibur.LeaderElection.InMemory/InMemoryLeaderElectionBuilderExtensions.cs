// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.LeaderElection;
using Excalibur.Dispatch.LeaderElection.DependencyInjection;
using Excalibur.LeaderElection.InMemory;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring in-memory leader election on <see cref="ILeaderElectionBuilder"/>.
/// </summary>
public static class InMemoryLeaderElectionBuilderExtensions
{
	/// <summary>
	/// Configures the leader election builder to use the in-memory provider.
	/// </summary>
	/// <param name="builder">The leader election builder.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <remarks>
	/// This is suitable for single-process scenarios, testing, and development.
	/// For distributed scenarios, use <c>UseRedis()</c>, <c>UseSqlServer()</c>,
	/// <c>UseConsul()</c>, or <c>UseKubernetes()</c>.
	/// </remarks>
	public static ILeaderElectionBuilder UseInMemory(this ILeaderElectionBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		builder.Services.AddKeyedSingleton<ILeaderElectionFactory>("inmemory",
			(sp, _) => sp.GetRequiredService<InMemoryLeaderElectionFactory>());
		builder.Services.TryAddKeyedSingleton<ILeaderElectionFactory>("default", (sp, _) =>
			sp.GetRequiredKeyedService<ILeaderElectionFactory>("inmemory"));
		builder.Services.TryAddSingleton<InMemoryLeaderElectionFactory>();

		return builder;
	}
}
