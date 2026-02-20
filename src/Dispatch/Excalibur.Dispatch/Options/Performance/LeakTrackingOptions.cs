// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

namespace Excalibur.Dispatch.Options.Performance;

/// <summary>
/// Options for leak tracking.
/// </summary>
public sealed class LeakTrackingOptions
{
	/// <summary>
	/// Gets or sets maximum number of objects to retain in pool.
	/// </summary>
	/// <value>The current <see cref="MaximumRetained"/> value.</value>
	[Range(1, int.MaxValue)]
	public int MaximumRetained { get; set; } = Environment.ProcessorCount * 2;

	/// <summary>
	/// Gets or sets minimum number of objects to pre-create.
	/// </summary>
	/// <value>The current <see cref="MinimumRetained"/> value.</value>
	[Range(0, int.MaxValue)]
	public int MinimumRetained { get; set; } = Environment.ProcessorCount;

	/// <summary>
	/// Gets or sets timeout before considering an object leaked.
	/// </summary>
	/// <value>
	/// Timeout before considering an object leaked.
	/// </value>
	public TimeSpan LeakTimeout { get; set; } = TimeSpan.FromMinutes(5);

	/// <summary>
	/// Gets or sets interval for leak detection checks.
	/// </summary>
	/// <value>
	/// Interval for leak detection checks.
	/// </value>
	public TimeSpan LeakDetectionInterval { get; set; } = TimeSpan.FromSeconds(30);

	/// <summary>
	/// Gets or sets a value indicating whether to track stack traces for debugging.
	/// </summary>
	/// <value>The current <see cref="TrackStackTraces"/> value.</value>
	public bool TrackStackTraces { get; set; }
}
