// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Saga.Queries;

/// <summary>
/// Provides query capabilities for finding saga instances by correlation properties.
/// </summary>
/// <remarks>
/// <para>
/// Use this interface to locate saga instances based on correlation identifiers
/// or arbitrary property values. This is useful for:
/// </para>
/// <list type="bullet">
/// <item><description>Finding sagas related to a specific business entity</description></item>
/// <item><description>Debugging and operational tooling</description></item>
/// <item><description>Building saga management dashboards</description></item>
/// </list>
/// <para>
/// Follows the query pattern from <c>Microsoft.Extensions.Diagnostics.HealthChecks</c>
/// with a minimal interface surface (2 methods).
/// </para>
/// </remarks>
public interface ISagaCorrelationQuery
{
	/// <summary>
	/// Finds saga instances by their correlation identifier.
	/// </summary>
	/// <param name="correlationId">The correlation identifier to search for.</param>
	/// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
	/// <returns>
	/// A read-only list of matching saga query results, or an empty list if none found.
	/// </returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="correlationId"/> is null or empty.
	/// </exception>
	Task<IReadOnlyList<SagaQueryResult>> FindByCorrelationIdAsync(
		string correlationId,
		CancellationToken cancellationToken);

	/// <summary>
	/// Finds saga instances by a named property value.
	/// </summary>
	/// <param name="propertyName">The property name to search by.</param>
	/// <param name="value">The value to match against.</param>
	/// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
	/// <returns>
	/// A read-only list of matching saga query results, or an empty list if none found.
	/// </returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="propertyName"/> is null or empty.
	/// </exception>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="value"/> is null.
	/// </exception>
	Task<IReadOnlyList<SagaQueryResult>> FindByPropertyAsync(
		string propertyName,
		object value,
		CancellationToken cancellationToken);
}
