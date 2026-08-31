// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;

namespace Excalibur.AuditLogging;

/// <summary>
/// Names one chain partition — the unit a store chains over on write — so that every read path naming the
/// same partition arrives at the same key.
/// </summary>
/// <remarks>
/// <para>
/// A store verifying a range asks two questions of its table: which records fall inside the window, and
/// what tag immediately precedes the window. The answers must be matched up per partition, and the
/// matching is by key. Deriving those two keys separately is the defect this type exists to make
/// unwriteable: the halves are built in different methods, from differently-shaped rows, and a fold
/// applied to one is easy to omit from the other. When that happens the lookup misses, the anchor is
/// read as absent, and the verifier is told the first in-range record is the partition's genesis one —
/// which it is not. The result is a report of removal or reordering against a trail nobody touched, and
/// an auditor holding it cannot tell it from the real thing.
/// </para>
/// <para>
/// There is therefore no public constructor. A key is obtainable only from <see cref="For"/>, which
/// applies every fold, so a caller cannot hand-build one that skips them. Both folds are idempotent:
/// passing an already-folded value yields the same key as passing the raw stored one, which is what
/// allows the record path — holding events whose tenant has already been mapped back to the signed
/// value — and the anchor path — holding raw columns — to share the one factory.
/// </para>
/// </remarks>
public readonly record struct AuditChainKey
{
	private AuditChainKey(string? tenantId, string? applicationName)
	{
		TenantId = tenantId;
		ApplicationName = applicationName;
	}

	/// <summary>
	/// Gets the partition's tenant, as the record was signed rather than as it is stored.
	/// </summary>
	/// <value>The originating tenant identifier, or <see langword="null"/> for the untenanted partition.</value>
	public string? TenantId { get; }

	/// <summary>
	/// Gets the partition's application.
	/// </summary>
	/// <value>The application name, or <see langword="null"/> when the partition carries none.</value>
	public string? ApplicationName { get; }

	/// <summary>
	/// Derives the key of the partition a stored row belongs to.
	/// </summary>
	/// <param name="tenantId">The tenant term, either as stored or already mapped back to the signed value.</param>
	/// <param name="applicationName">The application name as stored, in any of its spellings.</param>
	/// <returns>The partition key.</returns>
	/// <remarks>
	/// Empty and <see langword="null"/> application names are folded together, so a record stored with no
	/// application name and one stored with an empty one are not read as two chains when the write path
	/// appended them to one.
	/// </remarks>
	public static AuditChainKey For(string? tenantId, string? applicationName) =>
		new(SignedTenantId(tenantId), string.IsNullOrEmpty(applicationName) ? null : applicationName);

	/// <summary>
	/// Maps a stored tenant term back to the value that was signed.
	/// </summary>
	/// <param name="storedTenantId">The tenant term as persisted.</param>
	/// <returns>The originating tenant identifier, or <see langword="null"/> for an untenanted record.</returns>
	/// <remarks>
	/// The reserved sentinel is a storage encoding for "no tenant", adopted where the column cannot hold
	/// null. The integrity tag, however, is computed over the record as supplied — where an untenanted
	/// event carries a null tenant. Returning the sentinel would re-canonicalize a different record than
	/// the one that was signed, and every untenanted record would fail verification against its own tag.
	/// </remarks>
	public static string? SignedTenantId(string? storedTenantId) =>
		KeyedTenantPartition.ToSignedTenantId(storedTenantId);
}
