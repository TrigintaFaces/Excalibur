// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc;

namespace Excalibur.Cdc.SqlServer;

/// <summary>
/// Derives the SQL Server CDC capture-instance list and the capture-instance → logical
/// table-name map from a set of <see cref="CdcTableConfig"/> entries.
/// </summary>
/// <remarks>
/// Single source of truth shared by both CDC configuration paths — the fluent builder
/// (<c>UseSqlServer</c>/<c>TrackTable</c>) and the config-driven job path
/// (<c>DatabaseConfigs[].Tables</c>) — so they produce identical runtime behavior.
/// </remarks>
internal static class CdcCaptureInstanceDeriver
{
	/// <summary>
	/// Derives capture instances and their logical-name map from the supplied tables.
	/// </summary>
	/// <param name="tables">The tables to derive from.</param>
	/// <returns>
	/// A tuple of the distinct capture instances and a map from each capture instance to its
	/// logical <see cref="CdcTableConfig.TableName"/>. The capture instance is
	/// <see cref="CdcTableConfig.CaptureInstance"/> when set; otherwise it falls back to
	/// <see cref="CdcTableConfig.TableName"/> (identity). Duplicate capture instances are
	/// skipped (case-insensitive); the first occurrence wins.
	/// </returns>
	public static (string[] CaptureInstances, IReadOnlyDictionary<string, string> CaptureInstanceToTableNameMap) Derive(
		IEnumerable<CdcTableConfig> tables)
	{
		ArgumentNullException.ThrowIfNull(tables);

		var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

		foreach (var table in tables)
		{
			if (table is null || string.IsNullOrEmpty(table.TableName))
			{
				continue;
			}

			var instance = string.IsNullOrEmpty(table.CaptureInstance) ? table.TableName : table.CaptureInstance;

			if (set.Add(instance))
			{
				// Map capture instance → logical table name so handlers identify tables by their
				// logical name even though CDC rows carry the capture instance.
				map[instance] = table.TableName;
			}
		}

		return ([.. set], map.AsReadOnly());
	}
}
