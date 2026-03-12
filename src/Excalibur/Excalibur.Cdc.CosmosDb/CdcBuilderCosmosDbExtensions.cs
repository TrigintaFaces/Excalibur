// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

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
	/// <summary>
	/// Configures the CDC processor to use Azure Cosmos DB with the change feed.
	/// </summary>
	/// <param name="builder">The CDC builder.</param>
	/// <param name="configure">Action to configure CosmosDB CDC options.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="builder"/> or <paramref name="configure"/> is null.
	/// </exception>
	/// <remarks>
	/// <para>
	/// Requires <c>CosmosClient</c> to be registered in the service collection.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// services.AddCdcProcessor(cdc =&gt;
	/// {
	///     cdc.UseCosmosDb(options =&gt;
	///     {
	///         options.DatabaseName = "mydb";
	///         options.ContainerName = "orders";
	///         options.LeaseContainerName = "leases";
	///     })
	///     .EnableBackgroundProcessing();
	/// });
	/// </code>
	/// </example>
	public static ICdcBuilder UseCosmosDb(
		this ICdcBuilder builder,
		Action<CosmosDbCdcOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		_ = builder.Services.AddCosmosDbCdc(configure);

		return builder;
	}

	/// <summary>
	/// Configures the CDC processor to use Azure Cosmos DB with a state store.
	/// </summary>
	/// <param name="builder">The CDC builder.</param>
	/// <param name="configureCdc">Action to configure CosmosDB CDC options.</param>
	/// <param name="configureStateStore">Action to configure CosmosDB CDC state store options.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="builder"/> or <paramref name="configureCdc"/> is null.
	/// </exception>
	/// <remarks>
	/// <para>
	/// This overload registers both the CDC processor and a CosmosDB-backed state store
	/// for tracking change feed positions.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// services.AddCdcProcessor(cdc =&gt;
	/// {
	///     cdc.UseCosmosDb(
	///         cdc =&gt;
	///         {
	///             cdc.DatabaseName = "mydb";
	///             cdc.ContainerName = "orders";
	///         },
	///         state =&gt;
	///         {
	///             state.DatabaseName = "mydb";
	///             state.ContainerName = "cdc-state";
	///         });
	/// });
	/// </code>
	/// </example>
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
}
