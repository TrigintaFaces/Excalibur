// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

namespace Excalibur.Dispatch.Options.Performance;

/// <summary>
/// Options for micro-batching.
/// </summary>
public sealed class MicroBatchOptions
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
}
