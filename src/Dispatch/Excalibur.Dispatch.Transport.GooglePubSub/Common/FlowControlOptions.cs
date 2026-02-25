// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Flow control settings for Google Cloud Pub/Sub.
/// </summary>
public sealed class FlowControlOptions
{
	/// <summary>
	/// Gets or sets the maximum number of outstanding messages.
	/// </summary>
	/// <value>
	/// The maximum number of outstanding messages.
	/// </value>
	public long MaxOutstandingMessages { get; set; } = 1000;

	/// <summary>
	/// Gets or sets the maximum bytes of outstanding messages.
	/// </summary>
	/// <value>
	/// The maximum bytes of outstanding messages.
	/// </value>
	public long MaxOutstandingBytes { get; set; } = 100_000_000; // 100MB

	/// <summary>
	/// Gets or sets a value indicating whether to limit exceeded behavior.
	/// </summary>
	/// <value>
	/// A value indicating whether to limit exceeded behavior.
	/// </value>
	public bool LimitExceededBehavior { get; set; } = true;
}
