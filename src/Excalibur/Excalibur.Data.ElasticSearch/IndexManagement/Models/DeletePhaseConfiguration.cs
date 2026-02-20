// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.IndexManagement;

/// <summary>
/// Configuration for the delete phase of index lifecycle management.
/// </summary>
public sealed class DeletePhaseConfiguration : PhaseConfiguration
{
	/// <summary>
	/// Gets a value indicating whether to wait for snapshot before deletion.
	/// </summary>
	/// <value> The snapshot policy to wait for before deletion. </value>
	public string? WaitForSnapshotPolicy { get; init; }
}
