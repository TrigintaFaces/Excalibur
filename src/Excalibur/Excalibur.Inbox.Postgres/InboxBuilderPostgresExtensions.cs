// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch;
using Excalibur.Inbox;
using Excalibur.Inbox.DependencyInjection;
using Excalibur.Inbox.Postgres;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Npgsql;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring Postgres provider on <see cref="IInboxBuilder"/>.
/// </summary>
public static class InboxBuilderPostgresExtensions
{
	/// <summary>
	/// Configures the inbox to use Postgres storage.
	/// </summary>
	/// <param name="builder">The inbox builder.</param>
	/// <param name="configure">Configuration action for the Postgres inbox builder.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <example>
	/// <code>
	/// services.AddExcaliburInbox(inbox =&gt;
	/// {
	///     inbox.UsePostgres(pg =&gt;
	///     {
	///         pg.ConnectionString("Host=localhost;Database=MyApp;")
	///           .SchemaName("public")
	///           .TableName("inbox_messages");
	///     });
	/// });
	/// </code>
	/// </example>
	public static IInboxBuilder UsePostgres(
		this IInboxBuilder builder,
		Action<IPostgresInboxBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		var options = new PostgresInboxOptions();
		var pgBuilder = new PostgresInboxBuilder(options);
		configure(pgBuilder);

		var hasBuilderConnection = pgBuilder.DataSourceFactoryFunc is not null
			|| pgBuilder.DataSourceInstance is not null
			|| pgBuilder.ConnectionStringNameValue is not null;

		RegisterOptionsAndServices(builder, pgBuilder, options, hasBuilderConnection);

		return builder;
	}

	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options validation/binding uses reflection by design.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design.")]
	private static void RegisterOptionsAndServices(
		IInboxBuilder builder,
		PostgresInboxBuilder pgBuilder,
		PostgresInboxOptions options,
		bool hasBuilderConnection)
	{
		_ = builder.Services.Configure<PostgresInboxOptions>(opt =>
		{
			opt.ConnectionString = options.ConnectionString;
			opt.SchemaName = options.SchemaName;
			opt.TableName = options.TableName;
			opt.CommandTimeoutSeconds = options.CommandTimeoutSeconds;
			opt.MaxRetryCount = options.MaxRetryCount;
		});

		if (pgBuilder.BindConfigurationPath is not null)
		{
			builder.Services.AddOptions<PostgresInboxOptions>()
				.BindConfiguration(pgBuilder.BindConfigurationPath)
				.ValidateOnStart();
		}

		builder.Services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<PostgresInboxOptions>>(
				new PostgresInboxOptionsValidator { HasBuilderConnection = hasBuilderConnection }));
		builder.Services.AddOptions<PostgresInboxOptions>().ValidateOnStart();

		// Register connection factory from builder state
		if (pgBuilder.DataSourceInstance is not null)
		{
			var ds = pgBuilder.DataSourceInstance;
			builder.Services.TryAddSingleton<Func<NpgsqlConnection>>(() =>
				ds.CreateConnection());
		}
		else if (pgBuilder.DataSourceFactoryFunc is not null)
		{
			var factory = pgBuilder.DataSourceFactoryFunc;
			builder.Services.TryAddSingleton<Func<NpgsqlConnection>>(sp =>
			{
				var ds = factory(sp);
				return () => ds.CreateConnection();
			});
		}
		else if (pgBuilder.ConnectionStringNameValue is not null)
		{
			var connStrName = pgBuilder.ConnectionStringNameValue;
			builder.Services.TryAddSingleton<Func<NpgsqlConnection>>(sp =>
			{
				var config = sp.GetRequiredService<IConfiguration>();
				var resolved = config.GetConnectionString(connStrName)
					?? throw new InvalidOperationException(
						$"Connection string '{connStrName}' not found in IConfiguration.");
				return () => new NpgsqlConnection(resolved);
			});
		}

		// Register the inbox store and its provider/default keys. Without this the provider is
		// advertised-but-unwired: AddExcaliburInbox(inbox => inbox.UsePostgres(...)) registers options
		// and a connection factory but no IInboxStore, so the documented entry point resolves nothing
		// under the "postgres" or "default" key. The fail-closed single-tenant default guarantees a
		// non-null ITenantContext; the multi-tenancy composition replaces it with the ambient context.
		builder.Services.AddDefaultTenantContext();
		// AddTenantScopedStore builds the store (injecting ITenantContext, on which PostgresInboxStore
		// filters every keyed read) AND emits the ITenantScopingCapability<IInboxStore> marker inseparably
		// (S886 rw2ull — a store wired without the tenant predicate cannot carry a truthful marker).
		builder.Services.AddTenantScopedStore<IInboxStore, PostgresInboxStore>((sp, tenantContext) =>
		{
			var inboxOptions = sp.GetRequiredService<IOptions<PostgresInboxOptions>>();
			var logger = sp.GetRequiredService<ILogger<PostgresInboxStore>>();
			var connectionFactory = sp.GetService<Func<NpgsqlConnection>>();
			return connectionFactory is not null
				? new PostgresInboxStore(connectionFactory, inboxOptions.Value, logger, tenantContext, sp.GetService<IOptions<TenantContextOptions>>())
				: new PostgresInboxStore(inboxOptions, logger, tenantContext, sp.GetService<IOptions<TenantContextOptions>>());
		});
		builder.Services.AddKeyedSingleton<IInboxStore>(
			"postgres", (sp, _) => sp.GetRequiredService<PostgresInboxStore>());
		builder.Services.TryAddKeyedSingleton<IInboxStore>(
			"default", (sp, _) => sp.GetRequiredKeyedService<IInboxStore>("postgres"));
		builder.Services.AddInboxSchemaValidation();
		builder.Services.AddSingleton<IInboxSchemaValidator>(sp => sp.GetRequiredService<PostgresInboxStore>());
	}
}
