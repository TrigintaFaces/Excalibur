// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Data.Persistence;

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
/// <item><description><see cref="IPersistenceProviderTransaction"/> — ambient transaction scopes and transactional request execution</description></item>
/// <item><description><see cref="IPersistenceProviderConnection"/> — connection string and retry policy</description></item>
/// </list>
/// </remarks>
public interface IPersistenceProvider : IAsyncDisposable, IDisposable
{
	/// <summary>
	/// Gets the identity of this configured provider instance.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This is the instance dimension, not the engine dimension. Two instances of the same engine
	/// configured separately report different names, which is what lets a consumer tell them apart in
	/// health output, logs and metrics. The engine an instance runs on is reported separately, under the
	/// <c>Provider</c> key of <see cref="IPersistenceProviderHealth.GetMetricsAsync"/>.
	/// </para>
	/// <para>
	/// The value is non-<see langword="null"/> and non-empty, is established at construction, and does
	/// not change for the lifetime of the instance — it is readable before <see cref="InitializeAsync"/>
	/// and after disposal. When no name is configured it falls back to a stable per-implementation
	/// default.
	/// </para>
	/// <para>
	/// Known gap: a provider instance shared across several configuration entries reports a single
	/// identity, because the shared instance is one object.
	/// </para>
	/// </remarks>
	/// <value>
	/// The identity of this configured provider instance.
	/// </value>
	string Name { get; }

	/// <summary>
	/// Gets the family of store this provider talks to, such as <c>"SQL"</c>, <c>"Document"</c> or
	/// <c>"KeyValue"</c>.
	/// </summary>
	/// <remarks>
	/// This is the family, not the engine and not the instance: several engines share a family, and it is
	/// what a consumer branches on when the shape of the store matters more than which product it is. It
	/// is a fixed literal, constant per implementation, and MUST NOT vary with configuration. For the
	/// engine, read the <c>Provider</c> key of
	/// <see cref="IPersistenceProviderHealth.GetMetricsAsync"/>; for the configured instance, read
	/// <see cref="Name"/>.
	/// </remarks>
	/// <value>
	/// The family of store this provider talks to.
	/// </value>
	string ProviderType { get; }

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
	/// <see cref="IPersistenceProviderConnection"/>, or provider-specific features.
	/// </summary>
	/// <param name="serviceType">The type of the requested service.</param>
	/// <returns>The service instance, or <see langword="null"/> if not supported.</returns>
	object? GetService(Type serviceType) => null;
}
