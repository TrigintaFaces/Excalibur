// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Delivery;

/// <summary>
/// Result of a deduplication check.
/// </summary>
public sealed class DeduplicationResult
{
	/// <summary>
	/// Gets or sets a value indicating whether the message is a duplicate.
	/// </summary>
	/// <value>The current <see cref="IsDuplicate"/> value.</value>
	public bool IsDuplicate { get; set; }

	/// <summary>
	/// Gets or sets when the message was first seen.
	/// </summary>
	/// <value>The current <see cref="FirstSeenAt"/> value.</value>
	public DateTimeOffset? FirstSeenAt { get; set; }

	/// <summary>
	/// Gets or sets the processor that first handled the message.
	/// </summary>
	/// <value>The current <see cref="ProcessedBy"/> value.</value>
	public string? ProcessedBy { get; set; }
}
