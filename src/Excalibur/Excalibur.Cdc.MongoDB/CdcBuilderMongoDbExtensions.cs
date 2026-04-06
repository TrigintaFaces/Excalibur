// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Cdc.MongoDB;

/// <inheritdoc cref="CdcBuilderMongoDbExtensions"/>
public static class CdcBuilderMongoDbExtensions
{
	/// <inheritdoc cref="UseMongoDB(ICdcBuilder, Action{MongoDbCdcOptions})"/>
	public static ICdcBuilder UseMongoDB(
		this ICdcBuilder builder,
		Action<MongoDbCdcOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		_ = builder.Services.AddMongoDbCdc(configure);

		return builder;
	}

	/// <inheritdoc cref="UseMongoDB(ICdcBuilder, Action{MongoDbCdcOptions}, Action{MongoDbCdcStateStoreOptions})"/>
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
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options validation/binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
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
					.ValidateOnStart();
			}
		}

		return builder;
	}
}
