// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch;
using Excalibur.Dispatch.Serialization;
using Excalibur.EventSourcing.DependencyInjection;
using Excalibur.EventSourcing.Queries;
using Excalibur.EventSourcing.SqlServer.DependencyInjection;
using Excalibur.EventSourcing.TieredStorage;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.SqlServer;

/// <summary>
/// Extension methods for configuring SQL Server event sourcing on <see cref="IEventSourcingBuilder"/>.
/// </summary>
/// <remarks>
/// <para>
/// These extensions provide fluent provider selection following the canonical
/// CDC builder pattern (see <c>CdcBuilderSqlServerExtensions</c>).
/// </para>
/// </remarks>
public static class EventSourcingBuilderSqlServerExtensions
{
	/// <summary>
	/// Configures the event sourcing builder to use SQL Server for event store
	/// and snapshot store.
	/// </summary>
	/// <param name="builder">The event sourcing builder.</param>
	/// <param name="configure">Configuration action for the SQL Server event sourcing builder.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="builder"/> or <paramref name="configure"/> is null.
	/// </exception>
	/// <example>
	/// <code>
	/// // Connection string
	/// services.AddExcalibur(x => x.AddEventSourcing(es =&gt;
	/// {
	///     es.UseSqlServer(sql =&gt;
	///     {
	///         sql.ConnectionString(configuration.GetConnectionString("EventStore")!)
	///            .EventStoreSchema("es")
	///            .SnapshotStoreSchema("es");
	///     })
	///     .AddRepository&lt;OrderAggregate, Guid&gt;();
	/// }));
	///
	/// // Named connection string
	/// services.AddExcalibur(x => x.AddEventSourcing(es =&gt;
	/// {
	///     es.UseSqlServer(sql =&gt;
	///     {
	///         sql.ConnectionStringName("EventStore");
	///     });
	/// }));
	///
	/// // Connection factory (Azure Managed Identity)
	/// services.AddExcalibur(x => x.AddEventSourcing(es =&gt;
	/// {
	///     es.UseSqlServer(sql =&gt;
	///     {
	///         sql.ConnectionFactory(sp =&gt;
	///         {
	///             var config = sp.GetRequiredService&lt;IConfiguration&gt;();
	///             var connStr = config.GetConnectionString("EventStore")!;
	///             return () =&gt; new SqlConnection(connStr);
	///         });
	///     });
	/// }));
	///
	/// // Bind from appsettings.json
	/// services.AddExcalibur(x => x.AddEventSourcing(es =&gt;
	/// {
	///     es.UseSqlServer(sql =&gt;
	///     {
	///         sql.BindConfiguration("EventSourcing:SqlServer");
	///     });
	/// }));
	/// </code>
	/// </example>
	public static IEventSourcingBuilder UseSqlServer(
		this IEventSourcingBuilder builder,
		Action<ISqlServerEventSourcingBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		// Create and configure SQL Server options via builder
		var options = new SqlServerEventSourcingOptions();
		var sqlBuilder = new SqlServerEventSourcingBuilder(options);
		configure(sqlBuilder);

		// Determine connection factory based on builder state
		var connectionFactory = ResolveConnectionFactory(sqlBuilder);

		// Determine whether the builder configured a non-connection-string connection
		var hasBuilderConnection = sqlBuilder.ConnectionFactoryFunc is not null
			|| sqlBuilder.ConnectionStringNameValue is not null;

		RegisterOptionsAndServices(builder, sqlBuilder, options, connectionFactory, hasBuilderConnection);

		return builder;
	}

	/// <summary>
	/// Resolves the connection factory from the builder configuration.
	/// </summary>
	/// <remarks>
	/// Priority order (last-wins means only one is set, but resolution handles fallback):
	/// <list type="number">
	/// <item>Explicit <see cref="SqlServerEventSourcingBuilder.ConnectionFactoryFunc"/> (set via <c>ConnectionFactory()</c>)</item>
	/// <item><see cref="SqlServerEventSourcingBuilder.ConnectionStringNameValue"/> (resolved from IConfiguration at DI resolution)</item>
	/// <item><see cref="SqlServerEventSourcingBuilder.BindConfigurationPath"/> (resolved via options binding)</item>
	/// <item><see cref="SqlServerEventSourcingOptions.ConnectionString"/> (set via <c>ConnectionString()</c>)</item>
	/// <item>None set -- ValidateOnStart will catch this at startup</item>
	/// </list>
	/// </remarks>
	private static Func<IServiceProvider, Func<SqlConnection>> ResolveConnectionFactory(
		SqlServerEventSourcingBuilder sqlBuilder)
	{
		// 1. Explicit factory takes highest precedence
		if (sqlBuilder.ConnectionFactoryFunc is not null)
		{
			return sqlBuilder.ConnectionFactoryFunc;
		}

		// 2. Named connection string resolved from IConfiguration
		if (sqlBuilder.ConnectionStringNameValue is not null)
		{
			var connStrName = sqlBuilder.ConnectionStringNameValue;
			return sp =>
			{
				var config = sp.GetRequiredService<IConfiguration>();
				var resolved = config.GetConnectionString(connStrName)
					?? throw new InvalidOperationException(
						$"Connection string '{connStrName}' not found in IConfiguration. " +
						$"Ensure it exists in the ConnectionStrings section of your configuration.");
				return () => new SqlConnection(resolved);
			};
		}

		// 3 & 4. Connection string from options (direct or via BindConfiguration)
		return sp =>
		{
			var opts = sp.GetRequiredService<IOptions<SqlServerEventSourcingOptions>>();
			var connectionString = opts.Value.ConnectionString;
			return () => new SqlConnection(connectionString);
		};
	}

	/// <summary>
	/// Registers options, services, and validation.
	/// </summary>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options validation/binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	private static void RegisterOptionsAndServices(
		IEventSourcingBuilder builder,
		SqlServerEventSourcingBuilder sqlBuilder,
		SqlServerEventSourcingOptions options,
		Func<IServiceProvider, Func<SqlConnection>> connectionFactory,
		bool hasBuilderConnection)
	{
		// Register options from builder state
		_ = builder.Services.Configure<SqlServerEventSourcingOptions>(opt =>
		{
			opt.ConnectionString = options.ConnectionString;
			opt.EventStoreSchema = options.EventStoreSchema;
			opt.EventStoreTable = options.EventStoreTable;
			opt.SnapshotStoreSchema = options.SnapshotStoreSchema;
			opt.SnapshotStoreTable = options.SnapshotStoreTable;
			opt.HealthChecks = options.HealthChecks;
			// Only when the builder was actually given one. This delegate runs alongside any
			// Configure<SqlServerEventSourcingOptions> the consumer registered directly, so an
			// unconditional assignment would overwrite their resolver with the builder's null.
			if (options.EventTypeInfoResolver is not null)
			{
				opt.EventTypeInfoResolver = options.EventTypeInfoResolver;
			}
		});

		// Register BindConfiguration if set
		if (sqlBuilder.BindConfigurationPath is not null)
		{
			builder.Services.AddOptions<SqlServerEventSourcingOptions>()
				.BindConfiguration(sqlBuilder.BindConfigurationPath)
				.ValidateOnStart();

			// When ConnectionString() was explicitly called alongside BindConfiguration,
			// re-apply via PostConfigure so the explicit value takes precedence over config.
			if (!string.IsNullOrWhiteSpace(options.ConnectionString))
			{
				var explicitConnectionString = options.ConnectionString;
				_ = builder.Services.PostConfigure<SqlServerEventSourcingOptions>(opt =>
				{
					opt.ConnectionString = explicitConnectionString;
				});
			}
		}

		// Register ValidateOnStart with connection awareness
		builder.Services.AddSingleton<IValidateOptions<SqlServerEventSourcingOptions>>(
			new SqlServerEventSourcingOptionsValidator { HasBuilderConnection = hasBuilderConnection });
		builder.Services.AddOptions<SqlServerEventSourcingOptions>().ValidateOnStart();

		// Resolve the selected source and final locations once for this service provider.
		// Re-running an outer factory can route the global reader to a different database.
		var ownsPrimaryConnections = sqlBuilder.ConnectionFactoryFunc is null || sqlBuilder.HasOwnedPrimaryConnectionFactory;
		builder.Services.TryAddSingleton(sp =>
		{
			var resolved = sp.GetRequiredService<IOptions<SqlServerEventSourcingOptions>>().Value;
			return new ProviderBinding(connectionFactory(sp), ownsPrimaryConnections, resolved.EventStoreSchema, resolved.EventStoreTable,
				resolved.SnapshotStoreSchema, resolved.SnapshotStoreTable, resolved.EventTypeInfoResolver);
		});

		// Register stores using the shared immutable binding
		RegisterEventStore(builder.Services);
		RegisterSnapshotStore(builder.Services);
		RegisterGlobalStreamQuery(builder.Services);

		// Register materialized view store if enabled via UseMaterializedViewStore()
		if (sqlBuilder.EnableMaterializedViewStore)
		{
			RegisterMaterializedViewStore(
				builder.Services,
				connectionFactory,
				sqlBuilder.MaterializedViewTableName,
				sqlBuilder.MaterializedViewPositionTableName);
		}

		// Register health checks if enabled and connection string is available
		if (options.HealthChecks.RegisterHealthChecks && !string.IsNullOrWhiteSpace(options.ConnectionString))
		{
			_ = builder.Services.AddHealthChecks()
				.AddEventStoreHealthCheck(
					name: options.HealthChecks.EventStoreHealthCheckName,
					tags: ["eventstore", "sqlserver", "eventsourcing"])
				.AddSnapshotStoreHealthCheck(
					name: options.HealthChecks.SnapshotStoreHealthCheckName,
					tags: ["snapshotstore", "sqlserver", "eventsourcing"]);
		}
	}

	private static void RegisterEventStore(
		IServiceCollection services)
	{
		services.AddDefaultTenantContext();
		// AddTenantAwareStore builds the store (injecting ITenantContext, since this store's constructor
		// declares one) AND emits the ITenantScopingCapability<IEventStore> marker inseparably.
		services.AddTenantAwareStore<IEventStore, SqlServerEventStore>(sp =>
		{
			var binding = sp.GetRequiredService<ProviderBinding>();
			var tenantContext = sp.GetRequiredService<ITenantContext>();
			return new SqlServerEventStore(
				binding.ConnectionFactory,
				sp.GetRequiredService<ILogger<SqlServerEventStore>>(),
				tenantContext: tenantContext,
				internalSerializer: sp.GetService<ISerializer>(),
				payloadSerializer: sp.GetService<IPayloadSerializer>(),
				schema: binding.EventStoreSchema,
				table: binding.EventStoreTable,
				eventTypeInfoResolver: binding.EventTypeInfoResolver,
				capabilities: new SqlServerEventStoreCapabilities(binding.ConnectionFactory, binding.EventStoreSchema,
					binding.EventStoreTable, tenantContext, binding.OwnsPrimaryConnections, binding.SourceIdentity));
		});

		SqlServerEventSourcingServiceCollectionExtensions.RegisterEventStoreTelemetryWrapper(services);
	}

	private static void RegisterSnapshotStore(
		IServiceCollection services)
	{
		// Self-sufficient rather than order-dependent: this method resolves ITenantContext as a REQUIRED
		// service, so it wires the default itself instead of relying on a sibling registration having run
		// first. TryAdd makes it idempotent, and a consumer's own context still wins.
		_ = services.AddDefaultTenantContext();

		// AddTenantAwareStore builds the store (injecting ITenantContext, since this store's constructor
		// declares one) AND emits the ITenantScopingCapability<ISnapshotStore> marker inseparably, mirroring
		// RegisterEventStore above. Without it this builder path registered a tenant-scoped snapshot store
		// that attested nothing, and RowDiscriminator rejected the whole host at startup.
		_ = services.AddTenantAwareStore<ISnapshotStore, SqlServerSnapshotStore>(sp =>
		{
			var binding = sp.GetRequiredService<ProviderBinding>();
			// The tenant context is a required dependency, so the partition this store writes to is
			// decided the same way on every registration path. It was previously optional, and omitting
			// it here collapsed every tenant onto one untenanted row per aggregate id -- a silent
			// cross-tenant overwrite. That state is no longer expressible.
			return new SqlServerSnapshotStore(
				binding.ConnectionFactory,
				sp.GetRequiredService<ILogger<SqlServerSnapshotStore>>(),
				tenantContext: sp.GetRequiredService<ITenantContext>(),
				schema: binding.SnapshotStoreSchema,
				table: binding.SnapshotStoreTable);
		});

		SqlServerEventSourcingServiceCollectionExtensions.RegisterSnapshotStoreTelemetryWrapper(services);
	}

	private static void RegisterGlobalStreamQuery(
		IServiceCollection services)
	{
		services.TryAddSingleton<IGlobalStreamQuery>(sp =>
		{
			var binding = sp.GetRequiredService<ProviderBinding>();
			// The provider is wrapped in ContiguousGlobalStreamQuery HERE rather than left to the
			// consumer, because a decorator a host can forget to add is a guarantee that silently is
			// not there. Wrapping is also the assertion that THIS provider allocates positions inside
			// the appending transaction, which is the precondition that makes waiting on a gap sound.
			var provider = new SqlServerGlobalStreamQuery(
				binding.ConnectionFactory,
				Options.Create(new SqlServerEventSourcingOptions
				{
					EventStoreSchema = binding.EventStoreSchema,
					EventStoreTable = binding.EventStoreTable,
				}));

			IGlobalStreamQuery global = new ContiguousGlobalStreamQuery(
				provider,
				sp.GetRequiredService<ILogger<ContiguousGlobalStreamQuery>>());
			var cold = TieredStorageServiceCollectionExtensions.ResolveGlobalColdStore(sp, binding.SourceIdentity);
			if (cold is null)
			{
				return global;
			}
			if (!binding.OwnsPrimaryConnections)
			{
				throw new InvalidOperationException("Global archive hydration requires an owned primary SQL Server connection factory.");
			}
			return new TieredGlobalStreamQuery(global, cold, new SqlServerAuthoritativeEventReader(
				binding.ConnectionFactory, binding.EventStoreSchema, binding.EventStoreTable, authorizedTenant: null));
		});
	}

	private static void RegisterMaterializedViewStore(
		IServiceCollection services,
		Func<IServiceProvider, Func<SqlConnection>> connectionFactory,
		string? viewTableName,
		string? positionTableName)
	{
		// Idempotent single-tenant default, so GetRequiredService<ITenantContext>() below always resolves
		// for a host that never registered multi-tenancy. A multi-tenant host registers its own first and
		// TryAdd leaves it alone.
		services.AddDefaultTenantContext();

		services.TryAddSingleton<Excalibur.EventSourcing.IMaterializedViewStore>(sp =>
		{
			var factory = connectionFactory(sp);
			return new SqlServerMaterializedViewStore(
				factory,
				sp.GetRequiredService<ILogger<SqlServerMaterializedViewStore>>(),
				sp.GetRequiredService<ITenantContext>(),
				viewTableName,
				positionTableName);
		});
	}
	private sealed record ProviderBinding(
		Func<SqlConnection> ConnectionFactory,
		bool OwnsPrimaryConnections,
		string EventStoreSchema,
		string EventStoreTable,
		string SnapshotStoreSchema,
		string SnapshotStoreTable,
		System.Text.Json.Serialization.Metadata.IJsonTypeInfoResolver? EventTypeInfoResolver)
	{
		internal EventStoreSourceIdentity SourceIdentity { get; } = new();
	}

}
