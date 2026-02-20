// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.IndexManagement;

/// <summary>
/// Base class for index lifecycle phase configurations.
/// </summary>
public abstract class PhaseConfiguration
{
	/// <summary>
	/// Gets the minimum age before this phase is entered.
	/// </summary>
	/// <value> The minimum age before entering this phase. </value>
	public TimeSpan? MinAge { get; init; }
}
