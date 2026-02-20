// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Options.Validation;

/// <summary>
/// Configuration options for message versioning and contract management. Controls how message versions are validated and enforced
/// throughout the dispatch system.
/// </summary>
public sealed class VersioningOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether message versioning is enabled. When enabled, the system will validate message contracts and
	/// version compatibility.
	/// </summary>
	/// <value> <c> true </c> if versioning is enabled; otherwise, <c> false </c>. Default is <c> true </c>. </value>
	public bool Enabled { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether contract version headers are required on all messages. When enabled, messages without
	/// version information will be rejected.
	/// </summary>
	/// <value> <c> true </c> if contract versions are required; otherwise, <c> false </c>. Default is <c> true </c>. </value>
	public bool RequireContractVersion { get; set; } = true;
}
