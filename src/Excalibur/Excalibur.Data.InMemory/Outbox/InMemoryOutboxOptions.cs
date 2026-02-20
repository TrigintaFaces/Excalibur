// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

namespace Excalibur.Data.InMemory.Outbox;

/// <summary>
/// Configuration options for the in-memory outbox store.
/// </summary>
public sealed class InMemoryOutboxOptions
{
	/// <summary>
	/// Gets or sets the maximum number of messages to retain.
	/// </summary>
	/// <value>The maximum message count. Zero means unlimited. Defaults to 10000.</value>
	[Range(0, int.MaxValue)]
	public int MaxMessages { get; set; } = 10000;

	/// <summary>
	/// Gets or sets the default retention period for sent messages.
	/// </summary>
	/// <value>The retention period. Defaults to 7 days.</value>
	public TimeSpan DefaultRetentionPeriod { get; set; } = TimeSpan.FromDays(7);
}
