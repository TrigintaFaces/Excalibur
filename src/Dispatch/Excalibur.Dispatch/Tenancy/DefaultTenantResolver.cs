// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch;

/// <summary>
/// Default <see cref="ITenantResolver"/>: resolves the tenant from the message context's
/// <see cref="TenantContextHolder.TenantIdItemKey"/> item, falling back to
/// <see cref="TenantContextOptions.DefaultTenantId"/> when the message carries no tenant.
/// </summary>
internal sealed class DefaultTenantResolver : ITenantResolver
{
	private readonly TenantContextOptions _options;

	public DefaultTenantResolver(IOptions<TenantContextOptions> options)
	{
		ArgumentNullException.ThrowIfNull(options);
		_options = options.Value;
	}

	/// <inheritdoc />
	public ValueTask<string?> ResolveAsync(IMessageContext context, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(context);
		cancellationToken.ThrowIfCancellationRequested();

		if (context.Items.TryGetValue(TenantContextHolder.TenantIdItemKey, out var value)
			&& value is string tenantId
			&& !string.IsNullOrEmpty(tenantId))
		{
			return ValueTask.FromResult<string?>(tenantId);
		}

		// No tenant on the message: fall back to the configured default. When RequireTenant is enabled and
		// no default is configured, fail fast rather than proceeding with an unscoped (false-isolation)
		// operation — mirrors the storage-side TenantShardNotFoundException.
		if (string.IsNullOrEmpty(_options.DefaultTenantId) && _options.RequireTenant)
		{
			throw new TenantRequiredException();
		}

		return ValueTask.FromResult(_options.DefaultTenantId);
	}
}
