// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.EventSourcing;
using Excalibur.EventSourcing.Projections;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers the in-memory projection store.
/// </summary>
public static class InMemoryProjectionStoreServiceCollectionExtensions
{
	/// <summary>
	/// Registers an in-memory <see cref="IProjectionStore{TProjection}"/> for development, tests and
	/// samples.
	/// </summary>
	/// <typeparam name="TProjection">The projection type.</typeparam>
	/// <param name="services">The service collection.</param>
	/// <returns>The service collection, for chaining.</returns>
	/// <remarks>
	/// <para>
	/// Registered as a SINGLETON, which is the whole point of it: a scoped in-memory store would discard
	/// everything it held at the end of each request, and a projection that loses its contents between
	/// requests looks exactly like one that never advanced.
	/// </para>
	/// <para>
	/// <b>Not for production, and not tenant-confined.</b> See
	/// <see cref="InMemoryProjectionStore{TProjection}"/> for what that means — in short, two tenants
	/// writing the same id address the same entry, and nothing survives the process.
	/// </para>
	/// <code>
	/// services.AddExcalibur(x => x.AddEventSourcing(es =>
	/// {
	///     es.UseInMemory().AddRepository&lt;OrderAggregate, Guid&gt;();
	///     es.AddProjection&lt;OrderSummary&gt;(p => p.Inline().When&lt;OrderPlaced&gt;(/* … */));
	/// }));
	///
	/// services.AddInMemoryProjectionStore&lt;OrderSummary&gt;();
	/// </code>
	/// </remarks>
	public static IServiceCollection AddInMemoryProjectionStore<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TProjection>(
		this IServiceCollection services)
		where TProjection : class
	{
		ArgumentNullException.ThrowIfNull(services);

		services.TryAddSingleton<InMemoryProjectionStore<TProjection>>();
		services.TryAddSingleton<IProjectionStore<TProjection>>(
			static sp => sp.GetRequiredService<InMemoryProjectionStore<TProjection>>());

		return services;
	}
}
