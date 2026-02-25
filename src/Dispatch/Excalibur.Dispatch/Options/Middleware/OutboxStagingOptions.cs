// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Options.Middleware;

/// <summary>
/// Configuration options for outbox staging middleware.
/// </summary>
public sealed class OutboxStagingOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether outbox staging is enabled.
	/// </summary>
	/// <value> Default is true. </value>
	public bool Enabled { get; set; } = true;

	/// <summary>
	/// Gets or sets the maximum number of outbound messages to stage per processing operation.
	/// </summary>
	/// <value> Default is 100. </value>
	public int MaxOutboundMessagesPerOperation { get; set; } = 100;

	/// <summary>
	/// Gets or sets a value indicating whether to compress message data in the outbox.
	/// </summary>
	/// <value> Default is false. </value>
	public bool CompressMessageData { get; set; }

	/// <summary>
	/// Gets or sets message types that bypass outbox staging.
	/// </summary>
	/// <value> The current <see cref="BypassOutboxForTypes" /> value. </value>
	public string[]? BypassOutboxForTypes { get; set; }
}
