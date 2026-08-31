// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Compliance.Retention;

/// <summary>
/// Configuration for <see cref="OutboxRetentionContributor"/> — how long sent outbox messages are kept
/// before <see cref="IRetentionEnforcementService"/> deletes them.
/// </summary>
public sealed class OutboxRetentionOptions
{
	/// <summary>
	/// Gets or sets the number of days a sent outbox message is retained before it becomes eligible for
	/// deletion. Default: 90. Set to 0 to disable outbox retention (the contributor reports zero cleaned
	/// on every pass, matching the "never claim success while deleting nothing" contract).
	/// </summary>
	public int RetentionDays { get; set; } = 90;

	/// <summary>
	/// Gets or sets the maximum number of messages removed per underlying store call. The contributor
	/// repeats the call until a pass removes fewer than this many rows, so the total cleaned in one
	/// enforcement pass is not bounded by this value. Default: 500.
	/// </summary>
	public int BatchSize { get; set; } = 500;
}
