// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

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
}
