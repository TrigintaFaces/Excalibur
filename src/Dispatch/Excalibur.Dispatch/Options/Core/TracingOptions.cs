// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

namespace Excalibur.Dispatch.Options.Core;

/// <summary>
/// Configuration options for distributed tracing.
/// </summary>
public sealed class TracingOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether distributed tracing is enabled.
	/// </summary>
	/// <value> Default is false. </value>
	public bool Enabled { get; set; }

	/// <summary>
	/// Gets or sets the trace sampling ratio (0.0 to 1.0).
	/// </summary>
	/// <value> Default is 1.0 (sample all traces). </value>
	[Range(0.0, 1.0)]
	public double SamplingRatio { get; set; } = 1.0;

	/// <summary>
	/// Gets or sets a value indicating whether to include sensitive data in trace spans.
	/// </summary>
	/// <value> Default is false. </value>
	public bool IncludeSensitiveData { get; set; }
}
