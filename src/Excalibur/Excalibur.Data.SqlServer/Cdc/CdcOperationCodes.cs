// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.SqlServer.Cdc;

/// <summary>
/// Enumeration representing the types of Change Data Capture (CDC) operations.
/// </summary>
public enum CdcOperationCodes
{
	/// <summary>
	/// Represents an unknown operation. This is the default value.
	/// </summary>
	Unknown = 0,

	/// <summary>
	/// Represents a delete operation, indicating that a row was removed from the table.
	/// </summary>
	Delete = 1,

	/// <summary>
	/// Represents an insert operation, indicating that a new row was added to the table.
	/// </summary>
	Insert = 2,

	/// <summary>
	/// Represents the state of a row before an update operation.
	/// </summary>
	UpdateBefore = 3,

	/// <summary>
	/// Represents the state of a row after an update operation.
	/// </summary>
	UpdateAfter = 4,
}
