// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Security.Claims;

using Excalibur.Dispatch;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Excalibur.A3.AspNetCore;

/// <summary>
/// Resolves the ambient tenant for an HTTP request from the authenticated principal, falling back to
/// the ambient tenant scope and then to a configured single-tenant default.
/// </summary>
/// <remarks>
/// <para>
/// <b>Resolution order.</b> The authenticated principal's tenant claim is consulted first, because it is
/// the only source the request itself authenticated; an ambient scope is process-local state that a
/// surrounding operation may have left behind, and preferring it would let a leaked scope decide which
/// tenant's rows a request may reach. When the principal carries no tenant claim the ambient scope
/// established by <see cref="TenantContextHolder.BeginScope"/> is used, which keeps non-HTTP flows and
/// hosts that resolve tenancy from a host header working unchanged. When neither yields a tenant,
/// <see cref="GrantAuthorizationHttpOptions.DefaultTenantId"/> applies, and when that is unset no tenant
/// is resolved and authorization fails closed.
/// </para>
/// <para>
/// <b>Invariant.</b> Repeated reads within one request return the same value: the principal is fixed for
/// the lifetime of the request and the options snapshot is captured at construction, so neither
/// <see cref="TenantId"/> nor <see cref="HasTenant"/> can change under a flow already in progress.
/// <see cref="HasTenant"/> is <see langword="true"/> exactly when <see cref="TenantId"/> is a
/// non-<see langword="null"/>, non-empty identifier.
/// </para>
/// </remarks>
internal sealed class HttpContextTenantContext(
	IHttpContextAccessor httpContextAccessor,
	IOptions<GrantAuthorizationHttpOptions> options) : ITenantContext
{
	private readonly GrantAuthorizationHttpOptions _options = options.Value;

	/// <inheritdoc />
	public string? TenantId
	{
		get
		{
			var principal = httpContextAccessor.HttpContext?.User;

			if (principal is not null)
			{
				foreach (var claimType in _options.TenantIdClaimTypes)
				{
					// Whitespace is not a tenant. A claim carrying only blanks is an absent tenant wearing
					// a non-empty string, and accepting it here would resolve a tenant the conversion
					// then refuses -- on a value that arrives from outside.
					var value = principal.FindFirstValue(claimType);
					if (!string.IsNullOrWhiteSpace(value))
					{
						return value;
					}
				}
			}

			var ambient = TenantContextHolder.Current;

			return string.IsNullOrWhiteSpace(ambient) ? _options.DefaultTenantId : ambient;
		}
	}

	/// <inheritdoc />
	/// <remarks>
	/// The predicate is the conversion's predicate. Spelled as a non-empty test it reports a tenant for a
	/// blank claim value and then throws when that value is converted — in the branch this property
	/// vouched for, on input that arrives from outside the process.
	/// </remarks>
	public bool HasTenant => !string.IsNullOrWhiteSpace(TenantId);
}
