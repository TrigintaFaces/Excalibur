// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Abstractions.CloudNative;

namespace Excalibur.Data.Abstractions;

/// <summary>
/// Configures consistency levels for database operations.
/// </summary>
/// <remarks>
/// <para>
/// This interface provides a provider-agnostic way to configure
/// consistency levels that map to specific provider implementations:
/// </para>
/// <list type="table">
/// <listheader>
/// <term>Provider</term>
/// <description>Configuration approach</description>
/// </listheader>
/// <item>
/// <term>CosmosDB</term>
/// <description>Maps to <c>ConsistencyLevel</c> in <c>ItemRequestOptions</c></description>
/// </item>
/// <item>
/// <term>DynamoDB</term>
/// <description>Maps to <c>ConsistentRead</c> flag in operations</description>
/// </item>
/// <item>
/// <term>MongoDB</term>
/// <description>Maps to <c>ReadConcern</c> and <c>WriteConcern</c></description>
/// </item>
/// <item>
/// <term>Firestore</term>
/// <description>Always strong consistency (configuration ignored)</description>
/// </item>
/// </list>
/// </remarks>
public interface IConsistencyConfiguration
{
	/// <summary>
	/// Gets the default consistency level for all operations.
	/// </summary>
	/// <value>
	/// The default consistency level. Defaults to <see cref="ConsistencyLevel.Session"/>.
	/// </value>
	/// <remarks>
	/// This level is used when <see cref="ReadConsistencyLevel"/> or
	/// <see cref="WriteConsistencyLevel"/> is not explicitly set.
	/// </remarks>
	ConsistencyLevel DefaultConsistencyLevel { get; }

	/// <summary>
	/// Gets the consistency level for read operations.
	/// </summary>
	/// <value>
	/// The read consistency level. If not set, falls back to <see cref="DefaultConsistencyLevel"/>.
	/// </value>
	/// <remarks>
	/// <para>
	/// Read consistency affects:
	/// </para>
	/// <list type="bullet">
	/// <item><description>Point reads (GetAsync)</description></item>
	/// <item><description>Query operations (QueryAsync)</description></item>
	/// <item><description>Aggregate reads (projections)</description></item>
	/// </list>
	/// </remarks>
	ConsistencyLevel ReadConsistencyLevel { get; }

	/// <summary>
	/// Gets the consistency level for write operations.
	/// </summary>
	/// <value>
	/// The write consistency level. If not set, falls back to <see cref="DefaultConsistencyLevel"/>.
	/// </value>
	/// <remarks>
	/// <para>
	/// Write consistency affects:
	/// </para>
	/// <list type="bullet">
	/// <item><description>Upsert operations (UpsertAsync)</description></item>
	/// <item><description>Delete operations (DeleteAsync)</description></item>
	/// <item><description>Batch/transactional writes</description></item>
	/// </list>
	/// </remarks>
	ConsistencyLevel WriteConsistencyLevel { get; }
}

/// <summary>
/// Default implementation of <see cref="IConsistencyConfiguration"/>.
/// </summary>
public sealed class ConsistencyConfiguration : IConsistencyConfiguration
{
	/// <summary>
	/// Gets a configuration with eventual consistency for all operations.
	/// </summary>
	public static ConsistencyConfiguration Eventual => new()
	{
		DefaultConsistencyLevel = ConsistencyLevel.Eventual,
		ReadConsistencyLevel = ConsistencyLevel.Eventual,
		WriteConsistencyLevel = ConsistencyLevel.Eventual
	};

	/// <summary>
	/// Gets a configuration with session consistency for all operations.
	/// </summary>
	public static ConsistencyConfiguration Session => new()
	{
		DefaultConsistencyLevel = ConsistencyLevel.Session,
		ReadConsistencyLevel = ConsistencyLevel.Session,
		WriteConsistencyLevel = ConsistencyLevel.Session
	};

	/// <summary>
	/// Gets a configuration with strong consistency for all operations.
	/// </summary>
	public static ConsistencyConfiguration Strong => new()
	{
		DefaultConsistencyLevel = ConsistencyLevel.Strong,
		ReadConsistencyLevel = ConsistencyLevel.Strong,
		WriteConsistencyLevel = ConsistencyLevel.Strong
	};

	/// <summary>
	/// Gets a configuration optimized for read-heavy workloads (eventual reads, session writes).
	/// </summary>
	public static ConsistencyConfiguration ReadOptimized => new()
	{
		DefaultConsistencyLevel = ConsistencyLevel.Session,
		ReadConsistencyLevel = ConsistencyLevel.Eventual,
		WriteConsistencyLevel = ConsistencyLevel.Session
	};

	/// <inheritdoc/>
	public ConsistencyLevel DefaultConsistencyLevel { get; set; } = ConsistencyLevel.Session;

	/// <inheritdoc/>
	public ConsistencyLevel ReadConsistencyLevel { get; set; } = ConsistencyLevel.Session;

	/// <inheritdoc/>
	public ConsistencyLevel WriteConsistencyLevel { get; set; } = ConsistencyLevel.Session;
}
