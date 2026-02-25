// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Defines the types of data changes captured by Change Data Capture (CDC) systems.
/// </summary>
/// <remarks>
/// <para>
/// This enum provides a provider-agnostic representation of data change operations.
/// All supported CDC providers map their native change types to these values:
/// </para>
/// <list type="table">
/// <listheader>
/// <term>Provider</term>
/// <description>Native Mapping</description>
/// </listheader>
/// <item>
/// <term>SQL Server</term>
/// <description>Operation codes 1/2 (Delete), 3/4 (Insert), Update pairs</description>
/// </item>
/// <item>
/// <term>Postgres</term>
/// <description>Logical replication INSERT/UPDATE/DELETE messages</description>
/// </item>
/// <item>
/// <term>MongoDB</term>
/// <description>Change stream operationType: insert/update/replace/delete</description>
/// </item>
/// <item>
/// <term>CosmosDB</term>
/// <description>Change feed document changes (insert/update inferred, delete via TTL)</description>
/// </item>
/// <item>
/// <term>DynamoDB</term>
/// <description>Stream records: INSERT/MODIFY/REMOVE</description>
/// </item>
/// </list>
/// </remarks>
public enum ChangeType
{
	/// <summary>
	/// A new record was inserted.
	/// </summary>
	/// <remarks>
	/// <para>For inserts, <see cref="Change{T}.Before"/> is <see langword="null"/> and
	/// <see cref="Change{T}.After"/> contains the inserted data.</para>
	/// </remarks>
	Insert = 0,

	/// <summary>
	/// An existing record was updated.
	/// </summary>
	/// <remarks>
	/// <para>For updates, <see cref="Change{T}.Before"/> contains the previous state (if available)
	/// and <see cref="Change{T}.After"/> contains the new state.</para>
	/// <para>Not all providers support before-image capture. Check provider documentation
	/// for availability (e.g., MongoDB requires pre-image recording enabled).</para>
	/// </remarks>
	Update = 1,

	/// <summary>
	/// An existing record was deleted.
	/// </summary>
	/// <remarks>
	/// <para>For deletes, <see cref="Change{T}.Before"/> contains the deleted data (if available)
	/// and <see cref="Change{T}.After"/> is <see langword="null"/>.</para>
	/// <para>Not all providers support delete detection or before-image capture for deletes.
	/// Check provider documentation for availability.</para>
	/// </remarks>
	Delete = 2
}
