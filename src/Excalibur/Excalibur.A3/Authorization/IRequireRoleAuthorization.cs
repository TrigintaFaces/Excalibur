// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.A3.Authorization;

/// <summary>
/// Interface for messages that require role-based authorization.
/// </summary>
public interface IRequireRoleAuthorization : IRequireAuthorization
{
	/// <summary>
	/// Gets list of required roles (e.g., "Manager", "Clerk").
	/// </summary>
	/// <value>The required roles, or <see langword="null"/> if not available.</value>
	IReadOnlyCollection<string>? RequiredRoles { get; }
}
