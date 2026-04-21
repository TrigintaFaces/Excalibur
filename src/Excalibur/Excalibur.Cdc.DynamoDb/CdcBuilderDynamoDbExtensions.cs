// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Cdc.DynamoDb;

/// <inheritdoc cref="CdcBuilderDynamoDbExtensions"/>
public static class CdcBuilderDynamoDbExtensions
{
	/// <inheritdoc cref="UseDynamoDb(ICdcBuilder, Action{DynamoDbCdcOptions})"/>
	public static ICdcBuilder UseDynamoDb(
		this ICdcBuilder builder,
		Action<DynamoDbCdcOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		_ = builder.Services.AddDynamoDbCdc(configure);

		return builder;
	}

	/// <inheritdoc cref="UseDynamoDb(ICdcBuilder, Action{DynamoDbCdcOptions}, Action{DynamoDbCdcStateStoreOptions})"/>
	public static ICdcBuilder UseDynamoDb(
		this ICdcBuilder builder,
		Action<DynamoDbCdcOptions> configureCdc,
		Action<DynamoDbCdcStateStoreOptions> configureStateStore)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configureCdc);
		ArgumentNullException.ThrowIfNull(configureStateStore);

		_ = builder.Services.AddDynamoDbCdc(configureCdc);
		_ = builder.Services.AddDynamoDbCdcStateStore(configureStateStore);

		return builder;
	}

	/// <summary>
	/// Configures the CDC processor to use Amazon DynamoDB Streams with fluent builder configuration.
	/// </summary>
	/// <param name="builder">The CDC builder.</param>
	/// <param name="configure">Action to configure DynamoDB CDC settings via the fluent builder.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="builder"/> is null.
	/// </exception>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options validation/binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	public static ICdcBuilder UseDynamoDb(
		this ICdcBuilder builder,
		Action<IDynamoDbCdcBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		// Create and configure DynamoDB options
		var dynamoOptions = new DynamoDbCdcOptions();
		var dynamoBuilder = new DynamoDbCdcBuilder(dynamoOptions);
		configure(dynamoBuilder);

		// Register source CDC options
		_ = builder.Services.AddDynamoDbCdc(opt =>
		{
			opt.TableName = dynamoOptions.TableName;
			opt.StreamArn = dynamoOptions.StreamArn;
			opt.ProcessorName = dynamoOptions.ProcessorName;
			opt.MaxBatchSize = dynamoOptions.MaxBatchSize;
			opt.PollInterval = dynamoOptions.PollInterval;
		});

		// Register source BindConfiguration if set
		if (dynamoBuilder.SourceBindConfigurationPath is not null)
		{
			builder.Services.AddOptions<DynamoDbCdcOptions>()
				.BindConfiguration(dynamoBuilder.SourceBindConfigurationPath)
				.ValidateOnStart();
		}

		// Configure state store if WithStateStore was called
		if (dynamoBuilder.StateClientFactory is not null)
		{
			var stateStoreOptions = new DynamoDbCdcStateStoreOptions();

			// Apply state store configure callback
			string? stateStoreBindConfigPath = null;
			if (dynamoBuilder.StateStoreConfigure is not null)
			{
				var stateBuilder = new DynamoDbCdcStateStoreBuilder(stateStoreOptions);
				dynamoBuilder.StateStoreConfigure(stateBuilder);
				stateStoreBindConfigPath = stateBuilder.BindConfigurationPath;
			}

			_ = builder.Services.AddDynamoDbCdcStateStore(opt =>
			{
				opt.TableName = stateStoreOptions.TableName;
			});

			// Register state store BindConfiguration if set
			if (stateStoreBindConfigPath is not null)
			{
				builder.Services.AddOptions<DynamoDbCdcStateStoreOptions>()
					.BindConfiguration(stateStoreBindConfigPath)
					.ValidateOnStart();
			}
		}

		return builder;
	}
}
