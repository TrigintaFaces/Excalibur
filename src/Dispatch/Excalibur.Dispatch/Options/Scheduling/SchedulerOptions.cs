// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0



using Excalibur.Dispatch.Delivery;

namespace Excalibur.Dispatch.Options.Scheduling;

/// <summary>
/// Options controlling the <see cref="ScheduledMessageService" /> polling behaviour.
/// </summary>
public sealed class SchedulerOptions
{
	/// <summary>
	/// Gets or sets how often the scheduler checks for messages to dispatch.
	/// </summary>
	/// <value>
	/// How often the scheduler checks for messages to dispatch.
	/// </value>
	public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(30);

	/// <summary>
	/// Gets or sets determines how messages scheduled in the past are handled.
	/// </summary>
	/// <value>The current <see cref="PastScheduleBehavior"/> value.</value>
	public PastScheduleBehavior PastScheduleBehavior { get; set; } = PastScheduleBehavior.ExecuteImmediately;
}
