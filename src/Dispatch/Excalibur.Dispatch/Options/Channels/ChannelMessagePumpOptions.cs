// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;
using System.Threading.Channels;

namespace Excalibur.Dispatch.Options.Channels;

/// <summary>
/// Configuration options for a channel message pump.
/// </summary>
public sealed class ChannelMessagePumpOptions
{
	/// <summary>
	/// Gets or sets the channel capacity. Default is 100.
	/// </summary>
	/// <value>The current <see cref="Capacity"/> value.</value>
	[Range(1, int.MaxValue)]
	public int Capacity { get; set; } = 100;

	/// <summary>
	/// Gets or sets the behavior when the channel is full.
	/// </summary>
	/// <value>The current <see cref="FullMode"/> value.</value>
	public BoundedChannelFullMode FullMode { get; set; } = BoundedChannelFullMode.Wait;

	/// <summary>
	/// Gets or sets a value indicating whether to allow synchronous continuations.
	/// </summary>
	/// <value>The current <see cref="AllowSynchronousContinuations"/> value.</value>
	public bool AllowSynchronousContinuations { get; set; }

	/// <summary>
	/// Gets or sets the number of concurrent consumers. Default is 1.
	/// </summary>
	/// <value>The current <see cref="ConcurrentConsumers"/> value.</value>
	[Range(1, int.MaxValue)]
	public int ConcurrentConsumers { get; set; } = 1;

	/// <summary>
	/// Gets or sets a value indicating whether single reader mode is enabled for optimization.
	/// </summary>
	/// <value>The current <see cref="SingleReader"/> value.</value>
	public bool SingleReader { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether single writer mode is enabled for optimization.
	/// </summary>
	/// <value>The current <see cref="SingleWriter"/> value.</value>
	public bool SingleWriter { get; set; }

	/// <summary>
	/// Gets or sets the batch size for fetching messages (if supported). Default is 10.
	/// </summary>
	/// <value>The current <see cref="BatchSize"/> value.</value>
	[Range(1, int.MaxValue)]
	public int BatchSize { get; set; } = 10;

	/// <summary>
	/// Gets or sets the timeout for batch operations in milliseconds. Default is 1000ms.
	/// </summary>
	/// <value>The current <see cref="BatchTimeoutMs"/> value.</value>
	[Range(1, int.MaxValue)]
	public int BatchTimeoutMs { get; set; } = 1000;

	/// <summary>
	/// Gets or sets a value indicating whether to enable metrics collection.
	/// </summary>
	/// <value>The current <see cref="EnableMetrics"/> value.</value>
	public bool EnableMetrics { get; set; } = true;

	/// <summary>
	/// Gets or sets the prefetch count for message sources that support it.
	/// </summary>
	/// <value>The current <see cref="PrefetchCount"/> value.</value>
	[Range(1, int.MaxValue)]
	public int PrefetchCount { get; set; } = 20;
}
