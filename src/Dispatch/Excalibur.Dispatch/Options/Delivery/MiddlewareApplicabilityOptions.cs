// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Options.Delivery;

/// <summary>
/// Configuration options for middleware applicability evaluation.
/// </summary>
public sealed class MiddlewareApplicabilityOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether to include middleware when applicability evaluation fails.
	/// </summary>
	/// <value> Default is false for fail-safe behavior. </value>
	public bool IncludeOnError { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to cache applicability results.
	/// </summary>
	/// <value> Default is true for performance. </value>
	public bool EnableCaching { get; set; } = true;
}
