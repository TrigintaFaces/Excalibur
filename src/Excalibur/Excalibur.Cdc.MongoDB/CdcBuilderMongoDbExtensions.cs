// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

using MongoDB.Driver;

namespace Excalibur.Cdc.MongoDB;

/// <summary>
/// Extension methods for configuring MongoDB CDC on <see cref="ICdcBuilder"/>.
/// </summary>
public static class CdcBuilderMongoDbExtensions
{
	private const string BuilderManagedConnectionSentinel = "mongodb://builder-managed-client";

	/// <summary>
	/// Configures the CDC processor to use MongoDB Change Streams with fluent builder configuration.
	/// </summary>
	/// <param name="builder">The CDC builder.</param>
	/// <param name="configure">Action to configure MongoDB CDC settings via the fluent builder.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="builder"/> or <paramref name="configure"/> is null.
	/// </exception>
	/// <example>
	/// <code>
	/// services.AddExcaliburCdc(cdc =&gt;
	/// {
	///     cdc.UseMongoDB(mongo =&gt;
	///     {
	///         mongo.ConnectionString("mongodb://localhost:27017")
	///              .DatabaseName("myapp")
	///              .WithStateStore(state =&gt;
	///              {
	///                  state.DatabaseName("myapp")
	///                       .CollectionName("cdc_state");
	///              });
	///     });
	/// });
	/// </code>
	/// </example>
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

		var mongoOptions = new MongoDbCdcOptions();
		var mongoBuilder = new MongoDbCdcBuilder(mongoOptions);
		configure(mongoBuilder);

		RegisterCdcOptions(builder.Services, mongoBuilder, mongoOptions);
		RegisterStateStore(builder.Services, mongoBuilder);

		return builder;
	}

	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options validation/binding uses reflection by design.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design.")]
	private static void RegisterCdcOptions(
		IServiceCollection services,
		MongoDbCdcBuilder mongoBuilder,
		MongoDbCdcOptions mongoOptions)
	{
		_ = services.Configure<MongoDbCdcOptions>(opt =>
		{
			opt.Connection.ConnectionString = mongoOptions.Connection.ConnectionString;
			opt.DatabaseName = mongoOptions.DatabaseName;
			opt.CollectionNames = mongoOptions.CollectionNames;
			opt.ProcessorId = mongoOptions.ProcessorId;
			opt.BatchSize = mongoOptions.BatchSize;
			opt.ReconnectInterval = mongoOptions.ReconnectInterval;
		});

		if (mongoBuilder.SourceBindConfigurationPath is not null)
		{
			services.AddOptions<MongoDbCdcOptions>()
				.BindConfiguration(mongoBuilder.SourceBindConfigurationPath)
				.ValidateOnStart();

			if (!string.IsNullOrWhiteSpace(mongoOptions.Connection.ConnectionString))
			{
				var explicitConnectionString = mongoOptions.Connection.ConnectionString;
				_ = services.PostConfigure<MongoDbCdcOptions>(opt =>
				{
					opt.Connection.ConnectionString = explicitConnectionString;
				});
			}
		}

		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<MongoDbCdcOptions>, MongoDbCdcOptionsValidator>());
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<MongoDbCdcRecoveryOptions>, MongoDbCdcRecoveryOptionsValidator>());
		services.AddOptions<MongoDbCdcOptions>().ValidateOnStart();
		services.TryAddSingleton<IMongoDbCdcProcessor, MongoDbCdcProcessor>();
	}

	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options validation/binding uses reflection by design.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design.")]
	private static void RegisterStateStore(
		IServiceCollection services,
		MongoDbCdcBuilder mongoBuilder)
	{
		if (mongoBuilder.StateStoreConfigure is null)
		{
			return;
		}

		var stateStoreOptions = new MongoDbCdcStateStoreOptions();
		var stateBuilder = new MongoDbCdcStateStoreBuilder(stateStoreOptions);
		mongoBuilder.StateStoreConfigure(stateBuilder);

		_ = services.Configure<MongoDbCdcStateStoreOptions>(opt =>
		{
			opt.CollectionName = stateStoreOptions.CollectionName;
		});

		if (stateBuilder.BindConfigurationPath is not null)
		{
			services.AddOptions<MongoDbCdcStateStoreOptions>()
				.BindConfiguration(stateBuilder.BindConfigurationPath)
				.ValidateOnStart();
		}

		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<MongoDbCdcStateStoreOptions>, MongoDbCdcStateStoreOptionsValidator>());
		services.AddOptions<MongoDbCdcStateStoreOptions>().ValidateOnStart();

		services.TryAddSingleton<IMongoDbCdcStateStore>(sp => new MongoDbCdcStateStore(
			sp.GetRequiredService<IMongoClient>(),
			sp.GetRequiredService<IOptions<MongoDbCdcStateStoreOptions>>()));
	}
}
