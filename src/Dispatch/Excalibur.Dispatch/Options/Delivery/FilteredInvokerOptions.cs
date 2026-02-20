// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

namespace Excalibur.Dispatch.Options.Delivery;

/// <summary>
/// Configuration options for filtered middleware invoker.
/// </summary>
public sealed class FilteredInvokerOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether to cache filtered middleware arrays by message kind.
	/// </summary>
	/// <value> Default is true for performance. </value>
	public bool EnableCaching { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether to include middleware when filtering fails.
	/// </summary>
	/// <value> Default is false for fail-safe behavior. </value>
	public bool IncludeMiddlewareOnFilterError { get; set; }

	/// <summary>
	/// Gets or sets the maximum number of cached middleware arrays.
	/// </summary>
	/// <value> Default is 64 (covers most message kind combinations). </value>
	[Range(1, int.MaxValue)]
	public int MaxCachedEntries { get; set; } = 64;
}
