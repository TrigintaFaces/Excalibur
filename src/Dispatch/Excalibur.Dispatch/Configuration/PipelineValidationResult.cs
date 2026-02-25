// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.ObjectModel;

namespace Excalibur.Dispatch.Configuration;

/// <summary>
/// Represents the result of pipeline validation.
/// </summary>
public sealed class PipelineValidationResult
{
	/// <summary>
	/// Gets or sets a value indicating whether the pipeline is optimized for high-performance scenarios.
	/// </summary>
	/// <value>The current <see cref="IsOptimized"/> value.</value>
	public bool IsOptimized { get; set; }

	/// <summary>
	/// Gets or sets the complexity level of the pipeline.
	/// </summary>
	/// <value>The current <see cref="Complexity"/> value.</value>
	public PipelineComplexity Complexity { get; set; }

	/// <summary>
	/// Gets the validation notes and recommendations.
	/// </summary>
	/// <value>The current <see cref="Notes"/> value.</value>
	public Collection<string> Notes { get; } = [];
}
