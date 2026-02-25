// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

using Excalibur.Dispatch.Pooling.Configuration;

namespace Excalibur.Dispatch.Options.Pooling;

/// <summary>
/// Type-specific pool configuration.
/// </summary>
public sealed class TypePoolOptions
{
	/// <summary>
	/// Gets or sets the maximum pool size for this type.
	/// </summary>
	/// <value>
	/// The maximum pool size for this type.
	/// </value>
	[Range(0, 10000)]
	public int MaxPoolSize { get; set; } // 0 means use default

	/// <summary>
	/// Gets or sets a value indicating whether to enable pooling for this type.
	/// </summary>
	/// <value> The current <see cref="Enabled" /> value. </value>
	public bool Enabled { get; set; } = true;

	/// <summary>
	/// Gets or sets the reset strategy for this type.
	/// </summary>
	/// <value> The current <see cref="ResetStrategy" /> value. </value>
	public ResetStrategy ResetStrategy { get; set; } = ResetStrategy.Auto;

	/// <summary>
	/// Gets or sets a value indicating whether to pre-warm the pool.
	/// </summary>
	/// <value> The current <see cref="PreWarm" /> value. </value>
	public bool PreWarm { get; set; }

	/// <summary>
	/// Gets or sets the pre-warm count.
	/// </summary>
	/// <value>
	/// The pre-warm count.
	/// </value>
	[Range(0, 100)]
	public int PreWarmCount { get; set; }
}
