// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Excalibur.A3.AspNetCore;

/// <summary>
/// The HTTP mode: the tenant claim of the request principal, falling back to the ambient tenant.
/// </summary>
/// <remarks>
/// It outranks the ambient mode because it covers it: with no request, or no tenant claim, it reads the same
/// ambient tenant the ambient mode would. The context it creates reads the request through the accessor on
/// every access and holds no per-request state, so one shared instance serves every request.
/// </remarks>
internal sealed class HttpContextTenantContextMode : ITenantContextMode
{
	/// <inheritdoc />
	public int Precedence => 2;

	/// <inheritdoc />
	public ITenantContext Create(IServiceProvider services) =>
		new HttpContextTenantContext(
			services.GetRequiredService<IHttpContextAccessor>(),
			services.GetRequiredService<IOptions<GrantAuthorizationHttpOptions>>());
}
