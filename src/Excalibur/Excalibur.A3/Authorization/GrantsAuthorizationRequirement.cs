// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Microsoft.AspNetCore.Authorization;

namespace Excalibur.A3.Authorization;

/// <summary>
/// Authorization requirement that validates user permissions based on grants for specific activities and resources.
/// </summary>
/// <param name="activityName">The name of the activity being authorized.</param>
/// <param name="resourceTypes">The types of resources that can be accessed.</param>
/// <param name="resourceId">The optional specific resource identifier being accessed.</param>
public sealed class GrantsAuthorizationRequirement(string activityName, string[] resourceTypes, string? resourceId = null)
	: IAuthorizationRequirement
{
	/// <summary>
	/// Gets the name of the activity being authorized.
	/// </summary>
	/// <value>The name of the activity being authorized.</value>
	public string ActivityName { get; } = activityName;

	/// <summary>
	/// Gets the types of resources that can be accessed with this authorization.
	/// </summary>
	/// <value>The array of resource types that can be accessed with this authorization.</value>
	public string[] ResourceTypes { get; } = resourceTypes;

	/// <summary>
	/// Gets the optional specific resource identifier being accessed.
	/// </summary>
	/// <value>The specific resource identifier, or <see langword="null"/> if not specified.</value>
	public string? ResourceId { get; } = resourceId;
}
