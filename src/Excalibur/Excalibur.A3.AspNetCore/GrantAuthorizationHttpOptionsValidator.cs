// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.A3.AspNetCore;

/// <summary>
/// Refuses a claim configuration that cannot identify a caller.
/// </summary>
/// <remarks>
/// Both lists are the bridge's only means of reading an identity out of the principal ASP.NET Core has
/// already validated. An empty list is not a permissive setting: nothing matches, no user or tenant is
/// resolved, and authorization then fails closed on every request. That reads to an operator as a broken
/// grant store rather than a misconfigured claim mapping, so it is refused at start-up where the message
/// can say which list is empty.
///
/// This is a real invariant rather than a validator written to satisfy a gate — an empty collection is a
/// value the type can hold and the framework cannot act on.
/// </remarks>
internal sealed class GrantAuthorizationHttpOptionsValidator : IValidateOptions<GrantAuthorizationHttpOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, GrantAuthorizationHttpOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var failures = new List<string>();

		if (options.UserIdClaimTypes.Count == 0)
		{
			failures.Add(
				$"{nameof(GrantAuthorizationHttpOptions.UserIdClaimTypes)} is empty, so no claim can identify the "
				+ "caller and every request would be denied. Name at least one claim type that carries the user id.");
		}

		if (options.TenantIdClaimTypes.Count == 0 && options.DefaultTenantId is null)
		{
			failures.Add(
				$"{nameof(GrantAuthorizationHttpOptions.TenantIdClaimTypes)} is empty and no "
				+ $"{nameof(GrantAuthorizationHttpOptions.DefaultTenantId)} is set, so no tenant can be resolved and "
				+ "every request would be denied. Name at least one claim type that carries the tenant id, or set a "
				+ "default tenant for a single-tenant host.");
		}

		return failures.Count > 0
			? ValidateOptionsResult.Fail(failures)
			: ValidateOptionsResult.Success;
	}
}
