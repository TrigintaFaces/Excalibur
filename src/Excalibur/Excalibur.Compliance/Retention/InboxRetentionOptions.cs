// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Compliance.Retention;

/// <summary>
/// Configuration for <see cref="InboxRetentionContributor"/> — how long processed inbox entries are
/// kept before <see cref="IRetentionEnforcementService"/> deletes them.
/// </summary>
public sealed class InboxRetentionOptions
{
	/// <summary>
	/// Gets or sets the number of days a processed inbox entry is retained before it becomes eligible
	/// for deletion. Default: 90. Set to 0 to disable inbox retention (the contributor reports zero
	/// cleaned on every pass, matching the "never claim success while deleting nothing" contract).
	/// </summary>
	public int RetentionDays { get; set; } = 90;
}
