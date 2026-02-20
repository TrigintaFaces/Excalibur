// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Saga.Correlation;

/// <summary>
/// Configuration options for multi-property saga correlation.
/// </summary>
public sealed class MultiPropertyCorrelationOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether all properties must match
	/// for the correlation to succeed.
	/// </summary>
	/// <value>
	/// <see langword="true"/> to require all property values to be non-null
	/// for correlation; otherwise, <see langword="false"/> to allow partial matches.
	/// Default is <see langword="true"/>.
	/// </value>
	public bool RequireAllProperties { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether to combine property values
	/// into a single composite key.
	/// </summary>
	/// <value>
	/// <see langword="true"/> to produce a composite key by combining property
	/// values with a separator; otherwise, <see langword="false"/> to match
	/// against each property independently. Default is <see langword="true"/>.
	/// </value>
	/// <remarks>
	/// When <see langword="true"/>, the composite key is formed by joining
	/// property values with a pipe separator (e.g., "OrderId|CustomerId").
	/// </remarks>
	public bool UseCompositeKey { get; set; } = true;
}
