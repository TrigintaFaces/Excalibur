// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Abstractions.Sharding;
using Excalibur.Data.OpenSearch.Projections;
using Excalibur.Data.OpenSearch.Sharding;
using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.DependencyInjection;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering OpenSearch tenant shard providers.
/// </summary>
public static class OpenSearchTenantShardingExtensions
{
	/// <summary>
	/// Registers the OpenSearch <see cref="ITenantStoreResolver{TStore}"/> for
	/// <see cref="IProjectionStore{TProjection}"/>, enabling tenant-aware projection
	/// store routing using index-per-tenant isolation.
	/// </summary>
	/// <typeparam name="TProjection">The projection type.</typeparam>
	public static IEventSourcingBuilder UseOpenSearchTenantProjectionStore<TProjection>(
		this IEventSourcingBuilder builder)
		where TProjection : class
	{
		ArgumentNullException.ThrowIfNull(builder);

		builder.Services.TryAddSingleton<ITenantStoreResolver<IProjectionStore<TProjection>>>(sp =>
			new OpenSearchTenantProjectionStoreResolver<TProjection>(
				sp.GetRequiredService<ITenantShardMap>(),
				sp.GetRequiredService<ILoggerFactory>(),
				sp.GetRequiredService<IOptionsMonitor<OpenSearchProjectionStoreOptions>>()));

		return builder;
	}
}
