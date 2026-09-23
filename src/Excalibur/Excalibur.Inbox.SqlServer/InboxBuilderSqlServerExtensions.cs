// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.Inbox;
using Excalibur.Inbox.DependencyInjection;
using Excalibur.Inbox.SqlServer;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring SQL Server provider on <see cref="IInboxBuilder"/>.
/// </summary>
public static class InboxBuilderSqlServerExtensions
{
	/// <summary>
	/// Configures the inbox to use SQL Server storage.
	/// </summary>
	/// <param name="builder">The inbox builder.</param>
	/// <param name="configure">Action to configure the SQL Server inbox builder.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="builder"/> or <paramref name="configure"/> is null.
	/// </exception>
	/// <example>
	/// <code>
	/// services.AddExcaliburInbox(inbox =&gt;
	/// {
	///     inbox.UseSqlServer(sql =&gt;
	///     {
	///         sql.ConnectionString(connectionString)
	///            .SchemaName("dbo")
	///            .TableName("inbox_messages");
	///     });
	/// });
	/// </code>
	/// </example>
	/// <remarks>
	/// To populate the options from configuration, call
	/// <c>services.AddOptions&lt;SqlServerInboxOptions&gt;().BindConfiguration("Section:Path")</c>
	/// after this registration. Configuration binding is reflective, so it is not trim- or
	/// native-AOT-safe; doing it at your own call site puts the warning where the trimmer can see it
	/// rather than on this method, which is otherwise trim-safe.
	/// </remarks>
	public static IInboxBuilder UseSqlServer(
		this IInboxBuilder builder,
		Action<ISqlServerInboxBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		var options = new SqlServerInboxOptions();
		var sqlBuilder = new SqlServerInboxBuilder(options);
		configure(sqlBuilder);

		var connectionFactory = ResolveConnectionFactory(sqlBuilder);
		var hasBuilderConnection = sqlBuilder.ConnectionFactoryFunc is not null
			|| sqlBuilder.ConnectionStringNameValue is not null;

		RegisterOptionsAndServices(builder, sqlBuilder, options, connectionFactory, hasBuilderConnection);

		return builder;
	}

	private static Func<IServiceProvider, Func<SqlConnection>> ResolveConnectionFactory(
		SqlServerInboxBuilder sqlBuilder)
	{
		if (sqlBuilder.ConnectionFactoryFunc is not null)
		{
			return sqlBuilder.ConnectionFactoryFunc;
		}

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

		return sp =>
		{
			var opts = sp.GetRequiredService<IOptions<SqlServerInboxOptions>>();
			return () => new SqlConnection(opts.Value.ConnectionString);
		};
	}

	private static void RegisterOptionsAndServices(
		IInboxBuilder builder,
		SqlServerInboxBuilder sqlBuilder,
		SqlServerInboxOptions options,
		Func<IServiceProvider, Func<SqlConnection>> connectionFactory,
		bool hasBuilderConnection)
	{
		_ = builder.Services.Configure<SqlServerInboxOptions>(opt =>
		{
			opt.ConnectionString = options.ConnectionString;
			opt.SchemaName = options.SchemaName;
			opt.TableName = options.TableName;
			opt.CommandTimeoutSeconds = options.CommandTimeoutSeconds;
			opt.MaxRetryCount = options.MaxRetryCount;
		});

		// Register ValidateOnStart
		builder.Services.AddSingleton<IValidateOptions<SqlServerInboxOptions>>(
			new SqlServerInboxBuilderOptionsValidator { HasBuilderConnection = hasBuilderConnection });

		// Enforce the SQL-identifier allowlist on schema/table names for parity with the
		// options-based registration path (SqlServerInboxExtensions) — otherwise a malicious
		// identifier supplied via SchemaName()/TableName() would reach SQL text unvalidated.
		builder.Services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<SqlServerInboxOptions>, SqlServerInboxOptionsValidator>());

		builder.Services.AddOptions<SqlServerInboxOptions>().ValidateOnStart();

		// The fail-closed single-tenant default guarantees a non-null ITenantContext for the tenant
		// predicate; the multi-tenancy composition replaces it with the ambient context.
		builder.Services.AddDefaultTenantContext();

		// Register inbox store with connection factory
		// AddTenantAwareStore builds the store (injecting ITenantContext so the documented UseSqlServer()
		// path applies the tenant predicate, since this store's constructor declares one) AND emits the
		// ITenantScopingCapability<IInboxStore> marker inseparably (no lying marker).
		builder.Services.AddTenantAwareStore<IInboxStore, SqlServerInboxStore>(sp =>
		{
			var factory = connectionFactory(sp);
			var inboxOptions = sp.GetRequiredService<IOptions<SqlServerInboxOptions>>().Value;
			var logger = sp.GetRequiredService<ILogger<SqlServerInboxStore>>();
			return new SqlServerInboxStore(factory, inboxOptions, logger, sp.GetRequiredService<ITenantContext>(), sp.GetRequiredService<IOptions<TenantContextOptions>>());
		});
		builder.Services.AddKeyedSingleton<IInboxStore>(
			"sqlserver", (sp, _) => sp.GetRequiredService<SqlServerInboxStore>());
		builder.Services.TryAddKeyedSingleton<IInboxStore>(
			"default", (sp, _) => sp.GetRequiredKeyedService<IInboxStore>("sqlserver"));
		builder.Services.AddInboxSchemaValidation();
		builder.Services.AddSingleton<IInboxSchemaValidator>(sp => sp.GetRequiredService<SqlServerInboxStore>());

		// The ITenantScopingCapability<IInboxStore> marker is emitted by AddTenantAwareStore above,
		// inseparably from the store registration.

		// Register health checks if enabled
		if (sqlBuilder.HealthChecksEnabled && !string.IsNullOrWhiteSpace(options.ConnectionString))
		{
			_ = builder.Services.AddHealthChecks()
				.AddSqlServer(
					options.ConnectionString,
					name: sqlBuilder.HealthCheckName,
					tags: ["inbox", "sqlserver"]);
		}
	}
}
