// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

namespace Excalibur.Dispatch.Options.Channels;

/// <summary>
/// Options for configuring spin wait behavior.
/// </summary>
public sealed class SpinWaitOptions
{
	/// <summary>
	/// Gets or sets the number of times to spin before yielding.
	/// </summary>
	/// <value>The current <see cref="SpinCount"/> value.</value>
	[Range(1, int.MaxValue)]
	public int SpinCount { get; set; } = 10;

	/// <summary>
	/// Gets or sets the delay in milliseconds between spins after yielding.
	/// </summary>
	/// <value>The current <see cref="DelayMilliseconds"/> value.</value>
	[Range(0, int.MaxValue)]
	public int DelayMilliseconds { get; set; } = 1;

	/// <summary>
	/// Gets or sets a value indicating whether to use aggressive spinning.
	/// </summary>
	/// <value>The current <see cref="AggressiveSpin"/> value.</value>
	public bool AggressiveSpin { get; set; }

	/// <summary>
	/// Gets or sets the number of spin iterations per cycle.
	/// </summary>
	/// <value>The current <see cref="SpinIterations"/> value.</value>
	[Range(1, int.MaxValue)]
	public int SpinIterations { get; set; } = 100;

	/// <summary>
	/// Gets or sets the maximum number of spin cycles.
	/// </summary>
	/// <value>The current <see cref="MaxSpinCycles"/> value.</value>
	[Range(1, int.MaxValue)]
	public int MaxSpinCycles { get; set; } = 10;
}
