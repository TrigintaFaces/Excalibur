// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Observability.Sampling;

/// <summary>
/// Defines the trace sampling strategies available.
/// </summary>
public enum SamplingStrategy
{
	/// <summary>
	/// All traces are sampled (recorded).
	/// </summary>
	AlwaysOn = 0,

	/// <summary>
	/// No traces are sampled (all dropped).
	/// </summary>
	AlwaysOff = 1,

	/// <summary>
	/// Traces are sampled based on a configured ratio (0.0 to 1.0).
	/// </summary>
	RatioBased = 2,

	/// <summary>
	/// Sampling decision is inherited from the parent trace context.
	/// Falls back to ratio-based sampling for root spans.
	/// </summary>
	ParentBased = 3,
}
