// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Abstractions.Messaging;
using Excalibur.Saga.Abstractions;
using Excalibur.Saga.DependencyInjection;
using Excalibur.Saga.Idempotency;
using Excalibur.Saga.Queries;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Saga.SqlServer.DependencyInjection;

/// <summary>
/// Extension methods for configuring SQL Server saga stores on <see cref="ISagaBuilder"/>.
/// </summary>
public static class SagaBuilderSqlServerExtensions
{
	/// <summary>
	/// Configures the saga builder to use SQL Server for saga store, timeout store,
	/// and monitoring service.
	/// </summary>
	/// <param name="builder">The saga builder.</param>
	/// <param name="configure">Action to configure the SQL Server saga builder.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="builder"/> or <paramref name="configure"/> is null.
	/// </exception>
	/// <example>
	/// <code>
	/// services.AddExcalibur(x => x.AddSagas(saga =&gt;
	/// {
	///     saga.UseSqlServer(sql =&gt;
	///     {
	///         sql.ConnectionString(connectionString)
	///            .SchemaName("dispatch")
	///            .TableName("sagas");
	///     })
	///     .WithOrchestration()
	///     .WithTimeouts();
	/// }));
	/// </code>
	/// </example>
	public static ISagaBuilder UseSqlServer(
		this ISagaBuilder builder,
		Action<ISqlServerSagaBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		var options = new SqlServerSagaStoreOptions();
		var sqlBuilder = new SqlServerSagaBuilder(options);
		configure(sqlBuilder);

		var connectionFactory = ResolveConnectionFactory(sqlBuilder);
		var hasBuilderConnection = sqlBuilder.ConnectionFactoryFunc is not null
			|| sqlBuilder.ConnectionStringNameValue is not null;

		RegisterOptionsAndServices(builder, sqlBuilder, options, connectionFactory, hasBuilderConnection);

		return builder;
	}

	/// <summary>
	/// Configures the saga builder to use SQL Server for idempotency tracking.
	/// </summary>
	/// <param name="builder">The saga builder.</param>
	/// <param name="configure">Action to configure SQL Server idempotency options.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="builder"/> or <paramref name="configure"/> is null.
	/// </exception>
	public static ISagaBuilder WithSqlServerIdempotency(
		this ISagaBuilder builder,
		Action<SqlServerSagaIdempotencyOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		_ = builder.Services.AddOptions<SqlServerSagaIdempotencyOptions>()
			.ValidateOnStart();

		_ = builder.Services.Configure(configure);

		builder.Services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<SqlServerSagaIdempotencyOptions>, SqlServerSagaIdempotencyOptionsValidator>());

		builder.Services.TryAddSingleton<ISagaIdempotencyProvider>(sp =>
		{
			var idempotencyOptions = sp.GetRequiredService<IOptions<SqlServerSagaIdempotencyOptions>>();
			var logger = sp.GetRequiredService<ILogger<SqlServerSagaIdempotencyProvider>>();
			return new SqlServerSagaIdempotencyProvider(idempotencyOptions.Value.ConnectionString!, idempotencyOptions, logger);
		});

		return builder;
	}

	private static Func<IServiceProvider, Func<SqlConnection>> ResolveConnectionFactory(
		SqlServerSagaBuilder sqlBuilder)
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
			var opts = sp.GetRequiredService<IOptions<SqlServerSagaStoreOptions>>();
			return () => new SqlConnection(opts.Value.ConnectionString);
		};
	}

	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options validation/binding uses reflection by design.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design.")]
	private static void RegisterOptionsAndServices(
		ISagaBuilder builder,
		SqlServerSagaBuilder sqlBuilder,
		SqlServerSagaStoreOptions options,
		Func<IServiceProvider, Func<SqlConnection>> connectionFactory,
		bool hasBuilderConnection)
	{
		// Register saga store options
		_ = builder.Services.Configure<SqlServerSagaStoreOptions>(opt =>
		{
			opt.ConnectionString = options.ConnectionString;
			opt.SchemaName = options.SchemaName;
			opt.TableName = options.TableName;
		});

		if (sqlBuilder.BindConfigurationPath is not null)
		{
			builder.Services.AddOptions<SqlServerSagaStoreOptions>()
				.BindConfiguration(sqlBuilder.BindConfigurationPath)
				.ValidateOnStart();

			if (!string.IsNullOrWhiteSpace(options.ConnectionString))
			{
				var explicitConnectionString = options.ConnectionString;
				_ = builder.Services.PostConfigure<SqlServerSagaStoreOptions>(opt =>
				{
					opt.ConnectionString = explicitConnectionString;
				});
			}
		}

		// Register ValidateOnStart
		builder.Services.AddSingleton<IValidateOptions<SqlServerSagaStoreOptions>>(
			new SqlServerSagaBuilderOptionsValidator { HasBuilderConnection = hasBuilderConnection });
		builder.Services.AddOptions<SqlServerSagaStoreOptions>().ValidateOnStart();

		// Register saga store with connection factory
		builder.Services.TryAddSingleton(sp =>
		{
			var factory = connectionFactory(sp);
			var storeOptions = sp.GetRequiredService<IOptions<SqlServerSagaStoreOptions>>();
			var logger = sp.GetRequiredService<ILogger<SqlServerSagaStore>>();
			var serializer = sp.GetRequiredService<Excalibur.Dispatch.Serialization.DispatchJsonSerializer>();
			return new SqlServerSagaStore(factory, storeOptions, logger, serializer);
		});
		builder.Services.AddKeyedSingleton<ISagaStore>(
			"sqlserver", (sp, _) => sp.GetRequiredService<SqlServerSagaStore>());
		builder.Services.TryAddKeyedSingleton<ISagaStore>(
			"default", (sp, _) => sp.GetRequiredKeyedService<ISagaStore>("sqlserver"));

		// Register timeout store sharing the same connection
		_ = builder.Services.Configure<SqlServerSagaTimeoutStoreOptions>(opt =>
		{
			opt.ConnectionString = options.ConnectionString;
		});
		builder.Services.TryAddSingleton(sp =>
		{
			var factory = connectionFactory(sp);
			var timeoutOptions = sp.GetRequiredService<IOptions<SqlServerSagaTimeoutStoreOptions>>();
			var logger = sp.GetRequiredService<ILogger<SqlServerSagaTimeoutStore>>();
			return new SqlServerSagaTimeoutStore(factory, timeoutOptions, logger);
		});
		builder.Services.TryAddSingleton<ISagaTimeoutStore>(
			sp => sp.GetRequiredService<SqlServerSagaTimeoutStore>());

		// Register monitoring service sharing the same connection
		builder.Services.TryAddSingleton(sp =>
		{
			var factory = connectionFactory(sp);
			var storeOptions = sp.GetRequiredService<IOptions<SqlServerSagaStoreOptions>>();
			var logger = sp.GetRequiredService<ILogger<SqlServerSagaMonitoringService>>();
			return new SqlServerSagaMonitoringService(factory, storeOptions, logger);
		});
		builder.Services.TryAddSingleton<ISagaMonitoringService>(
			sp => sp.GetRequiredService<SqlServerSagaMonitoringService>());

		// Register correlation query
		_ = builder.Services.AddOptions<SagaCorrelationQueryOptions>()
			.ValidateOnStart();
		builder.Services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<SagaCorrelationQueryOptions>, SagaCorrelationQueryOptionsValidator>());
		builder.Services.TryAddSingleton<ISagaCorrelationQuery>(sp =>
		{
			var factory = connectionFactory(sp);
			var storeOptions = sp.GetRequiredService<IOptions<SqlServerSagaStoreOptions>>();
			var queryOpts = sp.GetRequiredService<IOptions<SagaCorrelationQueryOptions>>();
			var logger = sp.GetRequiredService<ILogger<SqlServerSagaCorrelationQuery>>();
			return new SqlServerSagaCorrelationQuery(factory, storeOptions, queryOpts, logger);
		});
	}
}
