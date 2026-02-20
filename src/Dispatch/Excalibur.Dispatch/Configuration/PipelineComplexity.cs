// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Configuration;

/// <summary>
/// Defines the complexity levels for pipelines.
/// </summary>
public enum PipelineComplexity
{
	/// <summary>
	/// Standard pipeline with typical middleware overhead.
	/// </summary>
	Standard = 0,

	/// <summary>
	/// Reduced pipeline with fewer middleware components.
	/// </summary>
	Reduced = 1,

	/// <summary>
	/// Minimal pipeline with only essential middleware.
	/// </summary>
	Minimal = 2,

	/// <summary>
	/// Direct pipeline with zero middleware overhead.
	/// </summary>
	Direct = 3,
}
