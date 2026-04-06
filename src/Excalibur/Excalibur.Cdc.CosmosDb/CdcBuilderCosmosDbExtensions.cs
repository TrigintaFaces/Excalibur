// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Cdc.CosmosDb;

/// <summary>
/// Extension methods for configuring CosmosDB CDC provider on <see cref="ICdcBuilder"/>.
/// </summary>
/// <remarks>
/// <para>
/// These extensions provide fluent provider selection by adding
/// provider-specific configuration to the core <see cref="ICdcBuilder"/> interface.
/// </para>
/// </remarks>
public static class CdcBuilderCosmosDbExtensions
{
	/// <inheritdoc cref="UseCosmosDb(ICdcBuilder, Action{CosmosDbCdcOptions})"/>
	public static ICdcBuilder UseCosmosDb(
		this ICdcBuilder builder,
		Action<CosmosDbCdcOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		_ = builder.Services.AddCosmosDbCdc(configure);

		return builder;
	}

	/// <inheritdoc cref="UseCosmosDb(ICdcBuilder, Action{CosmosDbCdcOptions}, Action{CosmosDbCdcStateStoreOptions})"/>
	public static ICdcBuilder UseCosmosDb(
		this ICdcBuilder builder,
		Action<CosmosDbCdcOptions> configureCdc,
		Action<CosmosDbCdcStateStoreOptions> configureStateStore)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configureCdc);
		ArgumentNullException.ThrowIfNull(configureStateStore);

		_ = builder.Services.AddCosmosDbCdc(configureCdc);
		_ = builder.Services.AddCosmosDbCdcStateStore(configureStateStore);

		return builder;
	}

	/// <summary>
	/// Configures the CDC processor to use Azure Cosmos DB with fluent builder configuration.
	/// </summary>
	/// <param name="builder">The CDC builder.</param>
	/// <param name="configure">Action to configure CosmosDB CDC settings via the fluent builder.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="builder"/> or <paramref name="configure"/> is null.
	/// </exception>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options validation/binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	public static ICdcBuilder UseCosmosDb(
		this ICdcBuilder builder,
		Action<ICosmosDbCdcBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		// Create and configure CosmosDB options
		var cosmosOptions = new CosmosDbCdcOptions();
		var cosmosBuilder = new CosmosDbCdcBuilder(cosmosOptions);
		configure(cosmosBuilder);

		// Register source CDC options
		_ = builder.Services.AddCosmosDbCdc(opt =>
		{
			opt.ConnectionString = cosmosOptions.ConnectionString;
			opt.DatabaseId = cosmosOptions.DatabaseId;
			opt.ContainerId = cosmosOptions.ContainerId;
			opt.ProcessorName = cosmosOptions.ProcessorName;
			opt.ChangeFeed.MaxBatchSize = cosmosOptions.ChangeFeed.MaxBatchSize;
			opt.ChangeFeed.PollInterval = cosmosOptions.ChangeFeed.PollInterval;
			opt.ChangeFeed.MaxWaitTime = cosmosOptions.ChangeFeed.MaxWaitTime;
		});

		// Register source BindConfiguration if set
		if (cosmosBuilder.SourceBindConfigurationPath is not null)
		{
			builder.Services.AddOptions<CosmosDbCdcOptions>()
				.BindConfiguration(cosmosBuilder.SourceBindConfigurationPath)
				.ValidateOnStart();

			// When ConnectionString() was explicitly called alongside BindConfiguration,
			// re-apply via PostConfigure so the explicit value takes precedence over config.
			if (!string.IsNullOrWhiteSpace(cosmosOptions.ConnectionString))
			{
				var explicitConnectionString = cosmosOptions.ConnectionString;
				_ = builder.Services.PostConfigure<CosmosDbCdcOptions>(opt =>
				{
					opt.ConnectionString = explicitConnectionString;
				});
			}
		}

		// Configure state store if WithStateStore was called
		if (cosmosBuilder.StateStoreConfigure is not null)
		{
			var stateStoreOptions = new CosmosDbCdcStateStoreOptions();
			var stateBuilder = new CosmosDbCdcStateStoreBuilder(stateStoreOptions);
			cosmosBuilder.StateStoreConfigure(stateBuilder);

			_ = builder.Services.AddCosmosDbCdcStateStore(opt =>
			{
				if (!string.IsNullOrWhiteSpace(stateStoreOptions.ConnectionString))
				{
					opt.ConnectionString = stateStoreOptions.ConnectionString;
				}

				opt.DatabaseId = stateStoreOptions.DatabaseId;
				opt.ContainerId = stateStoreOptions.ContainerId;
			});

			// Register state store BindConfiguration if set
			if (stateBuilder.BindConfigurationPath is not null)
			{
				builder.Services.AddOptions<CosmosDbCdcStateStoreOptions>()
					.BindConfiguration(stateBuilder.BindConfigurationPath)
					.ValidateOnStart();
			}
		}

		return builder;
	}
}
