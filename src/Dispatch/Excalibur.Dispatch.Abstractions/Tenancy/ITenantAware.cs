// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Defines a contract for entities that can be associated with a specific tenant in multi-tenant scenarios.
/// </summary>
public interface ITenantAware
{
	/// <summary>
	/// Gets the tenant ID for the entity.
	/// </summary>
	string TenantId { get; }
}
