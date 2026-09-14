// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.AspNetCore.Authorization;

namespace Excalibur.A3.AspNetCore;

/// <summary>
/// Requires the caller to hold an Excalibur A3 grant for an activity, optionally narrowed to the single
/// resource named by a route parameter of the matched endpoint.
/// </summary>
/// <remarks>
/// <para>
/// This is the strongly-typed way to apply a grant policy, and it works on both hosting styles — an MVC
/// controller action carries it as an attribute, and a minimal API endpoint passes an instance to
/// <c>RequireAuthorization</c>:
/// </para>
/// <example>
/// <code>
/// [HttpGet("/orders/{id}")]
/// [RequireGrant("Read", "Order", "id")]
/// public IActionResult GetById(string id) => Ok(id);
///
/// app.MapGet("/orders/{id}", (string id) =&gt; Results.Ok(id))
///    .RequireAuthorization(new RequireGrantAttribute("Read", "Order", "id"));
/// </code>
/// </example>
/// <para>
/// It sets <see cref="AuthorizeAttribute.Policy"/> to the equivalent grant policy name, so it is exactly
/// the policy an endpoint would get from writing that name out by hand — without the chance of a typo in
/// a string that would otherwise only fail at runtime.
/// </para>
/// </remarks>
[AttributeUsage(
	AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Delegate,
	AllowMultiple = true,
	Inherited = true)]
public sealed class RequireGrantAttribute : AuthorizeAttribute
{
	/// <summary>
	/// Requires the activity against a resource type, without narrowing to a single resource.
	/// </summary>
	/// <param name="activityName"> The grant activity the caller must hold. </param>
	/// <param name="resourceType"> The resource type the activity applies to. </param>
	/// <exception cref="ArgumentException">
	/// Thrown when an argument is null, whitespace, or contains the policy-name separator.
	/// </exception>
	public RequireGrantAttribute(string activityName, string resourceType) =>
		Policy = GrantPolicyName.For(activityName, resourceType);

	/// <summary>
	/// Requires the activity against the single resource identified by a route parameter of the matched
	/// endpoint.
	/// </summary>
	/// <param name="activityName"> The grant activity the caller must hold. </param>
	/// <param name="resourceType"> The resource type the activity applies to. </param>
	/// <param name="resourceIdRouteValue">
	/// The name of the route parameter carrying the resource identifier — <c>id</c> for a route template
	/// of <c>/orders/{id}</c>.
	/// </param>
	/// <exception cref="ArgumentException">
	/// Thrown when an argument is null, whitespace, or contains the policy-name separator.
	/// </exception>
	public RequireGrantAttribute(string activityName, string resourceType, string resourceIdRouteValue) =>
		Policy = GrantPolicyName.ForRouteValue(activityName, resourceType, resourceIdRouteValue);
}
