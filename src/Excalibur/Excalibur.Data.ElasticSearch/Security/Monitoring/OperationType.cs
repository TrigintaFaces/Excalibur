// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Defines data operation types.
/// </summary>
public enum OperationType
{
	/// <summary>
	/// Read operation for retrieving data.
	/// </summary>
	Read = 0,

	/// <summary>
	/// Create operation for adding new data.
	/// </summary>
	Create = 1,

	/// <summary>
	/// Update operation for modifying existing data.
	/// </summary>
	Update = 2,

	/// <summary>
	/// Delete operation for removing data.
	/// </summary>
	Delete = 3,

	/// <summary>
	/// Bulk operation for performing multiple operations in a single request.
	/// </summary>
	BulkOperation = 4,
}
