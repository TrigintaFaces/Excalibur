// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Represents a tenant identifier.
/// </summary>
public interface ITenantId
{
	/// <summary>
	/// Gets or sets the tenant identifier value.
	/// </summary>
	/// <value> The unique tenant identifier string. </value>
	string Value { get; set; }

	/// <summary>
	/// Returns the tenant identifier as a string.
	/// </summary>
	string ToString();
}
