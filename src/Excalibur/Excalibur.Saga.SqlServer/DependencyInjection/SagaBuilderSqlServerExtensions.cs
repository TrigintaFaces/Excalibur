// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Saga.DependencyInjection;
using Excalibur.Saga.Idempotency;
using Excalibur.Saga.Queries;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Saga.SqlServer.DependencyInjection;

/// <summary>
/// Extension methods for configuring SQL Server saga stores on <see cref="ISagaBuilder"/>.
/// </summary>
/// <remarks>
/// <para>
/// These extensions provide fluent provider selection following the established
/// unified builder pattern where the connection string is configured via options.
/// </para>
/// </remarks>
public static class SagaBuilderSqlServerExtensions
{
	/// <summary>
	/// Configures the saga builder to use SQL Server for saga store, timeout store,
	/// and monitoring service.
	/// </summary>
	/// <param name="builder">The saga builder.</param>
	/// <param name="configure">Action to configure SQL Server saga options (connection string, schema, table names).</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="builder"/> or <paramref name="configure"/> is null.
	/// </exception>
	/// <example>
	/// <code>
	/// services.AddExcaliburSaga(saga =&gt;
	/// {
	///     saga.UseSqlServer(sql =&gt;
	///         {
	///             sql.ConnectionString = connectionString;
	///         })
	///         .WithOrchestration()
	///         .WithTimeouts();
	/// });
	/// </code>
	/// </example>
	public static ISagaBuilder UseSqlServer(
		this ISagaBuilder builder,
		Action<SqlServerSagaStoreOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		_ = builder.Services.AddSqlServerSagaStore(configure);

		// Propagate ConnectionString from saga store options to timeout store options
		_ = builder.Services.AddSqlServerSagaTimeoutStore(timeout =>
		{
			// Resolve the connection string from the saga store options
			var sagaOpts = new SqlServerSagaStoreOptions();
			configure(sagaOpts);
			timeout.ConnectionString = sagaOpts.ConnectionString;
		});

		_ = builder.Services.AddSqlServerSagaMonitoringService(configure);

		// Register correlation query options + implementation
		_ = builder.Services.AddOptions<SagaCorrelationQueryOptions>()
			.ValidateOnStart();

		builder.Services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<SagaCorrelationQueryOptions>, SagaCorrelationQueryOptionsValidator>());

		builder.Services.TryAddSingleton<ISagaCorrelationQuery>(sp =>
		{
			var storeOpts = sp.GetRequiredService<IOptions<SqlServerSagaStoreOptions>>();
			Func<SqlConnection> factory = () => new SqlConnection(storeOpts.Value.ConnectionString);
			var queryOpts = sp.GetRequiredService<IOptions<SagaCorrelationQueryOptions>>();
			var logger = sp.GetRequiredService<ILogger<SqlServerSagaCorrelationQuery>>();
			return new SqlServerSagaCorrelationQuery(factory, storeOpts, queryOpts, logger);
		});

		return builder;
	}

	/// <summary>
	/// Configures the saga builder to use SQL Server for idempotency tracking.
	/// </summary>
	/// <param name="builder">The saga builder.</param>
	/// <param name="configure">Action to configure SQL Server idempotency options (connection string, schema, table names).</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="builder"/> or <paramref name="configure"/> is null.
	/// </exception>
	/// <example>
	/// <code>
	/// services.AddExcaliburSaga(saga =&gt;
	/// {
	///     saga.UseSqlServer(sql =&gt; { sql.ConnectionString = connectionString; })
	///         .WithSqlServerIdempotency(sql =&gt; { sql.ConnectionString = connectionString; });
	/// });
	/// </code>
	/// </example>
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
			var options = sp.GetRequiredService<IOptions<SqlServerSagaIdempotencyOptions>>();
			var logger = sp.GetRequiredService<ILogger<SqlServerSagaIdempotencyProvider>>();
			return new SqlServerSagaIdempotencyProvider(options.Value.ConnectionString!, options, logger);
		});

		return builder;
	}
}
