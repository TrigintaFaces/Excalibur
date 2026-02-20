// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.IndexManagement;

/// <summary>
/// Configures index lifecycle management settings.
/// </summary>
public sealed class LifecycleManagementOptions
{
	/// <summary>
	/// Gets a value indicating whether lifecycle management is enabled.
	/// </summary>
	/// <value> True to enable lifecycle management, false otherwise. </value>
	public bool Enabled { get; init; } = true;

	/// <summary>
	/// Gets the hot phase duration.
	/// </summary>
	/// <value> The duration of the hot phase. Defaults to 7 days. </value>
	public TimeSpan HotPhaseDuration { get; init; } = TimeSpan.FromDays(7);

	/// <summary>
	/// Gets the warm phase duration.
	/// </summary>
	/// <value> The duration of the warm phase. Defaults to 30 days. </value>
	public TimeSpan WarmPhaseDuration { get; init; } = TimeSpan.FromDays(30);

	/// <summary>
	/// Gets the cold phase duration.
	/// </summary>
	/// <value> The duration of the cold phase. Defaults to 90 days. </value>
	public TimeSpan ColdPhaseDuration { get; init; } = TimeSpan.FromDays(90);

	/// <summary>
	/// Gets a value indicating whether to delete indices after the cold phase.
	/// </summary>
	/// <value> True to delete old indices, false to keep them indefinitely. </value>
	public bool DeleteAfterColdPhase { get; init; }
}
