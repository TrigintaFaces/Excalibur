// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Domain.Model;

/// <summary>
/// Represents a snapshot of an aggregate's state at a specific version.
/// </summary>
public interface ISnapshot
{
	/// <summary>
	/// Gets the unique identifier for this snapshot.
	/// </summary>
	/// <value>The unique identifier for this snapshot.</value>
	string SnapshotId { get; }

	/// <summary>
	/// Gets the aggregate identifier this snapshot belongs to.
	/// </summary>
	/// <value>The aggregate identifier this snapshot belongs to.</value>
	string AggregateId { get; }

	/// <summary>
	/// Gets the version of the aggregate when this snapshot was created.
	/// </summary>
	/// <value>The version of the aggregate when this snapshot was created.</value>
	long Version { get; }

	/// <summary>
	/// Gets the timestamp when this snapshot was created.
	/// </summary>
	/// <value>The timestamp when this snapshot was created.</value>
	DateTimeOffset CreatedAt { get; }

	/// <summary>
	/// Gets the serialized state data.
	/// </summary>
	/// <value>The serialized state data as an immutable memory region.</value>
	ReadOnlyMemory<byte> Data { get; }

	/// <summary>
	/// Gets the type of the aggregate for deserialization.
	/// </summary>
	/// <value>The type of the aggregate for deserialization.</value>
	string AggregateType { get; }

	/// <summary>
	/// Gets optional metadata about the snapshot.
	/// </summary>
	/// <value>Optional metadata about the snapshot.</value>
	IDictionary<string, object>? Metadata { get; }

	/// <summary>
	/// Gets the tenant this snapshot belongs to, when the host is multi-tenant.
	/// </summary>
	/// <remarks>
	/// A snapshot is identified by its aggregate, and two tenants can hold the same aggregate identifier.
	/// Without a tenant the identity is ambiguous: one tenant's snapshot can be served to another, and a
	/// store keyed only on the aggregate cannot hold both at once. This is that discriminator.
	/// <para>
	/// Required, deliberately, and NOT defaulted to <see langword="null"/>. A default would let every
	/// implementation inherit "no tenant" without stating it, so a store that never considered tenancy
	/// would be indistinguishable from one that considered it and correctly reported none. Requiring the
	/// member forces each implementation to answer the question; forgetting is a compile error rather
	/// than a silent single-tenant assumption.
	/// </para>
	/// <para>
	/// <see langword="null"/> means single-tenant — the correct value for a host that is not
	/// multi-tenant. It does not mean "unknown".
	/// </para>
	/// </remarks>
	/// <value>The owning tenant identifier, or <see langword="null"/> in a single-tenant host.</value>
	string? TenantId { get; }
}
