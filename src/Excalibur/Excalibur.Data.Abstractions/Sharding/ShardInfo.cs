// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Data.Sharding;

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
	string? Region = null)
{
	/// <summary>
	/// Returns an isolation coordinate this shard declares, or throws when it does not declare one.
	/// </summary>
	/// <param name="value">The coordinate read from this shard entry.</param>
	/// <param name="coordinateName">The coordinate's name, for the message.</param>
	/// <returns>The declared value.</returns>
	/// <exception cref="InvalidOperationException">Thrown when the shard declares no value.</exception>
	/// <remarks>
	/// A shard exists to keep one tenant's data apart from another's, so a coordinate it does not declare
	/// cannot be filled from a shared default: two shards that both omit the same coordinate resolve to the
	/// same physical location, and the tenants mapped to them are silently commingled with no error and no
	/// log. Where the intended value genuinely is the deployment default, the shard entry states it — an
	/// isolation boundary is not something to infer from a null.
	/// </remarks>
	public string RequireCoordinate(string? value, string coordinateName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(coordinateName);

		return string.IsNullOrWhiteSpace(value)
			? throw new InvalidOperationException(
				$"Tenant shard '{ShardId}' does not declare {coordinateName}. A shard that omits an "
				+ "isolation coordinate would fall back to the shared default, placing every "
				+ "incompletely-mapped tenant in the same location. Declare it on the shard entry, even "
				+ "when the intended value is the deployment default.")
			: value;
	}
}
