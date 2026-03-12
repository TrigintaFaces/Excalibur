// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Cdc.Postgres;

/// <summary>
/// Represents the type of data change captured during Postgres CDC processing.
/// </summary>
public enum PostgresDataChangeType
{
	/// <summary>The data change type is unknown or not specified.</summary>
	Unknown = 0,
	/// <summary>The change represents an insert operation.</summary>
	Insert = 1,
	/// <summary>The change represents an update operation.</summary>
	Update = 2,
	/// <summary>The change represents a delete operation.</summary>
	Delete = 3,
	/// <summary>The change represents a truncate operation.</summary>
	Truncate = 4,
}

/// <summary>
/// Extension methods for converting between <see cref="PostgresDataChangeType"/> and <see cref="CdcChangeType"/>.
/// </summary>
public static class PostgresDataChangeTypeExtensions
{
	/// <summary>
	/// Converts a provider-specific <see cref="PostgresDataChangeType"/> to the canonical <see cref="CdcChangeType"/>.
	/// </summary>
	public static CdcChangeType ToCdcChangeType(this PostgresDataChangeType changeType) =>
		changeType switch
		{
			PostgresDataChangeType.Insert => CdcChangeType.Insert,
			PostgresDataChangeType.Update => CdcChangeType.Update,
			PostgresDataChangeType.Delete => CdcChangeType.Delete,
			PostgresDataChangeType.Truncate => CdcChangeType.Truncate,
			_ => CdcChangeType.None,
		};
}
