// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

namespace Excalibur.Dispatch.Observability.Sampling;

/// <summary>
/// Configuration options for trace sampling.
/// </summary>
public sealed class TraceSamplerOptions
{
	/// <summary>
	/// Gets or sets the sampling ratio (0.0 to 1.0).
	/// Only used when <see cref="Strategy"/> is <see cref="SamplingStrategy.RatioBased"/>
	/// or as the fallback for <see cref="SamplingStrategy.ParentBased"/> root spans.
	/// </summary>
	/// <value>A value between 0.0 (no sampling) and 1.0 (sample everything). Defaults to 1.0.</value>
	[Range(0.0, 1.0)]
	public double SamplingRatio { get; set; } = 1.0;

	/// <summary>
	/// Gets or sets the sampling strategy to use.
	/// </summary>
	/// <value>The sampling strategy. Defaults to <see cref="SamplingStrategy.AlwaysOn"/>.</value>
	public SamplingStrategy Strategy { get; set; } = SamplingStrategy.AlwaysOn;
}
