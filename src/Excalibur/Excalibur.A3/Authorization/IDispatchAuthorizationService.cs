// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Security.Claims;

using Microsoft.AspNetCore.Authorization;

using AuthorizationResult = Excalibur.Dispatch.Abstractions.AuthorizationResult;

namespace Excalibur.A3.Authorization;

/// <summary>
/// Service for authorizing users against specific requirements and policies in the dispatch system.
/// </summary>
public interface IDispatchAuthorizationService
{
	/// <summary>
	/// Authorizes a user against a set of authorization requirements.
	/// </summary>
	/// <param name="user"> The claims principal representing the user. </param>
	/// <param name="resource"> The optional resource being accessed. </param>
	/// <param name="requirements"> The authorization requirements to evaluate. </param>
	/// <returns> The authorization result indicating success or failure. </returns>
	Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object? resource,
		params IAuthorizationRequirement[] requirements);

	/// <summary>
	/// Authorizes a user against a named authorization policy.
	/// </summary>
	/// <param name="user"> The claims principal representing the user. </param>
	/// <param name="resource"> The optional resource being accessed. </param>
	/// <param name="policyName"> The name of the authorization policy to evaluate. </param>
	/// <returns> The authorization result indicating success or failure. </returns>
	Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object? resource, string policyName);
}
