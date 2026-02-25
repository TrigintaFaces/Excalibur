// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Projections;

/// <summary>
/// Defines types of migration step operations.
/// </summary>
public enum StepOperationType
{
	/// <summary>
	/// Create a new index.
	/// </summary>
	CreateIndex = 0,

	/// <summary>
	/// Update index mapping.
	/// </summary>
	UpdateMapping = 1,

	/// <summary>
	/// Reindex documents.
	/// </summary>
	Reindex = 2,

	/// <summary>
	/// Transform documents.
	/// </summary>
	Transform = 3,

	/// <summary>
	/// Validate data integrity.
	/// </summary>
	Validate = 4,

	/// <summary>
	/// Switch alias.
	/// </summary>
	SwitchAlias = 5,

	/// <summary>
	/// Delete old index.
	/// </summary>
	DeleteIndex = 6,

	/// <summary>
	/// Create backup.
	/// </summary>
	Backup = 7,
}
