// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Projections;

/// <summary>
/// Defines migration strategies.
/// </summary>
public enum MigrationStrategy
{
	/// <summary>
	/// Reindex all documents to a new index.
	/// </summary>
	Reindex = 0,

	/// <summary>
	/// Update mapping in place (limited to compatible changes).
	/// </summary>
	UpdateInPlace = 1,

	/// <summary>
	/// Create alias and switch after migration.
	/// </summary>
	AliasSwitch = 2,

	/// <summary>
	/// Dual write to both old and new schemas during transition.
	/// </summary>
	DualWrite = 3,
}
