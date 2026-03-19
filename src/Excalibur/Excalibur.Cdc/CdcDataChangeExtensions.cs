// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Cdc;

/// <summary>
/// Extension methods for extracting typed values from CDC change data.
/// </summary>
/// <remarks>
/// <para>
/// These methods are designed for use in <see cref="ICdcEventMapper{TEvent}"/> implementations
/// to extract column values from change data without reflection.
/// </para>
/// </remarks>
public static class CdcDataChangeExtensions
{
	/// <summary>
	/// Gets the new value of a column by name, cast to <typeparamref name="T"/>.
	/// </summary>
	/// <typeparam name="T">The expected type of the column value.</typeparam>
	/// <param name="changes">The list of column changes.</param>
	/// <param name="columnName">The column name to look up.</param>
	/// <returns>The new value of the column cast to <typeparamref name="T"/>.</returns>
	/// <exception cref="CdcMappingException">Thrown when the column is not found in the change data.</exception>
	public static T GetValue<T>(this IReadOnlyList<CdcDataChange> changes, string columnName)
	{
		for (var i = 0; i < changes.Count; i++)
		{
			if (string.Equals(changes[i].ColumnName, columnName, StringComparison.OrdinalIgnoreCase))
			{
				return (T)changes[i].NewValue!;
			}
		}

		throw new CdcMappingException($"Column '{columnName}' not found in change data.");
	}

	/// <summary>
	/// Gets the old value (before change) of a column by name, cast to <typeparamref name="T"/>.
	/// </summary>
	/// <typeparam name="T">The expected type of the column value.</typeparam>
	/// <param name="changes">The list of column changes.</param>
	/// <param name="columnName">The column name to look up.</param>
	/// <returns>The old value of the column cast to <typeparamref name="T"/>.</returns>
	/// <exception cref="CdcMappingException">Thrown when the column is not found in the change data.</exception>
	public static T GetOldValue<T>(this IReadOnlyList<CdcDataChange> changes, string columnName)
	{
		for (var i = 0; i < changes.Count; i++)
		{
			if (string.Equals(changes[i].ColumnName, columnName, StringComparison.OrdinalIgnoreCase))
			{
				return (T)changes[i].OldValue!;
			}
		}

		throw new CdcMappingException($"Column '{columnName}' not found in change data.");
	}

	/// <summary>
	/// Tries to get the new value of a column; returns <see langword="false"/> if the column is missing.
	/// </summary>
	/// <typeparam name="T">The expected type of the column value.</typeparam>
	/// <param name="changes">The list of column changes.</param>
	/// <param name="columnName">The column name to look up.</param>
	/// <param name="value">When this method returns, contains the column value if found; otherwise, the default value.</param>
	/// <returns><see langword="true"/> if the column was found; otherwise, <see langword="false"/>.</returns>
	public static bool TryGetValue<T>(this IReadOnlyList<CdcDataChange> changes, string columnName, out T? value)
	{
		for (var i = 0; i < changes.Count; i++)
		{
			if (string.Equals(changes[i].ColumnName, columnName, StringComparison.OrdinalIgnoreCase))
			{
				value = (T?)changes[i].NewValue;
				return true;
			}
		}

		value = default;
		return false;
	}
}
