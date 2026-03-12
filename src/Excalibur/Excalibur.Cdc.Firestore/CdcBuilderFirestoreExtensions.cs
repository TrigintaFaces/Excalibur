// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Cdc.Firestore;

/// <summary>
/// Extension methods for configuring Firestore CDC provider on <see cref="ICdcBuilder"/>.
/// </summary>
/// <remarks>
/// <para>
/// These extensions provide fluent provider selection by adding
/// provider-specific configuration to the core <see cref="ICdcBuilder"/> interface.
/// </para>
/// </remarks>
public static class CdcBuilderFirestoreExtensions
{
	/// <summary>
	/// Configures the CDC processor to use Google Cloud Firestore.
	/// </summary>
	/// <param name="builder">The CDC builder.</param>
	/// <param name="configure">Action to configure Firestore CDC options.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="builder"/> or <paramref name="configure"/> is null.
	/// </exception>
	/// <remarks>
	/// <para>
	/// Requires <see cref="Google.Cloud.Firestore.FirestoreDb"/> to be registered in the service collection.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// services.AddCdcProcessor(cdc =&gt;
	/// {
	///     cdc.UseFirestore(options =&gt;
	///     {
	///         options.ProjectId = "my-project";
	///         options.CollectionPath = "orders";
	///     })
	///     .EnableBackgroundProcessing();
	/// });
	/// </code>
	/// </example>
	public static ICdcBuilder UseFirestore(
		this ICdcBuilder builder,
		Action<FirestoreCdcOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		_ = builder.Services.AddFirestoreCdc(configure);

		return builder;
	}

	/// <summary>
	/// Configures the CDC processor to use Google Cloud Firestore with a state store.
	/// </summary>
	/// <param name="builder">The CDC builder.</param>
	/// <param name="configureCdc">Action to configure Firestore CDC options.</param>
	/// <param name="configureStateStore">Action to configure Firestore CDC state store options.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="builder"/> or <paramref name="configureCdc"/> is null.
	/// </exception>
	/// <example>
	/// <code>
	/// services.AddCdcProcessor(cdc =&gt;
	/// {
	///     cdc.UseFirestore(
	///         cdc =&gt;
	///         {
	///             cdc.ProjectId = "my-project";
	///             cdc.CollectionPath = "orders";
	///         },
	///         state =&gt;
	///         {
	///             state.CollectionName = "cdc-positions";
	///         });
	/// });
	/// </code>
	/// </example>
	public static ICdcBuilder UseFirestore(
		this ICdcBuilder builder,
		Action<FirestoreCdcOptions> configureCdc,
		Action<FirestoreCdcStateStoreOptions> configureStateStore)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configureCdc);
		ArgumentNullException.ThrowIfNull(configureStateStore);

		_ = builder.Services.AddFirestoreCdc(configureCdc);
		_ = builder.Services.AddFirestoreCdcStateStore(configureStateStore);

		return builder;
	}
}
