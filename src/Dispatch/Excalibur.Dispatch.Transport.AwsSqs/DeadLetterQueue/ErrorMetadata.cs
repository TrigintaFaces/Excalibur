// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// Error metadata.
/// </summary>
public sealed class ErrorMetadata
{
	/// <summary>
	/// Gets or sets the error source.
	/// </summary>
	/// <value>
	/// The error source.
	/// </value>
	public string? Source { get; set; }

	/// <summary>
	/// Gets or sets the error category.
	/// </summary>
	/// <value>
	/// The error category.
	/// </value>
	public string? Category { get; set; }

	/// <summary>
	/// Gets additional properties.
	/// </summary>
	/// <value>
	/// Additional properties.
	/// </value>
	public Dictionary<string, object> Properties { get; } = [];
}
