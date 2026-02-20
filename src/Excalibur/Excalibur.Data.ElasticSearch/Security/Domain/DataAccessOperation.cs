// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Represents the type of data access operation.
/// </summary>
public enum DataAccessOperation
{
	/// <summary>
	/// Data read operation.
	/// </summary>
	Read = 0,

	/// <summary>
	/// Data write or create operation.
	/// </summary>
	Write = 1,

	/// <summary>
	/// Data update or modify operation.
	/// </summary>
	Update = 2,

	/// <summary>
	/// Data delete operation.
	/// </summary>
	Delete = 3,

	/// <summary>
	/// Data export operation.
	/// </summary>
	Export = 4,

	/// <summary>
	/// Data import operation.
	/// </summary>
	Import = 5,

	/// <summary>
	/// Data query or search operation.
	/// </summary>
	Query = 6,
}
