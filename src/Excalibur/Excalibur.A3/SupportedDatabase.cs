// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.A3;

/// <summary>
/// Enumeration of database types supported by the A3 system.
/// </summary>
public enum SupportedDatabase
{
	/// <summary>
	/// Unknown or unspecified database type.
	/// </summary>
	Unknown = 0,

	/// <summary>
	/// Postgres database.
	/// </summary>
	Postgres = 1,

	/// <summary>
	/// Microsoft SQL Server database.
	/// </summary>
	SqlServer = 2,
}
