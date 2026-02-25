// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Projections;

/// <summary>
/// Defines types of field changes.
/// </summary>
public enum FieldChangeType
{
	/// <summary>
	/// Field was added.
	/// </summary>
	Added = 0,

	/// <summary>
	/// Field was removed.
	/// </summary>
	Removed = 1,

	/// <summary>
	/// Field type was changed.
	/// </summary>
	TypeChanged = 2,

	/// <summary>
	/// Field properties were modified.
	/// </summary>
	PropertiesModified = 3,

	/// <summary>
	/// Field was renamed.
	/// </summary>
	Renamed = 4,
}
