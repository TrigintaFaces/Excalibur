// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Cdc.Firestore;

/// <summary>
/// Fluent builder interface for configuring Firestore CDC settings.
/// </summary>
/// <remarks>
/// <para>
/// This builder configures Firestore-specific CDC options such as collection path,
/// processor name, batch size, and state store connections.
/// All methods return <c>this</c> for method chaining.
/// </para>
/// </remarks>
public interface IFirestoreCdcBuilder
{
	/// <summary>
	/// Sets the Firestore collection path to watch for changes.
	/// </summary>
	/// <param name="collectionPath">The collection path (e.g., "orders" or "organizations/org1/members").</param>
	/// <returns>The builder for fluent chaining.</returns>
	IFirestoreCdcBuilder CollectionPath(string collectionPath);

	/// <summary>
	/// Sets the unique processor name for this CDC instance.
	/// </summary>
	/// <param name="processorName">The processor name.</param>
	/// <returns>The builder for fluent chaining.</returns>
	IFirestoreCdcBuilder ProcessorName(string processorName);

	/// <summary>
	/// Sets the maximum number of events to process per batch.
	/// </summary>
	/// <param name="maxBatchSize">The maximum batch size.</param>
	/// <returns>The builder for fluent chaining.</returns>
	IFirestoreCdcBuilder MaxBatchSize(int maxBatchSize);

	/// <summary>
	/// Sets the interval between checking for new changes when idle.
	/// </summary>
	/// <param name="interval">The poll interval.</param>
	/// <returns>The builder for fluent chaining.</returns>
	IFirestoreCdcBuilder PollInterval(TimeSpan interval);

	/// <summary>
	/// Configures a separate connection for CDC state persistence.
	/// </summary>
	/// <param name="configure">An action to configure state store settings including project ID and collection.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <remarks>
	/// <para>
	/// Firestore uses project IDs instead of connection strings.
	/// Use <see cref="ICdcStateStoreBuilder.ConnectionString(string)"/> to set the project ID.
	/// When omitted, the source FirestoreDb is used for state persistence.
	/// </para>
	/// </remarks>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="configure"/> is null.
	/// </exception>
	IFirestoreCdcBuilder WithStateStore(Action<ICdcStateStoreBuilder> configure);

	/// <summary>
	/// Binds Firestore CDC source options from an <see cref="Microsoft.Extensions.Configuration.IConfiguration"/> section.
	/// </summary>
	/// <param name="sectionPath">The configuration section path (e.g., "Cdc:Firestore").</param>
	/// <returns>The builder for fluent chaining.</returns>
	IFirestoreCdcBuilder BindConfiguration(string sectionPath);
}
