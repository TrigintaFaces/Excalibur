// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Inbox.DependencyInjection;
using Excalibur.Inbox.Postgres;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
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
	}
}
