// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;

namespace Excalibur.Dispatch.Observability.Sampling;

/// <summary>
/// Determines whether a trace should be sampled (recorded and exported).
/// </summary>
/// <remarks>
/// Implementations apply sampling logic based on the configured <see cref="TraceSamplerOptions"/>.
/// The sampler is consulted during the trace context propagation phase.
/// </remarks>
public interface ITraceSampler
{
	/// <summary>
	/// Determines whether the given activity should be sampled.
	/// </summary>
	/// <param name="context">The activity context containing trace/span IDs and flags.</param>
	/// <param name="name">The name of the activity being created.</param>
	/// <returns><see langword="true"/> if the activity should be sampled; otherwise, <see langword="false"/>.</returns>
	bool ShouldSample(ActivityContext context, string name);
}
