// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Application.Requests;

/// <summary>
/// Represents an entity that is associated with a specific tenant.
/// </summary>
public interface IAmMultiTenant
{
	/// <summary>
	/// Gets the tenant identifier associated with the entity.
	/// </summary>
	/// <value>
	/// The tenant identifier associated with the entity.
	/// </value>
	string? TenantId { get; }
}
