// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

namespace Excalibur.A3.Abstractions.Authorization;

/// <summary>
/// Configuration options for role management.
/// </summary>
public sealed class RoleOptions
{
	/// <summary>
	/// Gets or sets the maximum allowed depth for role hierarchy inheritance.
	/// </summary>
	/// <value>Defaults to 5.</value>
	[Range(1, 10)]
	public int MaxHierarchyDepth { get; set; } = 5;

	/// <summary>
	/// Gets or sets a value indicating whether role names must be unique within a tenant.
	/// </summary>
	/// <value>Defaults to <see langword="true"/>.</value>
	public bool EnforceUniqueNames { get; set; } = true;

	/// <summary>
	/// Gets or sets the duration (in seconds) for which resolved role permissions
	/// are cached by the role-aware authorization evaluator.
	/// </summary>
	/// <value>Defaults to 300 (5 minutes).</value>
	[Range(0, 86400)]
	public int PermissionCacheDurationSeconds { get; set; } = 300;
}
