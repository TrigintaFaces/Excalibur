// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// Options for receiving messages from a queue.
/// </summary>
public sealed class ReceiveOptions
{
	/// <summary>
	/// Gets or sets the maximum number of messages to receive in a single request.
	/// </summary>
	public int? MaxNumberOfMessages { get; init; }

	/// <summary>
	/// Gets or sets the wait time for long polling.
	/// </summary>
	public TimeSpan? WaitTime { get; init; }

	/// <summary>
	/// Gets or sets the visibility timeout for received messages.
	/// </summary>
	public TimeSpan? VisibilityTimeout { get; init; }

	/// <summary>
	/// Gets or sets the message attribute names to retrieve.
	/// </summary>
	public List<string>? MessageAttributeNames { get; init; }

	/// <summary>
	/// Gets or sets the system attribute names to retrieve.
	/// </summary>
	public List<string>? AttributeNames { get; init; }
}
