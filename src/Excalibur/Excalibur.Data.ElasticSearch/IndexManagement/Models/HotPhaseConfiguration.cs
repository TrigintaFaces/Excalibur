// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.IndexManagement;

/// <summary>
/// Configuration for the hot phase of index lifecycle management.
/// </summary>
public sealed class HotPhaseConfiguration : PhaseConfiguration
{
	/// <summary>
	/// Gets the rollover conditions for the hot phase.
	/// </summary>
	/// <value> The conditions that trigger index rollover. </value>
	public RolloverConditions? Rollover { get; init; }

	/// <summary>
	/// Gets a value indicating whether to set priority for hot indices.
	/// </summary>
	/// <value> The priority value for hot indices. </value>
	public int? Priority { get; init; }
}
