// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;

namespace Excalibur.Inbox;

/// <summary>
/// The pure deployment-mode ↔ physical-schema contract for keyed inbox stores, shared by every relational
/// provider so the safety truth-table has one definition rather than per-provider copies that can drift.
/// Providers supply their read physical key plus their own column names (which differ by case per provider);
/// the logic is identical.
/// </summary>
internal static class InboxSchemaContract
{
	/// <summary>
	/// Verifies the physical unique key against the deployment mode and returns whether the tenant column is
	/// part of the key (which drives SQL emission — the store emits the tenant term iff the column exists).
	/// </summary>
	/// <param name="tableName">The (qualified) table name, for diagnostics.</param>
	/// <param name="multiTenant"><see langword="true"/> when multi-tenancy is registered (<c>RequireTenant</c>).</param>
	/// <param name="primaryKeyColumns">The physical primary-key columns, in key order.</param>
	/// <param name="tenantIdIsNullable"><see langword="null"/> when the tenant column is absent; <see langword="true"/> when nullable; <see langword="false"/> when NOT NULL.</param>
	/// <param name="messageIdColumn">The provider's message-id column name.</param>
	/// <param name="handlerTypeColumn">The provider's handler-type column name.</param>
	/// <param name="tenantIdColumn">The provider's tenant-id column name.</param>
	/// <returns><see langword="true"/> when the tenant column is part of the physical unique key.</returns>
	/// <exception cref="InvalidOperationException">
	/// The physical schema does not match the deployment mode: a multi-tenant store requires the triple key
	/// with a non-null tenant column; a single-tenant store requires the pair key with no tenant column.
	/// </exception>
	internal static bool Verify(
		string tableName,
		bool multiTenant,
		IReadOnlyList<string> primaryKeyColumns,
		bool? tenantIdIsNullable,
		string messageIdColumn,
		string handlerTypeColumn,
		string tenantIdColumn)
	{
		var foundKey = primaryKeyColumns.Count == 0 ? "(none)" : string.Join(", ", primaryKeyColumns);
		var tenantState = tenantIdIsNullable is null ? "absent" : tenantIdIsNullable.Value ? "NULL" : "NOT NULL";
		var actualKey = new HashSet<string>(primaryKeyColumns, StringComparer.OrdinalIgnoreCase);
		var hasTenantColumn = actualKey.Contains(tenantIdColumn);

		if (multiTenant)
		{
			var requiredKey = new[] { messageIdColumn, handlerTypeColumn, tenantIdColumn };
			var keyMatches = primaryKeyColumns.Count == requiredKey.Length && Array.TrueForAll(requiredKey, actualKey.Contains);
			if (!keyMatches || tenantIdIsNullable is null or true)
			{
				throw new InvalidOperationException(
					$"Multi-tenant inbox store: table {tableName} must have a PRIMARY KEY on " +
					$"({messageIdColumn}, {handlerTypeColumn}, {tenantIdColumn}) with {tenantIdColumn} NOT NULL. Found key " +
					$"[{foundKey}] and {tenantIdColumn} {tenantState}. Apply the multi-tenant inbox schema script, or " +
					"register the store without multi-tenancy for the single-tenant schema.");
			}
		}
		else
		{
			var requiredKey = new[] { messageIdColumn, handlerTypeColumn };
			var keyMatches = primaryKeyColumns.Count == requiredKey.Length && Array.TrueForAll(requiredKey, actualKey.Contains);
			if (!keyMatches || tenantIdIsNullable is not null)
			{
				throw new InvalidOperationException(
					$"Single-tenant inbox store: table {tableName} must have a PRIMARY KEY on " +
					$"({messageIdColumn}, {handlerTypeColumn}) and no {tenantIdColumn} column. Found key [{foundKey}] and " +
					$"{tenantIdColumn} {tenantState}. Apply the single-tenant inbox schema script, or register " +
					"multi-tenancy for the multi-tenant schema.");
			}
		}

		return hasTenantColumn;
	}

	/// <summary>
	/// Verifies that the ambient tenant identity is admissible under the key this store actually emits.
	/// </summary>
	/// <param name="tableName">The (qualified) table name, for diagnostics.</param>
	/// <param name="hasTenantColumn">Whether the tenant column is part of the physical unique key, as returned by <see cref="Verify"/>.</param>
	/// <param name="tenantContext">The ambient tenant context, read fresh on every call.</param>
	/// <exception cref="InvalidOperationException">
	/// The store emits no tenant discriminator, yet the ambient context resolves an identity other than the
	/// single canonical one.
	/// </exception>
	/// <remarks>
	/// <para>
	/// The invariant: a statement that emits NO tenant discriminator may be executed under AT MOST ONE tenant
	/// identity. <see cref="Verify"/> checks the physical schema against the deployment mode; it never checks
	/// the ambient identity against the schema, and the two are different questions with different lifetimes.
	/// The schema is read once and cached; the identity is a property of the OPERATION and must be re-read on
	/// every call, which is why this is a separate member rather than another statement inside the cached one.
	/// </para>
	/// <para>
	/// It takes <see cref="ITenantContext"/> rather than an already-resolved partition deliberately. Resolving
	/// the partition at the call site throws when multi-tenancy is active and no tenant is resolved, and the
	/// eager validation arm runs at host start with no request in flight -- so an eagerly-evaluated argument
	/// would fault startup in every multi-tenant deployment. Passing the context defers the read past the
	/// <paramref name="hasTenantColumn"/> short-circuit, which is the only path a multi-tenant store takes.
	/// </para>
	/// </remarks>
	internal static void VerifyTenantAdmissible(
		string tableName,
		bool hasTenantColumn,
		ITenantContext tenantContext)
	{
		if (hasTenantColumn)
		{
			// The statement carries the discriminator, so every identity is separated by construction.
			return;
		}

		// ADR-345 Decision 1 draws a line this check has to respect: the STORED partition and the AMBIENT
		// context are different states with different representations, and conflating them has already cost
		// a bead filed against correct design. Stored "no tenant" is the reserved value. Ambient "no tenant"
		// is an ABSENCE -- ITenantContext.TenantId is nullable, and that null is load-bearing, because the
		// fail-closed conversion throws on it rather than proceeding. This member reads the AMBIENT state, so
		// it asks the interface's own question first.
		if (!tenantContext.HasTenant)
		{
			// No tenant is resolved. That is not a foreign identity on an undiscriminating key -- it is the
			// ordinary single-tenant composition, and treating it as a violation would reject the shape most
			// single-tenant consumers have. An earlier revision of this check did exactly that.
			return;
		}

		// A resolved identity may still be a reserved spelling of "no tenant" rather than a real tenant.
		// Both are admissible: a host with no tenant of its own leaves it unresolved and the keyed partition
		// maps that onto the untenanted sentinel, while DefaultTenantId is the other constant identity a
		// single-tenant host can operate under -- it is configurable, so a host may run as a named single
		// tenant. What this guard exists to catch is a REAL tenant identity on a key that cannot distinguish
		// one, and a real identity is neither of these.
		if (string.Equals(tenantContext.TenantId, TenantScope.UntenantedSentinel, StringComparison.Ordinal)
			|| string.Equals(tenantContext.TenantId, TenantDefaults.DefaultTenantId, StringComparison.Ordinal))
		{
			return;
		}

		throw new InvalidOperationException(
			$"Single-tenant inbox store: table {tableName} has no tenant column, so every statement it "
			+ "emits is keyed by (MessageId, HandlerType) alone, yet the ambient tenant context resolves "
			+ $"'{tenantContext.TenantId}' rather than the single canonical identity. A second tenant "
			+ "identity on a key that cannot distinguish it would silently deduplicate one tenant's message "
			+ "against another's -- the second tenant's message would be discarded as an already-processed "
			+ "duplicate and never handled. Enable multi-tenant mode and apply the multi-tenant inbox "
			+ "schema, or remove the custom tenant context to operate as the single canonical tenant.");
	}
}
