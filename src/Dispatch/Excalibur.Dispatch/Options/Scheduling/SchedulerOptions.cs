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
	/// Gets or sets a value indicating whether adaptive polling backoff is enabled when the scheduler is idle.
	/// </summary>
	public bool EnableAdaptivePolling { get; set; }

	/// <summary>
	/// Gets or sets the minimum polling interval used when adaptive polling is enabled and work is present.
	/// </summary>
	public TimeSpan MinPollingInterval { get; set; } = TimeSpan.FromSeconds(1);

	/// <summary>
	/// Gets or sets the multiplier used to increase polling delay when the scheduler is idle.
	/// </summary>
	public double AdaptivePollingBackoffMultiplier { get; set; } = 2.0;

	/// <summary>
	/// Gets or sets the jitter ratio applied to polling delay to reduce synchronized wake-ups.
	/// </summary>
	/// <remarks>
	/// A value of 0 disables jitter. A value of 0.1 applies up to +/-10% random jitter.
	/// </remarks>
	public double PollingJitterRatio { get; set; }

	/// <summary>
	/// Gets or sets determines how messages scheduled in the past are handled.
	/// </summary>
	/// <value>The current <see cref="PastScheduleBehavior"/> value.</value>
	public PastScheduleBehavior PastScheduleBehavior { get; set; } = PastScheduleBehavior.ExecuteImmediately;
}
