// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.EventSourcing;
using Excalibur.EventSourcing.SqlServer;
using Excalibur.EventSourcing.SqlServer.DependencyInjection;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

#pragma warning disable IL2091 // DI registration methods create stores with DynamicallyAccessedMembers-annotated TProjection

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering SQL Server projection store services.
/// </summary>
/// <remarks>
/// Explicit per-projection configuration uses named options keyed by the projection type's
/// assembly-qualified name. Use <c>AddSqlServerProjections</c> for shared defaults with per-model
/// overrides, or <c>ConfigureAll</c> for intentional global options. Factory and typed-database
/// registrations without a configuration callback continue to use unnamed options.
/// </remarks>
public static class SqlServerProjectionStoreExtensions
{
	/// <summary>
	/// Adds the SQL Server projection store to the service collection.
	/// </summary>
	/// <typeparam name="TProjection">The projection type to store.</typeparam>
	/// <param name="services">The service collection.</param>
	/// <param name="configureOptions">Action to configure projection store options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddSqlServerProjectionStore<TProjection>(
		this IServiceCollection services,
		Action<SqlServerProjectionStoreOptions> configureOptions)
		where TProjection : class
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configureOptions);

		var optionsName = typeof(TProjection).AssemblyQualifiedName!;
		_ = services.Configure(optionsName, configureOptions);

		// Register validator for ValidateOnStart (P1-3 fix)
		_ = services.AddDefaultTenantContext();
		services.AddSingleton<IValidateOptions<SqlServerProjectionStoreOptions>>(new NamedProjectionOptionsValidator(optionsName));
		_ = services.AddOptions<SqlServerProjectionStoreOptions>(optionsName).ValidateOnStart();

		// Dep-gated seam: threads the ambient ITenantContext into the store (fixing the DOA where a null
		// context threw on every operation) AND emits the projection-family capability marker inseparably
		// from the tenant wiring it attests.
		services.AddTenantScopedProjectionStore<IProjectionStore<TProjection>, SqlServerProjectionStore<TProjection>, IProjectionStore<object>>(sp =>
		{
			var options = sp.GetRequiredService<IOptionsMonitor<SqlServerProjectionStoreOptions>>().Get(optionsName);
			var logger = sp.GetRequiredService<ILogger<SqlServerProjectionStore<TProjection>>>();

			options.Validate();

			return new SqlServerProjectionStore<TProjection>(
				options.ConnectionString ?? throw new InvalidOperationException("SqlServerProjectionStoreOptions.ConnectionString is required."),
				logger,
				tenantContext: sp.GetRequiredService<ITenantContext>(),
				tableName: options.TableName,
				jsonOptions: options.JsonSerializerOptions,
				schemaName: options.SchemaName);
		});

		return services;
	}

	/// <summary>
	/// Adds the SQL Server projection store to the service collection with a connection factory.
	/// </summary>
	/// <typeparam name="TProjection">The projection type to store.</typeparam>
	/// <param name="services">The service collection.</param>
	/// <param name="connectionFactory">A factory function that creates <see cref="SqlConnection"/> instances.</param>
	/// <param name="configureOptions">Optional action to further configure projection store options.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// Use this overload when you want to provide custom connection management,
	/// such as integrating with an existing connection pool or IDb abstraction.
	/// </remarks>
	public static IServiceCollection AddSqlServerProjectionStore<TProjection>(
		this IServiceCollection services,
		Func<SqlConnection> connectionFactory,
		Action<SqlServerProjectionStoreOptions>? configureOptions = null)
		where TProjection : class
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(connectionFactory);

        var optionsName = configureOptions is null ? Microsoft.Extensions.Options.Options.DefaultName : typeof(TProjection).AssemblyQualifiedName!;
        if (configureOptions is not null)
        {
            _ = services.Configure(optionsName, configureOptions);
        }

		// Dep-gated seam: threads the ambient ITenantContext into the store (fixing the DOA) AND emits the
		// projection-family capability marker inseparably.
		_ = services.AddDefaultTenantContext();
		services.AddTenantScopedProjectionStore<IProjectionStore<TProjection>, SqlServerProjectionStore<TProjection>, IProjectionStore<object>>(sp =>
		{
			var logger = sp.GetRequiredService<ILogger<SqlServerProjectionStore<TProjection>>>();
			var options = configureOptions is null
                ? sp.GetService<IOptions<SqlServerProjectionStoreOptions>>()?.Value
                : sp.GetRequiredService<IOptionsMonitor<SqlServerProjectionStoreOptions>>().Get(optionsName);

			return new SqlServerProjectionStore<TProjection>(
				connectionFactory,
				logger,
				tenantContext: sp.GetRequiredService<ITenantContext>(),
				tableName: options?.TableName,
				jsonOptions: options?.JsonSerializerOptions,
				schemaName: options?.SchemaName ?? "dbo");
		});

		return services;
	}

	/// <summary>
	/// Adds the SQL Server projection store using a typed <see cref="Excalibur.Data.IDb"/> marker for connection resolution.
	/// </summary>
	/// <typeparam name="TProjection">The projection type to store.</typeparam>
	/// <typeparam name="TDb">The typed database marker that implements <see cref="Excalibur.Data.IDb"/>.</typeparam>
	/// <param name="services">The service collection.</param>
	/// <param name="configureOptions">Optional action to further configure projection store options.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// <para>
	/// Resolves <typeparamref name="TDb"/> from DI and extracts its connection as a <see cref="SqlConnection"/>.
	/// Eliminates the bridging ceremony:
	/// <c>sp =&gt; () =&gt; (SqlConnection)sp.GetRequiredService&lt;TDb&gt;().Connection</c>
	/// </para>
	/// </remarks>
	public static IServiceCollection AddSqlServerProjectionStore<TProjection, TDb>(
		this IServiceCollection services,
		Action<SqlServerProjectionStoreOptions>? configureOptions = null)
		where TProjection : class
		where TDb : class, Excalibur.Data.IDb
	{
		ArgumentNullException.ThrowIfNull(services);

        var optionsName = configureOptions is null ? Microsoft.Extensions.Options.Options.DefaultName : typeof(TProjection).AssemblyQualifiedName!;
        if (configureOptions is not null)
        {
            _ = services.Configure(optionsName, configureOptions);
        }

		// Dep-gated seam: threads the ambient ITenantContext into the store (fixing the DOA) AND emits the
		// projection-family capability marker inseparably.
		_ = services.AddDefaultTenantContext();
		services.AddTenantScopedProjectionStore<IProjectionStore<TProjection>, SqlServerProjectionStore<TProjection>, IProjectionStore<object>>(sp =>
		{
			var logger = sp.GetRequiredService<ILogger<SqlServerProjectionStore<TProjection>>>();
			var options = configureOptions is null
                ? sp.GetService<IOptions<SqlServerProjectionStoreOptions>>()?.Value
                : sp.GetRequiredService<IOptionsMonitor<SqlServerProjectionStoreOptions>>().Get(optionsName);

			return new SqlServerProjectionStore<TProjection>(
				() => (SqlConnection)sp.GetRequiredService<TDb>().Connection,
				logger,
				tenantContext: sp.GetRequiredService<ITenantContext>(),
				tableName: options?.TableName,
				jsonOptions: options?.JsonSerializerOptions,
				schemaName: options?.SchemaName ?? "dbo");
		});

		return services;
	}

	/// <summary>
	/// Registers multiple SQL Server projection stores that share a common connection string,
	/// reducing boilerplate when multiple projections target the same database.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="connectionString">The shared SQL Server connection string.</param>
	/// <param name="configure">Action to register individual projection stores.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <example>
	/// <code>
	/// services.AddSqlServerProjections("Server=...;Database=...", projections =>
	/// {
	///     projections.Add&lt;OrderSummary&gt;();
	///     projections.Add&lt;CustomerProfile&gt;(options => options.TableName = "Customers");
	///     projections.Add&lt;ProductCatalog&gt;();
	/// });
	/// </code>
	/// </example>
	public static IServiceCollection AddSqlServerProjections(
		this IServiceCollection services,
		string connectionString,
		Action<SqlServerProjectionRegistrar> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
		ArgumentNullException.ThrowIfNull(configure);

		var registrar = new SqlServerProjectionRegistrar(services, connectionString);
		configure(registrar);

		return services;
	}

	/// <summary>
	/// Registers multiple SQL Server projection stores with shared options configuration,
	/// allowing advanced settings like custom table names, JSON serialization, and timeouts.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configureShared">Shared options applied to all projections before per-projection overrides.</param>
	/// <param name="configure">Action to register individual projection stores.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <example>
	/// <code>
	/// services.AddSqlServerProjections(
	///     shared => { shared.ConnectionString = connStr; shared.SchemaName = "projections"; },
	///     projections =>
	///     {
	///         projections.Add&lt;OrderSummary&gt;();
	///         projections.Add&lt;CustomerProfile&gt;(options => options.TableName = "Customers");
	///     });
	/// </code>
	/// </example>
	public static IServiceCollection AddSqlServerProjections(
		this IServiceCollection services,
		Action<SqlServerProjectionStoreOptions> configureShared,
		Action<SqlServerProjectionRegistrar> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configureShared);
		ArgumentNullException.ThrowIfNull(configure);

		var registrar = new SqlServerProjectionRegistrar(services, configureShared);
		configure(registrar);

		return services;
	}
    // Connection-string requirements belong only to registrations that obtain their connection
    // from options. Factory/IDb registrations supply it separately and must not inherit this check.
    private sealed class NamedProjectionOptionsValidator(string optionsName) : IValidateOptions<SqlServerProjectionStoreOptions>
    {
        private readonly SqlServerProjectionStoreOptionsValidator _validator = new();
        public ValidateOptionsResult Validate(string? name, SqlServerProjectionStoreOptions options) =>
            string.Equals(name, optionsName, StringComparison.Ordinal)
                ? _validator.Validate(name, options)
                : ValidateOptionsResult.Skip;
    }

}
