// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.AspNetCore.Authorization;

namespace Excalibur.A3.AspNetCore;

/// <summary>
/// The requirement produced from a convention-based grant policy name, carrying the resource scope the
/// caller must be authorized for.
/// </summary>
/// <param name="activityName"> The grant activity the caller must hold. </param>
/// <param name="resourceType"> The resource type the activity applies to. </param>
/// <param name="resourceId">
/// A fixed resource identifier, or <see langword="null"/> when the requirement is either unscoped or
/// scoped by <paramref name="resourceIdRouteValue"/>.
/// </param>
/// <param name="resourceIdRouteValue">
/// The route parameter whose value supplies the resource identifier for this request, or
/// <see langword="null"/> when the resource is fixed or the requirement is unscoped.
/// </param>
internal sealed class GrantRequirement(
	string activityName,
	string resourceType,
	string? resourceId,
	string? resourceIdRouteValue) : IAuthorizationRequirement
{
	/// <summary>Gets the grant activity the caller must hold.</summary>
	public string ActivityName { get; } = activityName;

	/// <summary>Gets the resource type the activity applies to.</summary>
	public string ResourceType { get; } = resourceType;

	/// <summary>Gets the fixed resource identifier, or <see langword="null"/>.</summary>
	public string? ResourceId { get; } = resourceId;

	/// <summary>
	/// Gets the route parameter supplying the resource identifier, or <see langword="null"/>.
	/// </summary>
	public string? ResourceIdRouteValue { get; } = resourceIdRouteValue;
}
