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
	/// <param name="configure">Action to configure MongoDB CDC settings via the fluent builder.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="builder"/> or <paramref name="configure"/> is null.
	/// </exception>
	/// <remarks>
	/// <para>
	/// This overload provides the fluent builder pattern with
	/// <see cref="IMongoDbCdcBuilder.WithStateStore(Action{ICdcStateStoreBuilder})"/>
	/// support for configuring a separate MongoDB connection for state persistence.
	/// Use <see cref="IMongoDbCdcBuilder.ConnectionString(string)"/> to set the source connection string.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// services.AddCdcProcessor(cdc =&gt;
	/// {
	///     cdc.UseMongoDB(mongo =&gt;
	///     {
	///         mongo.ConnectionString("mongodb://source:27017")
	///              .DatabaseName("orders-db")
	///              .CollectionNames("orders", "order_items")
	///              .ProcessorId("order-cdc")
	///              .WithStateStore(state =&gt;
	///              {
	///                  state.ConnectionString("mongodb://state:27017")
	///                       .SchemaName("cdc-state-db")   // maps to DatabaseName
	///                       .TableName("checkpoints");     // maps to CollectionName
	///              });
	///     });
	/// });
	/// </code>
	/// </example>
	public static ICdcBuilder UseMongoDB(
		this ICdcBuilder builder,
		Action<IMongoDbCdcBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		// Create and configure MongoDB options
		var mongoOptions = new MongoDbCdcOptions();
		var mongoBuilder = new MongoDbCdcBuilder(mongoOptions);
		configure(mongoBuilder);

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

			// When ConnectionString() was explicitly called alongside BindConfiguration,
			// re-apply via PostConfigure so the explicit value takes precedence over config.
			if (!string.IsNullOrWhiteSpace(mongoOptions.Connection.ConnectionString))
			{
				var explicitConnectionString = mongoOptions.Connection.ConnectionString;
				_ = builder.Services.PostConfigure<MongoDbCdcOptions>(opt =>
				{
					opt.Connection.ConnectionString = explicitConnectionString;
				});
			}
		}

		// Configure state store if WithStateStore was called
		if (mongoBuilder.StateStoreConfigure is not null)
		{
			var stateStoreOptions = new MongoDbCdcStateStoreOptions();
			var stateBuilder = new MongoDbCdcStateStoreBuilder(stateStoreOptions);
			mongoBuilder.StateStoreConfigure(stateBuilder);

			_ = builder.Services.AddMongoDbCdcStateStore(opt =>
			{
				opt.DatabaseName = stateStoreOptions.DatabaseName;
				opt.CollectionName = stateStoreOptions.CollectionName;
			});

			// Register state store BindConfiguration if set
			if (stateBuilder.BindConfigurationPath is not null)
			{
				builder.Services.AddOptions<MongoDbCdcStateStoreOptions>()
					.BindConfiguration(stateBuilder.BindConfigurationPath)
					.ValidateDataAnnotations()
					.ValidateOnStart();
			}
		}

		return builder;
	}
}
