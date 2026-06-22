// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.AspNetCore.Http;

namespace Excalibur.Dispatch.Hosting.AspNetCore;

/// <summary>
/// Surfaces the active ASP.NET Core request scope (<see cref="HttpContext.RequestServices"/>) as the
/// ambient dependency-injection scope, so the singleton dispatch pipeline resolves scoped handlers from —
/// and shares request-scoped state with — the current request rather than the root container.
/// </summary>
/// <param name="httpContextAccessor">Accessor for the current <see cref="HttpContext"/>.</param>
internal sealed class HttpContextAmbientScopeAccessor(IHttpContextAccessor httpContextAccessor)
    : IDispatchAmbientScopeAccessor
{
    /// <inheritdoc />
    public IServiceProvider? CurrentServiceProvider => httpContextAccessor.HttpContext?.RequestServices;
}
