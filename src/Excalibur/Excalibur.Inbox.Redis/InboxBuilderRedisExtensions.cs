// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Inbox.DependencyInjection;
using Excalibur.Inbox.Redis;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring Redis provider on <see cref="IInboxBuilder"/>.
/// </summary>
public static class InboxBuilderRedisExtensions
{
	/// <summary>
	/// Configures the inbox to use Redis storage.
	/// </summary>
	/// <param name="builder">The inbox builder.</param>
	/// <param name="configure">Action to configure the Redis inbox options.</param>
	/// <returns>The builder for fluent chaining.</returns>
	public static IInboxBuilder UseRedis(
		this IInboxBuilder builder,
		Action<RedisInboxOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		_ = builder.Services.AddRedisInboxStore(configure);

		return builder;
	}
}
