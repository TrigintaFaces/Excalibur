// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Inbox.DependencyInjection;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring Excalibur Inbox services in an <see cref="IServiceCollection"/>.
/// </summary>
public static class InboxServiceCollectionExtensions
{
	/// <summary>
	/// Adds Excalibur Inbox services and returns a builder for configuring the inbox provider.
	/// </summary>
	/// <param name="services">The service collection to add services to.</param>
	/// <param name="configure">Action to configure inbox services via the builder.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// <para>
	/// This is the recommended entry point for configuring inbox services.
	/// Use the builder to select a storage provider:
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// // SQL Server
	/// services.AddExcaliburInbox(inbox => inbox.UseSqlServer(sql => sql.ConnectionString = connectionString));
	///
	/// // Postgres
	/// services.AddExcaliburInbox(inbox => inbox.UsePostgres(pg => pg.ConnectionString = connectionString));
	///
	/// // In-Memory (for testing)
	/// services.AddExcaliburInbox(inbox => inbox.UseInMemory());
	///
	/// // Redis
	/// services.AddExcaliburInbox(inbox => inbox.UseRedis(options => options.ConnectionString = "localhost:6379"));
	/// </code>
	/// </example>
	public static IServiceCollection AddExcaliburInbox(
		this IServiceCollection services,
		Action<IInboxBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		var builder = new InboxBuilder(services);
		configure(builder);

		return services;
	}
}
