// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// Contains statistics about long polling receiver operations.
/// </summary>
public sealed class ReceiverStatistics
{
	/// <summary>
	/// Gets or sets the total number of receive operations performed.
	/// </summary>
	public required long TotalReceiveOperations { get; init; }

	/// <summary>
	/// Gets or sets the total number of messages received.
	/// </summary>
	public required long TotalMessagesReceived { get; init; }

	/// <summary>
	/// Gets or sets the total number of messages deleted.
	/// </summary>
	public required long TotalMessagesDeleted { get; init; }

	/// <summary>
	/// Gets or sets the number of visibility timeout optimizations performed.
	/// </summary>
	public required long VisibilityTimeoutOptimizations { get; init; }

	/// <summary>
	/// Gets or sets the timestamp of the last receive operation.
	/// </summary>
	public required DateTimeOffset LastReceiveTime { get; init; }

	/// <summary>
	/// Gets or sets the current polling status.
	/// </summary>
	public required PollingStatus PollingStatus { get; init; }
}
