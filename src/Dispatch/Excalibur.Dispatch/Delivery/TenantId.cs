// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Delivery;

/// <summary>
/// Implementation of tenant identifier for multi-tenant messaging scenarios. This class provides tenant isolation capabilities for SaaS
/// applications and multi-tenant architectures, ensuring proper message routing and security boundaries between different tenants.
/// </summary>
public sealed class TenantId : ITenantId
{
	/// <summary>
	/// Gets or sets the tenant identifier value that uniquely identifies a tenant in the system. This value is used for message routing,
	/// data isolation, and security enforcement in multi-tenant scenarios.
	/// </summary>
	/// <value>The current <see cref="Value"/> value.</value>
	public string Value { get; set; } = string.Empty;

	/// <summary>
	/// Returns the string representation of the tenant identifier. This method provides the tenant ID value for logging, routing, and
	/// security purposes.
	/// </summary>
	/// <returns> String representation of the tenant identifier value. </returns>
	public override string ToString() => Value;
}
