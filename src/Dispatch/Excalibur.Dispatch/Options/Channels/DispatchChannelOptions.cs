// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Threading.Channels;

using Excalibur.Dispatch.Channels;

namespace Excalibur.Dispatch.Options.Channels;

/// <summary>
/// Options for configuring a high-performance channel.
/// </summary>
public class DispatchChannelOptions
{
	/// <summary>
	/// Gets or sets the channel mode (bounded or unbounded).
	/// </summary>
	/// <value>The current <see cref="Mode"/> value.</value>
	public ChannelMode Mode { get; set; } = ChannelMode.Unbounded;

	/// <summary>
	/// Gets or sets the capacity for bounded channels.
	/// </summary>
	/// <value>The current <see cref="Capacity"/> value.</value>
	public int? Capacity { get; set; }

	/// <summary>
	/// Gets or sets the behavior when the channel is full (for bounded channels).
	/// </summary>
	/// <value>The current <see cref="FullMode"/> value.</value>
	public BoundedChannelFullMode FullMode { get; set; } = BoundedChannelFullMode.Wait;

	/// <summary>
	/// Gets or sets a value indicating whether the channel will have a single reader.
	/// </summary>
	/// <value>The current <see cref="SingleReader"/> value.</value>
	public bool SingleReader { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether the channel will have a single writer.
	/// </summary>
	/// <value>The current <see cref="SingleWriter"/> value.</value>
	public bool SingleWriter { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to allow synchronous continuations.
	/// </summary>
	/// <value>The current <see cref="AllowSynchronousContinuations"/> value.</value>
	public bool AllowSynchronousContinuations { get; set; } = true;

	/// <summary>
	/// Gets or sets the wait strategy to use.
	/// </summary>
	/// <value>The current <see cref="WaitStrategy"/> value.</value>
	public IWaitStrategy? WaitStrategy { get; set; }
}
