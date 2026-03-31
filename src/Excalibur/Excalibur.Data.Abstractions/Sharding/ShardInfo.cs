// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Data.Abstractions.Sharding;

/// <summary>
/// Describes a data shard's connection and routing metadata.
/// </summary>
/// <param name="ShardId">The unique shard identifier.</param>
/// <param name="ConnectionString">The connection string for this shard's primary data store.</param>
/// <param name="SchemaName">Optional schema name for schema-per-tenant isolation (SQL Server/Postgres).</param>
/// <param name="DatabaseName">Optional database name for database-per-tenant isolation.</param>
/// <param name="IndexPrefix">Optional index prefix for document/search store isolation (Elasticsearch, CosmosDB).</param>
/// <param name="Region">Optional region hint for geo-distributed shards.</param>
public sealed record ShardInfo(
	string ShardId,
	string ConnectionString,
	string? SchemaName = null,
	string? DatabaseName = null,
	string? IndexPrefix = null,
	string? Region = null);
