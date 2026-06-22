// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Hosting.AspNetCore;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers the ASP.NET Core ambient-scope integration for Dispatch.
/// </summary>
public static class DispatchAspNetCoreScopeExtensions
{
    /// <summary>
    /// Registers the ASP.NET Core <see cref="IDispatchAmbientScopeAccessor"/> so that scoped message
    /// handlers resolve from — and share state with — the active request scope
    /// (<see cref="HttpContext.RequestServices"/>) instead of a freshly created scope.
    /// </summary>
    /// <remarks>
    /// This is wired automatically by <c>WebApplicationBuilder.AddDispatch(...)</c>. Call it explicitly
    /// when composing Dispatch through a different entry point (for example <c>services.AddExcalibur(...)</c>)
    /// and you want request-scoped state shared with handlers. It is safe to call more than once
    /// (registrations use <c>TryAdd</c>). Without it, scoped handlers still work correctly — they are
    /// resolved from a fresh per-dispatch scope via <see cref="IServiceScopeFactory"/>.
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <returns>The same <paramref name="services"/> instance, to allow chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is <see langword="null"/>.</exception>
    public static IServiceCollection AddDispatchAmbientScope(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IHttpContextAccessor, HttpContextAccessor>();
        services.TryAddSingleton<IDispatchAmbientScopeAccessor, HttpContextAmbientScopeAccessor>();

        return services;
    }
}
