// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Configuration;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Convenience extension that bundles Excalibur.Dispatch with PostgreSQL event sourcing and outbox
/// into a single registration call.
/// </summary>
public static class DispatchPostgresServiceCollectionExtensions
{
	/// <summary>
	/// Registers Excalibur.Dispatch with PostgreSQL event sourcing and outbox using the specified connection string.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="connectionString">The PostgreSQL connection string.</param>
	/// <param name="configureDispatch">Optional dispatch builder configuration.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddDispatchWithPostgres(
		this IServiceCollection services,
		string connectionString,
		Action<IDispatchBuilder>? configureDispatch = null)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

		_ = services.AddDispatch(configureDispatch);
		_ = services.AddPostgresEventSourcing(options => options.ConnectionString = connectionString);

		return services;
	}
}
