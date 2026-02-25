// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Data.Abstractions.Persistence;

/// <summary>
/// Defines the contract for persistence providers that manage DataRequest execution and lifecycle.
/// Providers focus on connection management and infrastructure concerns rather than specific CRUD operations.
/// </summary>
/// <remarks>
/// <para>
/// This is the core interface with ≤5 members. Optional capabilities are accessed via
/// <see cref="GetService"/>:
/// </para>
/// <list type="bullet">
/// <item><description><see cref="IPersistenceProviderHealth"/> — health checks, metrics, pool stats</description></item>
/// <item><description><see cref="IPersistenceProviderTransaction"/> — transactions, retry policy, connection string</description></item>
/// </list>
/// </remarks>
public interface IPersistenceProvider : IAsyncDisposable, IDisposable
{
	/// <summary>
	/// Gets the name of the persistence provider.
	/// </summary>
	/// <value>
	/// The name of the persistence provider.
	/// </value>
	string Name { get; }

	/// <summary>
	/// Gets the type of the persistence provider (e.g., "SQL", "Document", "KeyValue").
	/// </summary>
	/// <value>
	/// The type of the persistence provider (e.g., "SQL", "Document", "KeyValue").
	/// </value>
	string ProviderType { get; }

	/// <summary>
	/// Executes a DataRequest with built-in retry logic and resilience capabilities.
	/// </summary>
	/// <typeparam name="TConnection"> The type of the database connection. </typeparam>
	/// <typeparam name="TResult"> The type of the result. </typeparam>
	/// <param name="request"> The data request to execute. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> The result of the data request execution. </returns>
	Task<TResult> ExecuteAsync<TConnection, TResult>(
		IDataRequest<TConnection, TResult> request,
		CancellationToken cancellationToken)
		where TConnection : IDisposable;

	/// <summary>
	/// Initializes the provider with the specified options.
	/// </summary>
	/// <param name="options"> The persistence options. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A task representing the asynchronous operation. </returns>
	Task InitializeAsync(IPersistenceOptions options, CancellationToken cancellationToken);

	/// <summary>
	/// Gets an implementation-specific service. Use to access optional capabilities
	/// such as <see cref="IPersistenceProviderHealth"/>, <see cref="IPersistenceProviderTransaction"/>,
	/// or provider-specific features.
	/// </summary>
	/// <param name="serviceType">The type of the requested service.</param>
	/// <returns>The service instance, or <see langword="null"/> if not supported.</returns>
	object? GetService(Type serviceType) => null;
}
