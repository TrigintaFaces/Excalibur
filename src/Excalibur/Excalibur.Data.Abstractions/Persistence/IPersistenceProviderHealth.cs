// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Data.Persistence;

/// <summary>
/// Provides health and diagnostics capabilities for persistence providers.
/// Obtain via <see cref="IPersistenceProvider.GetService"/> with
/// <c>typeof(IPersistenceProviderHealth)</c>.
/// </summary>
/// <remarks>
/// <para>
/// This interface follows the ISP pattern — consumers that only need to execute
/// data requests use <see cref="IPersistenceProvider"/> directly.
/// Health checks and monitoring tools use this sub-interface.
/// </para>
/// <para>
/// Reference: <c>Microsoft.Extensions.Diagnostics.HealthChecks.IHealthCheck</c> — single method;
/// this interface adds provider-specific diagnostics beyond simple pass/fail.
/// </para>
/// </remarks>
public interface IPersistenceProviderHealth
{
	/// <summary>
	/// Gets a value indicating whether the provider is ready to serve requests.
	/// </summary>
	/// <value>
	/// <see langword="true"/> while the provider is ready to serve and has not been disposed;
	/// otherwise, <see langword="false"/>.
	/// </value>
	/// <remarks>
	/// <para>
	/// Readiness may be established either at construction — when the provider is handed an already
	/// connected dependency, such as a client it does not own — or by <c>InitializeAsync</c> for
	/// providers that build their own connection from configuration. Both are supported; which one
	/// applies is a property of the constructor you chose, not of the interface.
	/// </para>
	/// <para>
	/// Implementations must report <see langword="false"/> once disposed, and may report
	/// <see langword="false"/> while otherwise ready if the underlying connection has dropped. This is
	/// a cheap, non-blocking status flag rather than a probe: reading it must not perform I/O.
	/// </para>
	/// </remarks>
	bool IsAvailable { get; }

	/// <summary>
	/// Tests the connection to the persistence provider.
	/// </summary>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> <see langword="true"/> if the connection is successful; otherwise, <see langword="false"/>. </returns>
	Task<bool> TestConnectionAsync(CancellationToken cancellationToken);

	/// <summary>
	/// Gets provider-specific health and performance metrics.
	/// </summary>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A dictionary of metric names and values. </returns>
	/// <remarks>
	/// <para>
	/// The returned dictionary MUST contain two identifying entries, and they carry different
	/// dimensions:
	/// </para>
	/// <list type="bullet">
	/// <item><description>
	/// <c>"Provider"</c> is the <b>engine</b> identity — a fixed literal, constant per implementation and
	/// identical for every instance of it (for example <c>"SqlServer"</c> for every SQL Server instance).
	/// It MUST NOT vary with configuration, and in particular MUST NOT be set from
	/// <see cref="IPersistenceProvider.Name"/>, which is the instance dimension and would make the engine
	/// unreadable.
	/// </description></item>
	/// <item><description>
	/// <c>"Name"</c> is the <b>instance</b> identity — the value of
	/// <see cref="IPersistenceProvider.Name"/>, which distinguishes two separately configured instances of
	/// the same engine.
	/// </description></item>
	/// </list>
	/// <para>
	/// Together they let a consumer aggregating across a mixed deployment group by engine and still
	/// attribute a reading to the instance that produced it. Keys are compared with an ordinal comparer,
	/// so the casing is part of the contract -- <c>"provider"</c> is a different key and does not satisfy
	/// it. Every other key is provider-specific; the convention is PascalCase. The conformance suite
	/// verifies the required entries.
	/// </para>
	/// </remarks>
	Task<IDictionary<string, object>> GetMetricsAsync(CancellationToken cancellationToken);
}
