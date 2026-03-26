// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.EventSourcing.Snapshots;

/// <summary>
/// Configuration options for incremental snapshot behavior.
/// </summary>
internal sealed class IncrementalSnapshotOptions
{
	/// <summary>
	/// Gets or sets the number of deltas before a full snapshot compaction is triggered.
	/// Default is 10 (R27.64).
	/// </summary>
	public int CompactionThreshold { get; set; } = 10;
}
