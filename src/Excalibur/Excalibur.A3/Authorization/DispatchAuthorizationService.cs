// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Security.Claims;

using Microsoft.AspNetCore.Authorization;

using AuthorizationResult = Excalibur.Dispatch.Abstractions.AuthorizationResult;

namespace Excalibur.A3.Authorization;

internal sealed class DispatchAuthorizationService(IAuthorizationService inner) : IDispatchAuthorizationService
{
	/// <inheritdoc/>
	public async Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object? resource,
		params IAuthorizationRequirement[] requirements)
	{
		var result = await inner.AuthorizeAsync(user, resource, requirements).ConfigureAwait(false);
		return result.Succeeded ? AuthorizationResult.Success() : AuthorizationResult.Failed("Authorization failed");
	}

	/// <inheritdoc/>
	public async Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object? resource, string policyName)
	{
		var result = await inner.AuthorizeAsync(user, resource, policyName).ConfigureAwait(false);
		return result.Succeeded ? AuthorizationResult.Success() : AuthorizationResult.Failed("Authorization failed");
	}
}
