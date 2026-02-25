// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Data.Abstractions;

namespace Excalibur.Data;

/// <summary>
/// Represents a database abstraction for outbox persistence that extends the generic database interface <see cref="IDb" />.
/// </summary>
/// <remarks>
/// <para>
/// Use this marker interface to register a dedicated database connection for outbox stores
/// (e.g., <c>SqlServerOutboxStore</c>) when outbox messages should be persisted to a different
/// database than the domain event store.
/// </para>
/// <para>
/// If your application uses a single database for all stores, register <see cref="IDomainDb" />
/// and use it for outbox persistence as well. Use <see cref="IOutboxDb" /> only when you need
/// separate database connections.
/// </para>
/// </remarks>
public interface IOutboxDb : IDb;
