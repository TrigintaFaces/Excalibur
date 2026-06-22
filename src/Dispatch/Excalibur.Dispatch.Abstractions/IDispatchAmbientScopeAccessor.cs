// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch;

/// <summary>
/// Exposes the ambient dependency-injection scope for the current logical operation, allowing the
/// singleton dispatch pipeline to resolve scoped message handlers from the correct scope rather than
/// from the root container.
/// </summary>
/// <remarks>
/// <para>
/// The dispatcher and its message bus are registered as singletons and therefore capture the
/// <b>root</b> <see cref="IServiceProvider"/>. Resolving a handler that was registered with
/// <see cref="Microsoft.Extensions.DependencyInjection.ServiceLifetime.Scoped"/> (or a handler whose
/// dependencies are scoped) directly from the root container throws
/// <see cref="InvalidOperationException"/> ("Cannot resolve scoped service '…' from root provider") —
/// the classic captive-dependency failure.
/// </para>
/// <para>
/// This accessor lets a hosting integration surface the <em>real</em> ambient scope (for example,
/// <c>HttpContext.RequestServices</c> in ASP.NET Core) so a scoped handler resolves from — and shares
/// state with — the active request scope. It mirrors the role of
/// <c>Microsoft.AspNetCore.Http.IHttpContextAccessor</c>: a pull-based ambient accessor with no
/// dependency on any specific host. When no implementation is registered, or when no ambient scope is
/// active (for example a background worker), the dispatcher falls back to creating a fresh scope via
/// <see cref="Microsoft.Extensions.DependencyInjection.IServiceScopeFactory"/>.
/// </para>
/// <para>
/// Implementations MUST be thread-safe and cheap to query; the property is consulted only when a
/// scoped handler is dispatched, never on the root-resolvable hot path.
/// </para>
/// </remarks>
public interface IDispatchAmbientScopeAccessor
{
    /// <summary>
    /// Gets the <see cref="IServiceProvider"/> for the ambient scope of the current operation, or
    /// <see langword="null"/> when no ambient scope is active and the dispatcher should create one.
    /// </summary>
    /// <value>
    /// The ambient scoped service provider (for example the active request scope), or
    /// <see langword="null"/> if none is available.
    /// </value>
    IServiceProvider? CurrentServiceProvider { get; }
}
