// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

using Excalibur.Dispatch.Channels;

namespace Excalibur.Dispatch.Options.Channels;

/// <summary>
/// Options for creating a bounded high-performance channel.
/// </summary>
public sealed class BoundedDispatchChannelOptions : DispatchChannelOptions
{
	/// <summary>
	/// Initializes a new instance of the <see cref="BoundedDispatchChannelOptions" /> class with specified capacity.
	/// </summary>
	/// <param name="capacity"> The channel capacity. </param>
	public BoundedDispatchChannelOptions(int capacity)
	{
		Mode = ChannelMode.Bounded;
		Capacity = capacity;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="BoundedDispatchChannelOptions" /> class with default capacity.
	/// </summary>
	public BoundedDispatchChannelOptions()
	{
		Mode = ChannelMode.Bounded;
		Capacity = 100; // Default capacity
	}

	/// <summary>
	/// Gets or sets the wait strategy for the channel writer.
	/// </summary>
	/// <value>The current <see cref="WriterWaitStrategy"/> value.</value>
	public IWaitStrategy? WriterWaitStrategy { get; set; }

	/// <summary>
	/// Gets or sets the wait strategy for the channel reader.
	/// </summary>
	/// <value>The current <see cref="ReaderWaitStrategy"/> value.</value>
	public IWaitStrategy? ReaderWaitStrategy { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to use aggressive spinning.
	/// </summary>
	/// <value>The current <see cref="AggressiveSpinning"/> value.</value>
	public bool AggressiveSpinning { get; set; }

	/// <summary>
	/// Gets or sets the number of spin iterations before yielding.
	/// </summary>
	/// <value>The current <see cref="SpinCount"/> value.</value>
	[Range(1, int.MaxValue)]
	public int SpinCount { get; set; } = 100;
}
