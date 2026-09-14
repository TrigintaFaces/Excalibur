// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

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
					var value = principal.FindFirstValue(claimType);
					if (!string.IsNullOrEmpty(value))
					{
						return value;
					}
				}
			}

			var ambient = TenantContextHolder.Current;

			return string.IsNullOrEmpty(ambient) ? _options.DefaultTenantId : ambient;
		}
	}

	/// <inheritdoc />
	public bool HasTenant => !string.IsNullOrEmpty(TenantId);
}
