// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Options.Core;

/// <summary>
/// Configuration options for in-memory message bus.
/// </summary>
public sealed class InMemoryBusOptions
{
	/// <summary>
	/// Gets or sets the maximum number of messages to queue per channel.
	/// </summary>
	/// <value> Default is 1000. </value>
	public int MaxQueueLength { get; set; } = 1000;

	/// <summary>
	/// Gets or sets a value indicating whether to preserve message order.
	/// </summary>
	/// <value> Default is true. </value>
	public bool PreserveOrder { get; set; } = true;

	/// <summary>
	/// Gets or sets the processing delay simulation (for testing).
	/// </summary>
	/// <value> Default is zero (no delay). </value>
	public TimeSpan ProcessingDelay { get; set; } = TimeSpan.Zero;
}
