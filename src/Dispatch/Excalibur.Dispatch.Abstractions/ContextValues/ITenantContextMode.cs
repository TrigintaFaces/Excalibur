// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Dispatch;

/// <summary>
/// One way of resolving the current tenant, contributed by the package that provides it.
/// </summary>
/// <remarks>
/// <para>
/// Several packages can each supply a tenant context: the single-tenant default, the ambient context, and the
/// HTTP context that reads the request principal. Each contributes a mode with
/// <see cref="Microsoft.Extensions.DependencyInjection.DefaultTenantContextServiceCollectionExtensions.AddTenantContextMode{TMode}"/>,
/// and <see cref="ITenantContext"/> resolves to the mode with the highest <see cref="Precedence"/>. The order
/// in which modes are registered does not matter.
/// </para>
/// <para>
/// A mode of higher precedence is expected to cover what the lower ones cover: the HTTP mode falls back to the
/// ambient tenant when there is no request, so registering it also serves background work. Two different
/// modes with the same precedence are refused, at startup and on first resolution, rather than one being
/// chosen silently.
/// </para>
/// </remarks>
public interface ITenantContextMode
{
	/// <summary>
	/// Gets the precedence of this mode. The highest precedence present is the one that resolves.
	/// </summary>
	/// <value>The single-tenant default is 0, the ambient context 1 and the HTTP context 2.</value>
	int Precedence { get; }

	/// <summary>
	/// Creates the tenant context this mode provides.
	/// </summary>
	/// <param name="services">The root service provider.</param>
	/// <returns>
	/// The tenant context. It is resolved once and shared, so it must read the current tenant on each access
	/// rather than capture one.
	/// </returns>
	ITenantContext Create(IServiceProvider services);
}
