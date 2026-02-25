// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.ErrorHandling;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Outbox.SqlServer;

/// <summary>
/// Extension methods for configuring SQL Server provider on <see cref="IOutboxBuilder"/>.
/// </summary>
/// <remarks>
/// <para>
/// These extensions provide fluent provider selection by adding
/// provider-specific configuration to the core <see cref="IOutboxBuilder"/> interface.
/// </para>
/// </remarks>
public static class OutboxBuilderSqlServerExtensions
{
	/// <summary>
	/// Configures the outbox to use SQL Server as the storage provider.
	/// </summary>
	/// <param name="builder">The outbox builder.</param>
	/// <param name="connectionString">The SQL Server connection string.</param>
	/// <param name="configure">Optional action to configure SQL Server-specific options.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="builder"/> is null.
	/// </exception>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="connectionString"/> is null, empty, or whitespace.
	/// </exception>
	/// <remarks>
	/// <para>
	/// This is the primary method for configuring SQL Server as the outbox storage provider.
	/// It registers the <see cref="SqlServerOutboxStore"/> and related services.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// services.AddExcaliburOutbox(outbox =>
	/// {
	///     outbox.UseSqlServer(connectionString, sql =>
	///     {
	///         sql.SchemaName("Messaging")
	///            .TableName("OutboxMessages")
	///            .CommandTimeout(TimeSpan.FromSeconds(60))
	///            .UseRowLocking(true);
	///     })
	///     .EnableBackgroundProcessing();
	/// });
	/// </code>
	/// </example>
	public static IOutboxBuilder UseSqlServer(
		this IOutboxBuilder builder,
		string connectionString,
		Action<ISqlServerOutboxBuilder>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

		// Create and configure SQL Server options
		var sqlOptions = new SqlServerOutboxOptions { ConnectionString = connectionString, };

		if (configure is not null)
		{
			var sqlBuilder = new SqlServerOutboxBuilder(sqlOptions);
			configure(sqlBuilder);
		}

		// Register SQL Server options
		_ = builder.Services.Configure<SqlServerOutboxOptions>(opt =>
		{
			opt.ConnectionString = sqlOptions.ConnectionString;
			opt.SchemaName = sqlOptions.SchemaName;
			opt.OutboxTableName = sqlOptions.OutboxTableName;
			opt.TransportsTableName = sqlOptions.TransportsTableName;
			opt.DeadLetterTableName = sqlOptions.DeadLetterTableName;
			opt.CommandTimeoutSeconds = sqlOptions.CommandTimeoutSeconds;
			opt.UseRowLocking = sqlOptions.UseRowLocking;
			opt.DefaultBatchSize = sqlOptions.DefaultBatchSize;
			opt.MaxRetryCount = sqlOptions.MaxRetryCount;
			opt.RetryDelayMinutes = sqlOptions.RetryDelayMinutes;
		});

		// Register SQL Server outbox store
		builder.Services.TryAddSingleton<SqlServerOutboxStore>();
		builder.Services.TryAddSingleton<IOutboxStore>(sp => sp.GetRequiredService<SqlServerOutboxStore>());
		builder.Services.TryAddSingleton<IMultiTransportOutboxStore>(sp => sp.GetRequiredService<SqlServerOutboxStore>());

		return builder;
	}

	/// <summary>
	/// Configures the outbox to use SQL Server with a connection factory.
	/// </summary>
	/// <param name="builder">The outbox builder.</param>
	/// <param name="connectionFactory">A factory function that creates <see cref="SqlConnection"/> instances.</param>
	/// <param name="configure">Optional action to configure SQL Server-specific options.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="builder"/> or <paramref name="connectionFactory"/> is null.
	/// </exception>
	/// <remarks>
	/// <para>
	/// Use this overload for advanced scenarios like multi-database setups,
	/// custom connection pooling, or IDb integration.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// services.AddExcaliburOutbox(outbox =>
	/// {
	///     outbox.UseSqlServer(
	///         sp => () => (SqlConnection)sp.GetRequiredService&lt;IOutboxDb&gt;().Connection,
	///         sql => sql.SchemaName("messaging"))
	///         .EnableBackgroundProcessing();
	/// });
	/// </code>
	/// </example>
	public static IOutboxBuilder UseSqlServer(
		this IOutboxBuilder builder,
		Func<IServiceProvider, Func<SqlConnection>> connectionFactory,
		Action<ISqlServerOutboxBuilder>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(connectionFactory);

		// Create and configure SQL Server options
		var sqlOptions = new SqlServerOutboxOptions();

		if (configure is not null)
		{
			var sqlBuilder = new SqlServerOutboxBuilder(sqlOptions);
			configure(sqlBuilder);
		}

		// Register SQL Server options
		_ = builder.Services.Configure<SqlServerOutboxOptions>(opt =>
		{
			opt.SchemaName = sqlOptions.SchemaName;
			opt.OutboxTableName = sqlOptions.OutboxTableName;
			opt.TransportsTableName = sqlOptions.TransportsTableName;
			opt.DeadLetterTableName = sqlOptions.DeadLetterTableName;
			opt.CommandTimeoutSeconds = sqlOptions.CommandTimeoutSeconds;
			opt.UseRowLocking = sqlOptions.UseRowLocking;
			opt.DefaultBatchSize = sqlOptions.DefaultBatchSize;
			opt.MaxRetryCount = sqlOptions.MaxRetryCount;
			opt.RetryDelayMinutes = sqlOptions.RetryDelayMinutes;
		});

		// Register SQL Server outbox store with connection factory
		builder.Services.TryAddSingleton(sp =>
		{
			var factory = connectionFactory(sp);
			var options = sp.GetRequiredService<IOptions<SqlServerOutboxOptions>>().Value;
			var payloadSerializer = sp.GetService<IPayloadSerializer>();
			var logger = sp.GetRequiredService<ILogger<SqlServerOutboxStore>>();
			return new SqlServerOutboxStore(factory, options, payloadSerializer, logger);
		});
		builder.Services.TryAddSingleton<IOutboxStore>(sp => sp.GetRequiredService<SqlServerOutboxStore>());
		builder.Services.TryAddSingleton<IMultiTransportOutboxStore>(sp => sp.GetRequiredService<SqlServerOutboxStore>());

		return builder;
	}

	/// <summary>
	/// Configures the outbox to use SQL Server dead letter queue.
	/// </summary>
	/// <param name="builder">The outbox builder.</param>
	/// <param name="connectionString">The SQL Server connection string.</param>
	/// <param name="configure">Optional action to configure dead letter queue options.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="builder"/> is null.
	/// </exception>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="connectionString"/> is null, empty, or whitespace.
	/// </exception>
	/// <remarks>
	/// <para>
	/// The dead letter queue stores messages that have exceeded their retry limit.
	/// Call this method to enable dead letter functionality with SQL Server.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// services.AddExcaliburOutbox(outbox =>
	/// {
	///     outbox.UseSqlServer(connectionString)
	///           .WithSqlServerDeadLetterQueue(connectionString)
	///           .EnableBackgroundProcessing();
	/// });
	/// </code>
	/// </example>
	public static IOutboxBuilder WithSqlServerDeadLetterQueue(
		this IOutboxBuilder builder,
		string connectionString,
		Action<SqlServerDeadLetterQueueOptions>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

		_ = builder.Services.Configure<SqlServerDeadLetterQueueOptions>(opt =>
		{
			opt.ConnectionString = connectionString;
		});

		if (configure is not null)
		{
			_ = builder.Services.Configure(configure);
		}

		builder.Services.TryAddSingleton<SqlServerDeadLetterQueue>();
		builder.Services.TryAddSingleton<IDeadLetterQueue>(sp => sp.GetRequiredService<SqlServerDeadLetterQueue>());

		return builder;
	}
}
