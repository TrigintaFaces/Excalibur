// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Cdc.DynamoDb;

/// <summary>
/// Extension methods for configuring DynamoDB CDC provider on <see cref="ICdcBuilder"/>.
/// </summary>
/// <remarks>
/// <para>
/// These extensions provide fluent provider selection by adding
/// provider-specific configuration to the core <see cref="ICdcBuilder"/> interface.
/// </para>
/// </remarks>
public static class CdcBuilderDynamoDbExtensions
{
	/// <summary>
	/// Configures the CDC processor to use Amazon DynamoDB Streams.
	/// </summary>
	/// <param name="builder">The CDC builder.</param>
	/// <param name="configure">Action to configure DynamoDB CDC options.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="builder"/> or <paramref name="configure"/> is null.
	/// </exception>
	/// <remarks>
	/// <para>
	/// Requires <c>IAmazonDynamoDB</c> and <c>IAmazonDynamoDBStreams</c> clients
	/// to be registered in the service collection.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// services.AddCdcProcessor(cdc =&gt;
	/// {
	///     cdc.UseDynamoDb(options =&gt;
	///     {
	///         options.TableName = "Orders";
	///         options.ProcessorId = "order-cdc";
	///     })
	///     .EnableBackgroundProcessing();
	/// });
	/// </code>
	/// </example>
	public static ICdcBuilder UseDynamoDb(
		this ICdcBuilder builder,
		Action<DynamoDbCdcOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		_ = builder.Services.AddDynamoDbCdc(configure);

		return builder;
	}

	/// <summary>
	/// Configures the CDC processor to use Amazon DynamoDB Streams with a state store.
	/// </summary>
	/// <param name="builder">The CDC builder.</param>
	/// <param name="configureCdc">Action to configure DynamoDB CDC options.</param>
	/// <param name="configureStateStore">Action to configure DynamoDB CDC state store options.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="builder"/> or <paramref name="configureCdc"/> is null.
	/// </exception>
	/// <remarks>
	/// <para>
	/// This overload registers both the CDC processor and a DynamoDB-backed state store
	/// for tracking stream positions.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// services.AddCdcProcessor(cdc =&gt;
	/// {
	///     cdc.UseDynamoDb(
	///         cdc =&gt;
	///         {
	///             cdc.TableName = "Orders";
	///             cdc.ProcessorId = "order-cdc";
	///         },
	///         state =&gt;
	///         {
	///             state.TableName = "CdcState";
	///         });
	/// });
	/// </code>
	/// </example>
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
}
