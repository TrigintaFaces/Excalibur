// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Cdc.Postgres;

/// <summary>
/// Fluent builder interface for configuring Postgres CDC settings.
/// </summary>
/// <remarks>
/// <para>
/// This interface composes three focused sub-interfaces for consumers that need only a subset of capabilities:
/// </para>
/// <list type="bullet">
/// <item><description><see cref="IPostgresCdcConnectionBuilder"/> — Connection, config binding, and state store configuration.</description></item>
/// <item><description><see cref="IPostgresCdcProcessingBuilder"/> — Schema, table, polling, batch, and timeout settings.</description></item>
/// <item><description><see cref="IPostgresCdcReplicationBuilder"/> — Replication slot, publication, protocol, and processor identity.</description></item>
/// </list>
/// </remarks>
/// <example>
/// <code>
/// cdc.UsePostgres(pg =>
/// {
///     pg.ConnectionString(connectionString)
///       .SchemaName("excalibur")
///       .StateTableName("cdc_state")
///       .ReplicationSlotName("my_slot")
///       .PublicationName("my_publication")
///       .PollingInterval(TimeSpan.FromSeconds(1))
///       .BatchSize(1000);
/// });
/// </code>
/// </example>
public interface IPostgresCdcBuilder
	: IPostgresCdcConnectionBuilder,
	  IPostgresCdcProcessingBuilder,
	  IPostgresCdcReplicationBuilder
{
}
