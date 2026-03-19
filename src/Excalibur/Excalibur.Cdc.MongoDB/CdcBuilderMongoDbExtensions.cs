// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Cdc.MongoDB;

/// <summary>
/// Extension methods for configuring MongoDB CDC provider on <see cref="ICdcBuilder"/>.
/// </summary>
/// <remarks>
/// <para>
/// These extensions provide fluent provider selection by adding
/// provider-specific configuration to the core <see cref="ICdcBuilder"/> interface.
/// </para>
/// </remarks>
public static class CdcBuilderMongoDbExtensions
{
	/// <summary>
	/// Configures the CDC processor to use MongoDB Change Streams.
	/// </summary>
	/// <param name="builder">The CDC builder.</param>
	/// <param name="configure">Action to configure MongoDB CDC options.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="builder"/> or <paramref name="configure"/> is null.
	/// </exception>
	/// <example>
	/// <code>
	/// services.AddCdcProcessor(cdc =&gt;
	/// {
	///     cdc.UseMongoDB(options =&gt;
	///     {
	///         options.Connection.ConnectionString = "mongodb://localhost:27017";
	///         options.ProcessorId = "order-cdc";
	///     })
	///     .EnableBackgroundProcessing();
	/// });
	/// </code>
	/// </example>
	public static ICdcBuilder UseMongoDB(
		this ICdcBuilder builder,
		Action<MongoDbCdcOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		_ = builder.Services.AddMongoDbCdc(configure);

		return builder;
	}

	/// <summary>
	/// Configures the CDC processor to use MongoDB Change Streams with a connection string.
	/// </summary>
	/// <param name="builder">The CDC builder.</param>
	/// <param name="connectionString">The MongoDB connection string.</param>
	/// <param name="processorId">The unique processor identifier.</param>
	/// <param name="configure">Optional additional configuration.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="builder"/> is null.
	/// </exception>
	/// <example>
	/// <code>
	/// services.AddCdcProcessor(cdc =&gt;
	/// {
	///     cdc.UseMongoDB("mongodb://localhost:27017", "order-cdc")
	///        .EnableBackgroundProcessing();
	/// });
	/// </code>
	/// </example>
	public static ICdcBuilder UseMongoDB(
		this ICdcBuilder builder,
		string connectionString,
		string processorId,
		Action<MongoDbCdcOptions>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
		ArgumentException.ThrowIfNullOrWhiteSpace(processorId);

		_ = builder.Services.AddMongoDbCdc(connectionString, processorId, configure);

		return builder;
	}

	/// <summary>
	/// Configures the CDC processor to use MongoDB Change Streams with a state store.
	/// </summary>
	/// <param name="builder">The CDC builder.</param>
	/// <param name="configureCdc">Action to configure MongoDB CDC options.</param>
	/// <param name="configureStateStore">Action to configure MongoDB CDC state store options.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="builder"/> or <paramref name="configureCdc"/> is null.
	/// </exception>
	/// <example>
	/// <code>
	/// services.AddCdcProcessor(cdc =&gt;
	/// {
	///     cdc.UseMongoDB(
	///         cdc =&gt;
	///         {
	///             cdc.Connection.ConnectionString = "mongodb://localhost:27017";
	///             cdc.ProcessorId = "order-cdc";
	///         },
	///         state =&gt;
	///         {
	///             state.DatabaseName = "mydb";
	///             state.CollectionName = "cdc_state";
	///         });
	/// });
	/// </code>
	/// </example>
	public static ICdcBuilder UseMongoDB(
		this ICdcBuilder builder,
		Action<MongoDbCdcOptions> configureCdc,
		Action<MongoDbCdcStateStoreOptions> configureStateStore)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configureCdc);
		ArgumentNullException.ThrowIfNull(configureStateStore);

		_ = builder.Services.AddMongoDbCdc(configureCdc);
		_ = builder.Services.AddMongoDbCdcStateStore(configureStateStore);

		return builder;
	}

	/// <summary>
	/// Configures the CDC processor to use MongoDB Change Streams with fluent builder configuration.
	/// </summary>
	/// <param name="builder">The CDC builder.</param>
	/// <param name="connectionString">The MongoDB connection string for the source.</param>
	/// <param name="configure">Action to configure MongoDB CDC settings via the fluent builder.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="builder"/> is null.
	/// </exception>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="connectionString"/> is null or whitespace.
	/// </exception>
	/// <remarks>
	/// <para>
	/// This overload provides the fluent builder pattern with <see cref="IMongoDbCdcBuilder.WithStateStore(string)"/>
	/// support for configuring a separate MongoDB connection for state persistence.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// services.AddCdcProcessor(cdc =&gt;
	/// {
	///     cdc.UseMongoDB("mongodb://source:27017", mongo =&gt;
	///     {
	///         mongo.DatabaseName("orders-db")
	///              .CollectionNames("orders", "order_items")
	///              .ProcessorId("order-cdc")
	///              .WithStateStore("mongodb://state:27017", state =&gt;
	///              {
	///                  state.SchemaName("cdc-state-db")   // maps to DatabaseName
	///                       .TableName("checkpoints");     // maps to CollectionName
	///              });
	///     });
	/// });
	/// </code>
	/// </example>
	public static ICdcBuilder UseMongoDB(
		this ICdcBuilder builder,
		string connectionString,
		Action<IMongoDbCdcBuilder>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

		// Create and configure MongoDB options
		var mongoOptions = new MongoDbCdcOptions();
		mongoOptions.Connection.ConnectionString = connectionString;
		var mongoBuilder = new MongoDbCdcBuilder(mongoOptions);
		configure?.Invoke(mongoBuilder);

		// Register source CDC options
		_ = builder.Services.AddMongoDbCdc(opt =>
		{
			opt.Connection.ConnectionString = mongoOptions.Connection.ConnectionString;
			opt.DatabaseName = mongoOptions.DatabaseName;
			opt.CollectionNames = mongoOptions.CollectionNames;
			opt.ProcessorId = mongoOptions.ProcessorId;
			opt.BatchSize = mongoOptions.BatchSize;
			opt.ReconnectInterval = mongoOptions.ReconnectInterval;
		});

		// Register source BindConfiguration if set
		if (mongoBuilder.SourceBindConfigurationPath is not null)
		{
			builder.Services.AddOptions<MongoDbCdcOptions>()
				.BindConfiguration(mongoBuilder.SourceBindConfigurationPath)
				.ValidateDataAnnotations()
				.ValidateOnStart();
		}

		// Configure state store if WithStateStore was called
		if (mongoBuilder.StateConnectionString is not null || mongoBuilder.StateClientFactory is not null)
		{
			var stateStoreOptions = new MongoDbCdcStateStoreOptions();

			// Apply state store configure callback
			string? stateStoreBindConfigPath = null;
			if (mongoBuilder.StateStoreConfigure is not null)
			{
				var stateBuilder = new MongoDbCdcStateStoreBuilder(stateStoreOptions);
				mongoBuilder.StateStoreConfigure(stateBuilder);
				stateStoreBindConfigPath = stateBuilder.BindConfigurationPath;
			}

			if (mongoBuilder.StateConnectionString is not null)
			{
				_ = builder.Services.AddMongoDbCdcStateStore(mongoBuilder.StateConnectionString, opt =>
				{
					opt.DatabaseName = stateStoreOptions.DatabaseName;
					opt.CollectionName = stateStoreOptions.CollectionName;
				});
			}
			else
			{
				_ = builder.Services.AddMongoDbCdcStateStore(opt =>
				{
					opt.DatabaseName = stateStoreOptions.DatabaseName;
					opt.CollectionName = stateStoreOptions.CollectionName;
				});
			}

			// Register state store BindConfiguration if set
			if (stateStoreBindConfigPath is not null)
			{
				builder.Services.AddOptions<MongoDbCdcStateStoreOptions>()
					.BindConfiguration(stateStoreBindConfigPath)
					.ValidateDataAnnotations()
					.ValidateOnStart();
			}
		}

		return builder;
	}
}
