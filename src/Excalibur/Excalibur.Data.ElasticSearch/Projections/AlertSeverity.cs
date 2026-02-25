// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Projections;

/// <summary>
/// Defines alert severity levels.
/// </summary>
public enum AlertSeverity
{
	/// <summary>
	/// Informational alert.
	/// </summary>
	Info = 0,

	/// <summary>
	/// Warning alert.
	/// </summary>
	Warning = 1,

	/// <summary>
	/// Error alert requiring attention.
	/// </summary>
	Error = 2,

	/// <summary>
	/// Critical alert requiring immediate attention.
	/// </summary>
	Critical = 3,
}
