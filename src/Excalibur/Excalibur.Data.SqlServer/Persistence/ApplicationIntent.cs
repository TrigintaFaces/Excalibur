// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.SqlServer.Persistence;

/// <summary>
/// Specifies the application intent for the connection.
/// </summary>
public enum ApplicationIntent
{
	/// <summary>
	/// Read and write operations.
	/// </summary>
	ReadWrite = 0,

	/// <summary>
	/// Read-only operations.
	/// </summary>
	ReadOnly = 1,
}
