// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Options.Middleware;

/// <summary>
/// Configuration options for contract version check middleware.
/// </summary>
public sealed class ContractVersionCheckOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether contract version checking is enabled.
	/// </summary>
	/// <value> Default is true. </value>
	public bool Enabled { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether explicit versions are required.
	/// </summary>
	/// <value> Default is false. </value>
	public bool RequireExplicitVersions { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to fail on incompatible versions.
	/// </summary>
	/// <value> Default is true. </value>
	public bool FailOnIncompatibleVersions { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether to fail on unknown versions.
	/// </summary>
	/// <value> Default is false. </value>
	public bool FailOnUnknownVersions { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to record deprecation metrics.
	/// </summary>
	/// <value> Default is true. </value>
	public bool RecordDeprecationMetrics { get; set; } = true;

	/// <summary>
	/// Gets or sets the header name configuration for version checking.
	/// </summary>
	/// <value> A <see cref="VersionCheckHeaders" /> instance with default header names. </value>
	public VersionCheckHeaders Headers { get; set; } = new();

	/// <summary>
	/// Gets or sets the list of supported versions.
	/// </summary>
	/// <value>The current <see cref="SupportedVersions"/> value.</value>
	public string[]? SupportedVersions { get; set; }

	/// <summary>
	/// Gets or sets message types that bypass version checking.
	/// </summary>
	/// <value>The current <see cref="BypassVersionCheckForTypes"/> value.</value>
	public string[]? BypassVersionCheckForTypes { get; set; }
}
