// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.IndexManagement;

/// <summary>
/// Specifies the type of alias operation to perform.
/// </summary>
public enum AliasOperationType
{
	/// <summary>
	/// Add an alias to an index.
	/// </summary>
	Add = 0,

	/// <summary>
	/// Remove an alias from an index.
	/// </summary>
	Remove = 1,
}
