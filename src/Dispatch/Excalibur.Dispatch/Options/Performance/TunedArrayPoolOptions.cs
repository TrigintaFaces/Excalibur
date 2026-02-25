// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

namespace Excalibur.Dispatch.Options.Performance;

/// <summary>
/// Options for tuned ArrayPool configuration.
/// </summary>
public sealed class TunedArrayPoolOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether to pre-warm pools on initialization.
	/// </summary>
	/// <value>The current <see cref="PreWarmPools"/> value.</value>
	public bool PreWarmPools { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether to clear arrays on return (security versus performance trade-off).
	/// </summary>
	/// <value>The current <see cref="ClearOnReturn"/> value.</value>
	public bool ClearOnReturn { get; set; }

	/// <summary>
	/// Gets or sets the maximum arrays per bucket for each pool.
	/// </summary>
	/// <value>The current <see cref="MaxArraysPerBucket"/> value.</value>
	[Range(1, int.MaxValue)]
	public int MaxArraysPerBucket { get; set; } = 50;
}
