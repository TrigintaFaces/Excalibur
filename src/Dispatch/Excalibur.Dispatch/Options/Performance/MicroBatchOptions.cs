// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using System.ComponentModel.DataAnnotations;

namespace Excalibur.Dispatch.Options.Performance;

/// <summary>
/// The values the batch processor is constructed with.
/// </summary>
/// <remarks>
/// This is a constructor argument, not a configuration surface: nothing binds it from configuration and
/// its only consumer is the batch processor. Batching is configured through the batching middleware's
/// options, which are bound and validated at host start. Keeping this type internal is what stops the two
/// from being mistaken for each other.
/// </remarks>
internal sealed class MicroBatchOptions
{
	/// <summary>
	/// Gets or sets maximum items in a batch.
	/// </summary>
	/// <value>The current <see cref="MaxBatchSize"/> value.</value>
	[Range(1, int.MaxValue)]
	public int MaxBatchSize { get; set; } = 100;

	/// <summary>
	/// Gets or sets maximum delay before flushing a batch.
	/// </summary>
	/// <value>
	/// Maximum delay before flushing a batch.
	/// </value>
	public TimeSpan MaxBatchDelay { get; set; } = TimeSpan.FromMilliseconds(100);

	/// <summary>
	/// Gets or sets the maximum number of pending items in the input channel.
	/// When the channel is full, producers block until space is available (backpressure).
	/// </summary>
	/// <value>The bounded channel capacity. Default is 10,000.</value>
	[Range(1, int.MaxValue)]
	public int ChannelCapacity { get; set; } = 10_000;
}
