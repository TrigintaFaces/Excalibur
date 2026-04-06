// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Configuration;
using Excalibur.Inbox.Postgres;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Npgsql;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring Postgres inbox store.
/// </summary>
public static class PostgresInboxExtensions
{
	/// <summary>
	/// Adds Postgres inbox store to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Action to configure the options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddPostgresInboxStore(
		this IServiceCollection services,
		Action<PostgresInboxOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.AddOptions<PostgresInboxOptions>()
			.Configure(configure)
			.ValidateOnStart();
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<PostgresInboxOptions>, PostgresInboxOptionsValidator>());
		services.TryAddSingleton<PostgresInboxStore>();
		services.AddKeyedSingleton<IInboxStore>("postgres", (sp, _) => sp.GetRequiredService<PostgresInboxStore>());
		services.TryAddKeyedSingleton<IInboxStore>("default", (sp, _) =>
			sp.GetRequiredKeyedService<IInboxStore>("postgres"));

		return services;
	}

	/// <summary>
	/// Adds Postgres inbox store to the service collection with a connection factory.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="connectionFactoryProvider">
	/// A factory function that creates <see cref="NpgsqlConnection"/> instances from the service provider.
	/// Useful for multi-database setups, custom connection pooling, or IDb integration.
	/// </param>
	/// <param name="configure">Action to configure the options (used for table names, timeouts, etc.).</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// <para>
	/// Example with IDb:
	/// <code>
	/// services.AddPostgresInboxStore(
	///     sp => () => (NpgsqlConnection)sp.GetRequiredService&lt;IInboxDb&gt;().Connection,
	///     options => options.SchemaName = "messaging");
	/// </code>
	/// </para>
	/// </remarks>
	public static IServiceCollection AddPostgresInboxStore(
		this IServiceCollection services,
		Func<IServiceProvider, Func<NpgsqlConnection>> connectionFactoryProvider,
		Action<PostgresInboxOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(connectionFactoryProvider);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.AddOptions<PostgresInboxOptions>()
			.Configure(configure)
			.ValidateOnStart();
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<PostgresInboxOptions>, PostgresInboxOptionsValidator>());
		services.TryAddSingleton(sp =>
		{
			var connectionFactory = connectionFactoryProvider(sp);
			var options = sp.GetRequiredService<IOptions<PostgresInboxOptions>>().Value;
			var logger = sp.GetRequiredService<ILogger<PostgresInboxStore>>();
			return new PostgresInboxStore(connectionFactory, options, logger);
		});
		services.AddKeyedSingleton<IInboxStore>("postgres", (sp, _) => sp.GetRequiredService<PostgresInboxStore>());
		services.TryAddKeyedSingleton<IInboxStore>("default", (sp, _) =>
			sp.GetRequiredKeyedService<IInboxStore>("postgres"));

		return services;
	}

	/// <summary>
	/// Configures the dispatch builder to use Postgres inbox store.
	/// </summary>
	/// <param name="builder">The dispatch builder.</param>
	/// <param name="configure">Action to configure the options.</param>
	/// <returns>The dispatch builder for fluent configuration.</returns>
	public static IDispatchBuilder UsePostgresInboxStore(
		this IDispatchBuilder builder,
		Action<PostgresInboxOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		_ = builder.Services.AddPostgresInboxStore(configure);

		return builder;
	}

	/// <summary>
	/// Configures the dispatch builder to use Postgres inbox store with a connection factory.
	/// </summary>
	/// <param name="builder">The dispatch builder.</param>
	/// <param name="connectionFactoryProvider">
	/// A factory function that creates <see cref="NpgsqlConnection"/> instances from the service provider.
	/// </param>
	/// <param name="configure">Action to configure the options (used for table names, timeouts, etc.).</param>
	/// <returns>The dispatch builder for fluent configuration.</returns>
	public static IDispatchBuilder UsePostgresInboxStore(
		this IDispatchBuilder builder,
		Func<IServiceProvider, Func<NpgsqlConnection>> connectionFactoryProvider,
		Action<PostgresInboxOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(connectionFactoryProvider);
		ArgumentNullException.ThrowIfNull(configure);

		_ = builder.Services.AddPostgresInboxStore(connectionFactoryProvider, configure);

		return builder;
	}
}
