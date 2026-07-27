// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch;

/// <summary>
/// Default <see cref="ITenantOptions{TOptions}"/>: resolves the named options instance for the current
/// ambient tenant via <see cref="IOptionsMonitor{TOptions}"/>, keyed by the tenant id.
/// </summary>
/// <typeparam name="TOptions">The options type.</typeparam>
internal sealed class TenantOptions<TOptions> : ITenantOptions<TOptions>
	where TOptions : class
{
	private readonly ITenantContext _tenantContext;
	private readonly IOptionsMonitor<TOptions> _monitor;

	public TenantOptions(ITenantContext tenantContext, IOptionsMonitor<TOptions> monitor)
	{
		_tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
		_monitor = monitor ?? throw new ArgumentNullException(nameof(monitor));
	}

	/// <inheritdoc />
	/// <remarks>
	/// A tenant id keys a named options instance; an absent tenant resolves the default (unnamed) options.
	/// <see cref="IOptionsMonitor{TOptions}.Get"/> already falls back to the default configuration for a name
	/// that has no specific registration, so an unconfigured tenant transparently gets the defaults.
	/// </remarks>
	public TOptions Value =>
		_tenantContext.HasTenant
			? _monitor.Get(_tenantContext.TenantId)
			: _monitor.CurrentValue;
}
