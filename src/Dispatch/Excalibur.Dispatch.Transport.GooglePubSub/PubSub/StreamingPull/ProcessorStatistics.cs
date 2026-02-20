// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Statistics about the message processor.
/// </summary>
public sealed class ProcessorStatistics
{
	/// <summary>
	/// Gets or sets the number of queued messages.
	/// </summary>
	/// <value>
	/// The number of queued messages.
	/// </value>
	public int QueuedMessages { get; set; }

	/// <summary>
	/// Gets or sets the maximum queue capacity.
	/// </summary>
	/// <value>
	/// The maximum queue capacity.
	/// </value>
	public int MaxQueueCapacity { get; set; }

	/// <summary>
	/// Gets or sets the number of active processing threads.
	/// </summary>
	/// <value>
	/// The number of active processing threads.
	/// </value>
	public int ActiveProcessingThreads { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether gets or sets whether the processor is shutting down.
	/// </summary>
	/// <value>
	/// A value indicating whether gets or sets whether the processor is shutting down.
	/// </value>
	public bool IsShuttingDown { get; set; }
}
