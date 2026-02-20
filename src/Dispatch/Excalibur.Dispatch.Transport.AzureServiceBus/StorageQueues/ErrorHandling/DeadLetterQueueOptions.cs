// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Azure;

/// <summary>
/// Configuration options for dead letter queue handling.
/// </summary>
public sealed class DeadLetterQueueOptions
{
	/// <summary>
	/// Gets or sets the maximum number of times a message can be dequeued before being dead lettered.
	/// </summary>
	/// <value>
	/// The maximum number of times a message can be dequeued before being dead lettered.
	/// </value>
	public int MaxDequeueCount { get; set; } = 5;

	/// <summary>
	/// Gets or sets the maximum number of retry attempts before dead lettering.
	/// </summary>
	/// <value>
	/// The maximum number of retry attempts before dead lettering.
	/// </value>
	public int MaxRetryAttempts { get; set; } = 3;

	/// <summary>
	/// Gets or sets the maximum age a message can have before being dead lettered.
	/// </summary>
	/// <value>
	/// The maximum age a message can have before being dead lettered.
	/// </value>
	public TimeSpan MaxMessageAge { get; set; } = TimeSpan.FromDays(1);

	/// <summary>
	/// Gets or sets the maximum size of the dead letter queue before it's considered unhealthy.
	/// </summary>
	/// <value>
	/// The maximum size of the dead letter queue before it's considered unhealthy.
	/// </value>
	public long MaxDeadLetterQueueSize { get; set; } = 1000;
}
