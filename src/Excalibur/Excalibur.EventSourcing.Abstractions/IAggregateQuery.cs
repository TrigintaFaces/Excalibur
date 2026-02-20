// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Domain.Model;

namespace Excalibur.EventSourcing.Abstractions;

/// <summary>
/// Marker interface for aggregate query objects.
/// </summary>
/// <typeparam name="TAggregate">The aggregate type this query targets.</typeparam>
/// <remarks>
/// <para>
/// Implement this interface for type-safe queries against aggregate repositories.
/// Query objects define their own criteria properties and are executed via
/// <see cref="IEventSourcedRepository{TAggregate, TKey}.QueryAsync{TQuery}"/> or
/// <see cref="IEventSourcedRepository{TAggregate, TKey}.FindAsync{TQuery}"/>.
/// </para>
/// <para>
/// This is a marker interface - no methods are required. Derived queries define
/// their own criteria properties based on the specific query requirements.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Define a query for finding users by status
/// public record GetUsersByStatusQuery(UserStatus Status) : IAggregateQuery&lt;UserAggregate&gt;;
///
/// // Execute the query
/// var activeUsers = await repository.QueryAsync(new GetUsersByStatusQuery(UserStatus.Active));
///
/// // Find single aggregate
/// var admin = await repository.FindAsync(new GetUserByRoleQuery(UserRole.Administrator));
/// </code>
/// </example>
public interface IAggregateQuery<TAggregate>
	where TAggregate : class, IAggregateRoot
{
}
