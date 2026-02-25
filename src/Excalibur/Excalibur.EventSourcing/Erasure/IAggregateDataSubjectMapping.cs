// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.EventSourcing.Erasure;

/// <summary>
/// Maps data subject identifiers to the aggregate instances that contain their personal data.
/// </summary>
/// <remarks>
/// <para>
/// Consumers MUST implement this interface to support GDPR erasure of event-sourced aggregates.
/// The implementation is application-specific because only the application knows which aggregates
/// belong to which data subjects.
/// </para>
/// <para>
/// Common strategies:
/// <list type="bullet">
/// <item>Query a lookup table mapping user IDs to aggregate IDs</item>
/// <item>Use naming conventions (e.g., aggregate ID equals user ID)</item>
/// <item>Search an index or projection that tracks data subject ownership</item>
/// </list>
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public class OrderAggregateMapping : IAggregateDataSubjectMapping
/// {
///     private readonly IOrderRepository _repository;
///
///     public async Task&lt;IReadOnlyList&lt;AggregateReference&gt;&gt; GetAggregatesForDataSubjectAsync(
///         string dataSubjectIdHash, string? tenantId, CancellationToken cancellationToken)
///     {
///         var orderIds = await _repository.GetOrderIdsByUserHashAsync(dataSubjectIdHash, cancellationToken);
///         return orderIds.Select(id =&gt; new AggregateReference(id, "Order")).ToList();
///     }
/// }
/// </code>
/// </example>
public interface IAggregateDataSubjectMapping
{
	/// <summary>
	/// Resolves all aggregate references associated with a data subject.
	/// </summary>
	/// <param name="dataSubjectIdHash">The SHA-256 hash of the data subject identifier.</param>
	/// <param name="tenantId">The tenant ID for multi-tenant scenarios, or null.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A list of aggregate references that contain data for the specified data subject.</returns>
	Task<IReadOnlyList<AggregateReference>> GetAggregatesForDataSubjectAsync(
		string dataSubjectIdHash,
		string? tenantId,
		CancellationToken cancellationToken);
}

/// <summary>
/// Identifies an aggregate instance by its ID and type.
/// </summary>
/// <param name="AggregateId">The aggregate identifier.</param>
/// <param name="AggregateType">The aggregate type name (e.g., "Order", "Customer").</param>
public sealed record AggregateReference(string AggregateId, string AggregateType);
