// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch;
using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.ErrorHandling;
using Excalibur.Dispatch.Serialization;
using Excalibur.Outbox.SqlServer;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring SQL Server outbox store.
/// </summary>
public static class SqlServerOutboxExtensions
{
	/// <summary>
	/// Adds SQL Server outbox store to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Action to configure the options.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// <para>
	/// Set <see cref="SqlServerOutboxOptions.ConnectionString"/> in the configure action
	/// to specify the SQL Server connection string.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// services.AddSqlServerOutboxStore(options =>
	/// {
	///     options.ConnectionString = "Server=.;Database=MyDb;Trusted_Connection=True;";
	///     options.SchemaName = "messaging";
	/// });
	/// </code>
	/// </example>
	public static IServiceCollection AddSqlServerOutboxStore(
		this IServiceCollection services,
		Action<SqlServerOutboxOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.Configure(configure);
		BridgeProcessorIdFromOutboxBuilder(services);
		services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<SqlServerOutboxOptions>, SqlServerOutboxOptionsValidator>());
		services.TryAddSingleton<SqlServerOutboxStore>();
		services.AddKeyedSingleton<IOutboxStore>("sqlserver", (sp, _) => sp.GetRequiredService<SqlServerOutboxStore>());
		services.TryAddKeyedSingleton<IOutboxStore>("default", (sp, _) =>
			sp.GetRequiredKeyedService<IOutboxStore>("sqlserver"));
		services.TryAddSingleton<IMultiTransportOutboxStore>(sp => sp.GetRequiredService<SqlServerOutboxStore>());
		services.TryAddSingleton<IMultiTransportOutboxStoreAdmin>(sp => sp.GetRequiredService<SqlServerOutboxStore>());
		services.TryAddSingleton<ITransactionalOutboxWriter>(sp => sp.GetRequiredService<SqlServerOutboxStore>());

		return services;
	}

	/// <summary>
	/// Adds SQL Server outbox store to the service collection with a connection factory.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="connectionFactoryProvider">
	/// A factory function that creates <see cref="SqlConnection"/> instances from the service provider.
	/// Useful for multi-database setups, custom connection pooling, or IDb integration.
	/// </param>
	/// <param name="configure">Action to configure the options (used for table names, timeouts, etc.).</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// <para>
	/// Example with IDb:
	/// <code>
	/// services.AddSqlServerOutboxStore(
	///     sp => () => (SqlConnection)sp.GetRequiredService&lt;IOutboxDb&gt;().Connection,
	///     options => options.SchemaName = "messaging");
	/// </code>
	/// </para>
	/// </remarks>
	public static IServiceCollection AddSqlServerOutboxStore(
		this IServiceCollection services,
		Func<IServiceProvider, Func<SqlConnection>> connectionFactoryProvider,
		Action<SqlServerOutboxOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(connectionFactoryProvider);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.Configure(configure);
		BridgeProcessorIdFromOutboxBuilder(services);
		services.TryAddSingleton(sp =>
		{
			var connectionFactory = connectionFactoryProvider(sp);
			var options = sp.GetRequiredService<IOptions<SqlServerOutboxOptions>>().Value;
			var payloadSerializer = sp.GetService<IPayloadSerializer>();
			var logger = sp.GetRequiredService<ILogger<SqlServerOutboxStore>>();
			return new SqlServerOutboxStore(connectionFactory, options, payloadSerializer, logger);
		});
		services.AddKeyedSingleton<IOutboxStore>("sqlserver", (sp, _) => sp.GetRequiredService<SqlServerOutboxStore>());
		services.TryAddKeyedSingleton<IOutboxStore>("default", (sp, _) =>
			sp.GetRequiredKeyedService<IOutboxStore>("sqlserver"));
		services.TryAddSingleton<IMultiTransportOutboxStore>(sp => sp.GetRequiredService<SqlServerOutboxStore>());
		services.TryAddSingleton<IMultiTransportOutboxStoreAdmin>(sp => sp.GetRequiredService<SqlServerOutboxStore>());
		services.TryAddSingleton<ITransactionalOutboxWriter>(sp => sp.GetRequiredService<SqlServerOutboxStore>());

		return services;
	}

	/// <summary>
	/// vdcxk4: honors the outbox builder's <c>WithProcessorId(x)</c> as the SQL lease owner. The
	/// <c>Excalibur.Outbox</c> builder sets <c>OutboxOptions.ProcessorId</c>; this flows it to
	/// <see cref="SqlServerOutboxOptions.ProcessorId"/> (and thus the persisted <c>LeasedBy</c>).
	/// </summary>
	/// <remarks>
	/// Optional + non-destructive: a no-op when the outbox builder isn't used (no <c>OutboxOptions</c> in DI)
	/// or <c>WithProcessorId</c> was not set, so the auto-unique <c>{MachineName}:{ProcessId}</c> default
	/// stands. Stale-lease reclamation is age-based (<c>LeasedAt &lt; timeout</c>) and concurrency safety is
	/// <c>SKIP LOCKED</c> row-claiming, so a shared <c>LeasedBy</c> is row-safe; cross-host uniqueness, if the
	/// operator sets an explicit id, is their responsibility (same as the default they replaced).
	/// </remarks>
	private static void BridgeProcessorIdFromOutboxBuilder(IServiceCollection services) =>
		_ = services.AddOptions<SqlServerOutboxOptions>().PostConfigure<IServiceProvider>((options, serviceProvider) =>
		{
			var builderProcessorId = serviceProvider.GetService<Excalibur.Outbox.OutboxOptions>()?.ProcessorId;
			if (!string.IsNullOrWhiteSpace(builderProcessorId))
			{
				options.ProcessorId = builderProcessorId;
			}
		});

	/// <summary>
	/// Adds SQL Server outbox store using a typed <see cref="Excalibur.Data.IDb"/> marker for connection resolution.
	/// </summary>
	/// <typeparam name="TDb">The typed database marker that implements <see cref="Excalibur.Data.IDb"/>.</typeparam>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Action to configure the options (used for table names, timeouts, etc.).</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// <para>
	/// Resolves <typeparamref name="TDb"/> from DI and extracts its connection as a <see cref="SqlConnection"/>.
	/// Eliminates the bridging ceremony:
	/// <c>sp =&gt; () =&gt; (SqlConnection)sp.GetRequiredService&lt;TDb&gt;().Connection</c>
	/// </para>
	/// </remarks>
	public static IServiceCollection AddSqlServerOutboxStore<TDb>(
		this IServiceCollection services,
		Action<SqlServerOutboxOptions> configure)
		where TDb : class, Excalibur.Data.IDb
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		return services.AddSqlServerOutboxStore(
			sp => () => (SqlConnection)sp.GetRequiredService<TDb>().Connection,
			configure);
	}

	/// <summary>
	/// Configures the dispatch builder to use SQL Server outbox store.
	/// </summary>
	/// <param name="builder">The dispatch builder.</param>
	/// <param name="configure">Action to configure the options.</param>
	/// <returns>The dispatch builder for fluent configuration.</returns>
	/// <remarks>
	/// <para>
	/// Set <see cref="SqlServerOutboxOptions.ConnectionString"/> in the configure action
	/// to specify the SQL Server connection string.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// builder.UseSqlServerOutboxStore(options =>
	/// {
	///     options.ConnectionString = "Server=.;Database=MyDb;Trusted_Connection=True;";
	/// });
	/// </code>
	/// </example>
	public static IDispatchBuilder UseSqlServerOutboxStore(
		this IDispatchBuilder builder,
		Action<SqlServerOutboxOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		_ = builder.Services.AddSqlServerOutboxStore(configure);

		return builder;
	}

	/// <summary>
	/// Configures the dispatch builder to use SQL Server outbox store with a connection factory.
	/// </summary>
	/// <param name="builder">The dispatch builder.</param>
	/// <param name="connectionFactoryProvider">
	/// A factory function that creates <see cref="SqlConnection"/> instances from the service provider.
	/// </param>
	/// <param name="configure">Action to configure the options (used for table names, timeouts, etc.).</param>
	/// <returns>The dispatch builder for fluent configuration.</returns>
	public static IDispatchBuilder UseSqlServerOutboxStore(
		this IDispatchBuilder builder,
		Func<IServiceProvider, Func<SqlConnection>> connectionFactoryProvider,
		Action<SqlServerOutboxOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(connectionFactoryProvider);
		ArgumentNullException.ThrowIfNull(configure);

		_ = builder.Services.AddSqlServerOutboxStore(connectionFactoryProvider, configure);

		return builder;
	}

	/// <summary>
	/// Adds SQL Server dead letter queue to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Action to configure the options.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// <para>
	/// Set <see cref="SqlServerDeadLetterQueueOptions.ConnectionString"/> in the configure action
	/// to specify the SQL Server connection string.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// services.AddSqlServerDeadLetterQueue(options =>
	/// {
	///     options.ConnectionString = "Server=.;Database=MyDb;Trusted_Connection=True;";
	/// });
	/// </code>
	/// </example>
	public static IServiceCollection AddSqlServerDeadLetterQueue(
		this IServiceCollection services,
		Action<SqlServerDeadLetterQueueOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.Configure(configure);
		services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<SqlServerDeadLetterQueueOptions>, SqlServerDeadLetterQueueOptionsValidator>());
		services.TryAddSingleton<SqlServerDeadLetterQueue>();
		services.TryAddSingleton<IDeadLetterQueue>(sp => sp.GetRequiredService<SqlServerDeadLetterQueue>());
		services.TryAddSingleton<IDeadLetterQueueAdmin>(sp => sp.GetRequiredService<SqlServerDeadLetterQueue>());

		return services;
	}

	/// <summary>
	/// Configures the dispatch builder to use SQL Server dead letter queue.
	/// </summary>
	/// <param name="builder">The dispatch builder.</param>
	/// <param name="configure">Action to configure the options.</param>
	/// <returns>The dispatch builder for fluent configuration.</returns>
	/// <remarks>
	/// <para>
	/// Set <see cref="SqlServerDeadLetterQueueOptions.ConnectionString"/> in the configure action
	/// to specify the SQL Server connection string.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// builder.UseSqlServerDeadLetterQueue(options =>
	/// {
	///     options.ConnectionString = "Server=.;Database=MyDb;Trusted_Connection=True;";
	/// });
	/// </code>
	/// </example>
	public static IDispatchBuilder UseSqlServerDeadLetterQueue(
		this IDispatchBuilder builder,
		Action<SqlServerDeadLetterQueueOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		_ = builder.Services.AddSqlServerDeadLetterQueue(configure);

		return builder;
	}
}
