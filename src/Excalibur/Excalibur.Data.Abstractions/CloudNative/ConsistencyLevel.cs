// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.Abstractions.CloudNative;

/// <summary>
/// Defines consistency levels for cloud-native database operations.
/// </summary>
/// <remarks>
/// <para>
/// Cloud-native databases offer various consistency models to balance latency,
/// throughput, and data freshness. This enum provides a provider-agnostic
/// abstraction that maps to specific database consistency settings.
/// </para>
/// <para>
/// <strong>Provider Mappings:</strong>
/// </para>
/// <list type="table">
/// <listheader>
/// <term>Level</term>
/// <description>Cosmos DB | DynamoDB | Firestore</description>
/// </listheader>
/// <item>
/// <term>Strong</term>
/// <description>Strong | Strongly consistent | Default</description>
/// </item>
/// <item>
/// <term>Session</term>
/// <description>Session | N/A (use strong) | N/A</description>
/// </item>
/// <item>
/// <term>BoundedStaleness</term>
/// <description>BoundedStaleness | N/A | N/A</description>
/// </item>
/// <item>
/// <term>Eventual</term>
/// <description>Eventual | Eventually consistent | N/A</description>
/// </item>
/// </list>
/// </remarks>
public enum ConsistencyLevel
{
	/// <summary>
	/// Use the default consistency level configured for the database account.
	/// </summary>
	Default = 0,

	/// <summary>
	/// Strong consistency: reads always return the most recent committed write.
	/// Highest latency, strongest guarantees.
	/// </summary>
	/// <remarks>
	/// Use for:
	/// <list type="bullet">
	/// <item>Financial transactions</item>
	/// <item>Inventory management</item>
	/// <item>Critical business operations</item>
	/// </list>
	/// </remarks>
	Strong = 1,

	/// <summary>
	/// Session consistency: reads within a session see all writes from that session.
	/// Good balance of consistency and performance for single-user scenarios.
	/// </summary>
	/// <remarks>
	/// Use for:
	/// <list type="bullet">
	/// <item>User profile operations</item>
	/// <item>Shopping cart management</item>
	/// <item>Any single-session workflow</item>
	/// </list>
	/// </remarks>
	Session = 2,

	/// <summary>
	/// Bounded staleness: reads may lag behind writes by a configured time or version window.
	/// Provides predictable staleness bounds with good performance.
	/// </summary>
	/// <remarks>
	/// Use for:
	/// <list type="bullet">
	/// <item>Analytics dashboards</item>
	/// <item>Reporting systems</item>
	/// <item>Read-heavy workloads with acceptable lag</item>
	/// </list>
	/// </remarks>
	BoundedStaleness = 3,

	/// <summary>
	/// Eventual consistency: reads may not reflect recent writes.
	/// Lowest latency, weakest guarantees.
	/// </summary>
	/// <remarks>
	/// Use for:
	/// <list type="bullet">
	/// <item>Social media feeds</item>
	/// <item>Product catalogs</item>
	/// <item>Non-critical read operations</item>
	/// </list>
	/// </remarks>
	Eventual = 4
}
