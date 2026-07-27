// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;

namespace Excalibur.Data.Sharding;

/// <summary>
/// Default <see cref="IAmbientTenantStoreResolver{TStore}"/>: reads the ambient tenant from
/// <see cref="ITenantContext"/> and delegates to the tenant-keyed <see cref="ITenantStoreResolver{TStore}"/>.
/// Stateless (the ambient tenant lives in the async-flow-local context), so it is safe as a singleton.
/// </summary>
/// <typeparam name="TStore">The store abstraction type.</typeparam>
internal sealed class AmbientTenantStoreResolver<TStore> : IAmbientTenantStoreResolver<TStore>
{
	private readonly ITenantStoreResolver<TStore> _resolver;
	private readonly ITenantContext _context;

	public AmbientTenantStoreResolver(ITenantStoreResolver<TStore> resolver, ITenantContext context)
	{
		ArgumentNullException.ThrowIfNull(resolver);
		ArgumentNullException.ThrowIfNull(context);
		_resolver = resolver;
		_context = context;
	}

	/// <inheritdoc />
	public TStore ResolveCurrent() => _resolver.Resolve(_context.TenantId ?? string.Empty);
}
