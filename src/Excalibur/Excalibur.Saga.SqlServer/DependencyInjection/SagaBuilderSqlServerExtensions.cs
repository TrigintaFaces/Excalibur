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
/// CDC builder pattern (see <c>CdcBuilderSqlServerExtensions</c>).
/// </para>
/// </remarks>
public static class SagaBuilderSqlServerExtensions
{
	/// <summary>
	/// Configures the saga builder to use SQL Server for saga store, timeout store,
	/// and monitoring service.
	/// </summary>
	/// <param name="builder">The saga builder.</param>
	/// <param name="connectionString">The SQL Server connection string.</param>
	/// <param name="configureSagaStore">Optional action to configure saga store options.</param>
	/// <param name="configureTimeoutStore">Optional action to configure saga timeout store options.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="builder"/> is null.
	/// </exception>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="connectionString"/> is null or whitespace.
	/// </exception>
	/// <example>
	/// <code>
	/// services.AddExcaliburSaga(saga =&gt;
	/// {
	///     saga.UseSqlServer(connectionString)
	///         .WithOrchestration()
	///         .WithTimeouts();
	/// });
	/// </code>
	/// </example>
	public static ISagaBuilder UseSqlServer(
		this ISagaBuilder builder,
		string connectionString,
		Action<SqlServerSagaStoreOptions>? configureSagaStore = null,
		Action<SqlServerSagaTimeoutStoreOptions>? configureTimeoutStore = null)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

		_ = builder.Services.AddSqlServerSagaStore(connectionString, configureSagaStore);
		_ = builder.Services.AddSqlServerSagaTimeoutStore(connectionString, configureTimeoutStore);
		_ = builder.Services.AddSqlServerSagaMonitoringService(connectionString);

		// Register correlation query options + implementation
		_ = builder.Services.AddOptions<SagaCorrelationQueryOptions>()
			.ValidateDataAnnotations()
			.ValidateOnStart();

		builder.Services.TryAddSingleton<ISagaCorrelationQuery>(sp =>
		{
			Func<SqlConnection> factory = () => new SqlConnection(connectionString);
			var storeOpts = sp.GetRequiredService<IOptions<SqlServerSagaStoreOptions>>();
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
	/// <param name="connectionString">The SQL Server connection string.</param>
	/// <param name="configureOptions">Optional action to configure idempotency options.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <example>
	/// <code>
	/// services.AddExcaliburSaga(saga =&gt;
	/// {
	///     saga.UseSqlServer(connectionString)
	///         .WithSqlServerIdempotency(connectionString);
	/// });
	/// </code>
	/// </example>
	public static ISagaBuilder WithSqlServerIdempotency(
		this ISagaBuilder builder,
		string connectionString,
		Action<SqlServerSagaIdempotencyOptions>? configureOptions = null)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

		_ = builder.Services.AddOptions<SqlServerSagaIdempotencyOptions>()
			.ValidateDataAnnotations()
			.ValidateOnStart();

		if (configureOptions is not null)
		{
			_ = builder.Services.Configure(configureOptions);
		}

		builder.Services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<SqlServerSagaIdempotencyOptions>, SqlServerSagaIdempotencyOptionsValidator>());

		builder.Services.TryAddSingleton<ISagaIdempotencyProvider>(sp =>
		{
			var options = sp.GetRequiredService<IOptions<SqlServerSagaIdempotencyOptions>>();
			var logger = sp.GetRequiredService<ILogger<SqlServerSagaIdempotencyProvider>>();
			return new SqlServerSagaIdempotencyProvider(connectionString, options, logger);
		});

		return builder;
	}
}
