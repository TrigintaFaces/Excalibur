// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Saga.DependencyInjection;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

using Npgsql;

namespace Excalibur.Saga.Postgres.DependencyInjection;

/// <summary>
/// Extension methods for configuring Postgres saga stores on <see cref="ISagaBuilder"/>.
/// </summary>
public static class SagaBuilderPostgresExtensions
{
	/// <summary>
	/// Configures the saga builder to use Postgres for saga store persistence.
	/// </summary>
	/// <param name="builder">The saga builder.</param>
	/// <param name="configure">Configuration action for the Postgres saga builder.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <example>
	/// <code>
	/// services.AddExcaliburSagas(saga =&gt;
	/// {
	///     saga.UsePostgres(pg =&gt;
	///     {
	///         pg.ConnectionString("Host=localhost;Database=MyApp;")
	///           .SchemaName("dispatch")
	///           .TableName("sagas");
	///     });
	/// });
	/// </code>
	/// </example>
	public static ISagaBuilder UsePostgres(
		this ISagaBuilder builder,
		Action<IPostgresSagaBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		var options = new PostgresSagaOptions();
		var pgBuilder = new PostgresSagaBuilder(options);
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
		ISagaBuilder builder,
		PostgresSagaBuilder pgBuilder,
		PostgresSagaOptions options,
		bool hasBuilderConnection)
	{
		// Register options from builder state
		_ = builder.Services.Configure<PostgresSagaOptions>(opt =>
		{
			opt.ConnectionString = options.ConnectionString;
			opt.Schema = options.Schema;
			opt.TableName = options.TableName;
			opt.CommandTimeoutSeconds = options.CommandTimeoutSeconds;
		});

		// Register BindConfiguration if set
		if (pgBuilder.BindConfigurationPath is not null)
		{
			builder.Services.AddOptions<PostgresSagaOptions>()
				.BindConfiguration(pgBuilder.BindConfigurationPath)
				.ValidateOnStart();
		}

		// Register ValidateOnStart with connection awareness
		builder.Services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<PostgresSagaOptions>>(
				new PostgresSagaOptionsValidator { HasBuilderConnection = hasBuilderConnection }));
		builder.Services.AddOptions<PostgresSagaOptions>().ValidateOnStart();

		// Register connection factory that resolves from builder state
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
		// else: ConnectionString or BindConfiguration — store handles via IOptions<PostgresSagaOptions>
	}
}
