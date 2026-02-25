// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

namespace Excalibur.EventSourcing.Snapshots;

/// <summary>
/// Configuration options for snapshot upgrading.
/// </summary>
public sealed class SnapshotUpgradingOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether automatic snapshot upgrading is enabled
	/// during aggregate hydration from snapshots.
	/// </summary>
	/// <value>True to enable automatic upgrading; false otherwise. Default is true.</value>
	public bool EnableAutoUpgradeOnLoad { get; set; } = true;

	/// <summary>
	/// Gets or sets the current snapshot version that aggregates should target.
	/// </summary>
	/// <value>The target snapshot version. Default is 1.</value>
	[Range(1, int.MaxValue)]
	public int CurrentSnapshotVersion { get; set; } = 1;
}
