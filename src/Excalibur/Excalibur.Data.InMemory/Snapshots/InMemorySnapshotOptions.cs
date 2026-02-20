// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

namespace Excalibur.Data.InMemory.Snapshots;

/// <summary>
/// Configuration options for the in-memory snapshot store.
/// </summary>
public sealed class InMemorySnapshotOptions
{
	/// <summary>
	/// Gets or sets the maximum number of snapshots to retain.
	/// </summary>
	/// <value>The maximum snapshot count. Zero means unlimited. Defaults to 10000.</value>
	[Range(0, int.MaxValue)]
	public int MaxSnapshots { get; set; } = 10000;

	/// <summary>
	/// Gets or sets the maximum number of snapshots to retain per aggregate.
	/// </summary>
	/// <value>The maximum snapshots per aggregate. Zero means unlimited. Defaults to 1 (only latest).</value>
	[Range(0, int.MaxValue)]
	public int MaxSnapshotsPerAggregate { get; set; } = 1;
}
