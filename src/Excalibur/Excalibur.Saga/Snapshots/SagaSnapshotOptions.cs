// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

namespace Excalibur.Saga.Snapshots;

/// <summary>
/// Configuration options for saga state snapshotting.
/// </summary>
public sealed class SagaSnapshotOptions
{
	/// <summary>
	/// Gets or sets the number of steps between automatic snapshots.
	/// Default: 10 steps.
	/// </summary>
	/// <value>The step interval for automatic snapshot creation. Must be at least 1.</value>
	[Range(1, int.MaxValue)]
	public int SnapshotInterval { get; set; } = 10;

	/// <summary>
	/// Gets or sets whether automatic snapshotting is enabled.
	/// Default: <see langword="true"/>.
	/// </summary>
	/// <value><see langword="true"/> to enable automatic snapshots; otherwise, <see langword="false"/>.</value>
	public bool EnableAutomaticSnapshots { get; set; } = true;

	/// <summary>
	/// Gets or sets the maximum number of snapshots to retain per saga instance.
	/// Older snapshots are deleted when this limit is exceeded.
	/// Default: 3.
	/// </summary>
	/// <value>The maximum snapshot retention count. Must be at least 1.</value>
	[Range(1, 100)]
	public int MaxRetainedSnapshots { get; set; } = 3;

	/// <summary>
	/// Gets or sets the retention period for snapshots after a saga completes.
	/// Default: 7 days.
	/// </summary>
	/// <value>The duration to retain snapshots after saga completion.</value>
	public TimeSpan RetentionPeriod { get; set; } = TimeSpan.FromDays(7);
}
