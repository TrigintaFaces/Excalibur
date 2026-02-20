// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Data.Abstractions;

namespace Excalibur.Data;

/// <summary>
/// Represents a database abstraction for SQL read-side projection persistence that extends the generic database interface <see cref="IDb" />.
/// </summary>
/// <remarks>
/// <para>
/// Use this marker interface to register a dedicated SQL database connection for projection stores
/// (e.g., <c>SqlServerProjectionStore</c>, <c>PostgresProjectionStore</c>) when read-side
/// projections should be persisted to a different database than the domain event store.
/// </para>
/// <para>
/// Separating read and write databases is a common CQRS pattern. Register <see cref="IProjectionDb" />
/// pointing to your read database and <see cref="IDomainDb" /> pointing to your write database.
/// </para>
/// <para>
/// This interface applies to SQL projection stores only. Document database projection stores
/// (Elasticsearch, CosmosDB, MongoDB) use their own SDK clients and are configured through
/// <c>IOptions&lt;T&gt;</c> rather than <see cref="IDb" />.
/// </para>
/// </remarks>
public interface IProjectionDb : IDb;
