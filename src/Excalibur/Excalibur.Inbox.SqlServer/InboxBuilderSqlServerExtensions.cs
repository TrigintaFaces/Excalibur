// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch;
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

	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options validation/binding uses reflection by design.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design.")]
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

		if (sqlBuilder.BindConfigurationPath is not null)
		{
			builder.Services.AddOptions<SqlServerInboxOptions>()
				.BindConfiguration(sqlBuilder.BindConfigurationPath)
				.ValidateOnStart();

			if (!string.IsNullOrWhiteSpace(options.ConnectionString))
			{
				var explicitConnectionString = options.ConnectionString;
				_ = builder.Services.PostConfigure<SqlServerInboxOptions>(opt =>
				{
					opt.ConnectionString = explicitConnectionString;
				});
			}
		}

		// Register ValidateOnStart
		builder.Services.AddSingleton<IValidateOptions<SqlServerInboxOptions>>(
			new SqlServerInboxBuilderOptionsValidator { HasBuilderConnection = hasBuilderConnection });
		builder.Services.AddOptions<SqlServerInboxOptions>().ValidateOnStart();

		// Register inbox store with connection factory
		builder.Services.TryAddSingleton(sp =>
		{
			var factory = connectionFactory(sp);
			var inboxOptions = sp.GetRequiredService<IOptions<SqlServerInboxOptions>>().Value;
			var logger = sp.GetRequiredService<ILogger<SqlServerInboxStore>>();
			return new SqlServerInboxStore(factory, inboxOptions, logger);
		});
		builder.Services.AddKeyedSingleton<IInboxStore>(
			"sqlserver", (sp, _) => sp.GetRequiredService<SqlServerInboxStore>());
		builder.Services.TryAddKeyedSingleton<IInboxStore>(
			"default", (sp, _) => sp.GetRequiredKeyedService<IInboxStore>("sqlserver"));

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
