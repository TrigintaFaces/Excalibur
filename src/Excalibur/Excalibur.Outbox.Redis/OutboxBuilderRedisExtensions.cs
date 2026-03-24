// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Outbox;
using Excalibur.Outbox.Redis;

using StackExchange.Redis;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring Redis provider on <see cref="IOutboxBuilder"/>.
/// </summary>
/// <remarks>
/// <para>
/// These extensions provide fluent provider selection by adding
/// provider-specific configuration to the core <see cref="IOutboxBuilder"/> interface.
/// </para>
/// </remarks>
public static class OutboxBuilderRedisExtensions
{
	/// <summary>
	/// Configures the outbox to use Redis storage.
	/// </summary>
	/// <param name="builder">The outbox builder.</param>
	/// <param name="configure">Action to configure the Redis outbox options.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="builder"/> or <paramref name="configure"/> is null.
	/// </exception>
	/// <example>
	/// <code>
	/// services.AddExcaliburOutbox(outbox =&gt;
	/// {
	///     outbox.UseRedis(options =&gt;
	///     {
	///         options.ConnectionString = "localhost:6379";
	///         options.KeyPrefix = "outbox:";
	///     })
	///     .EnableBackgroundProcessing();
	/// });
	/// </code>
	/// </example>
	public static IOutboxBuilder UseRedis(
		this IOutboxBuilder builder,
		Action<RedisOutboxOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		_ = builder.Services.AddRedisOutboxStore(configure);

		return builder;
	}

	/// <summary>
	/// Configures the outbox to use Redis storage with an existing connection.
	/// </summary>
	/// <param name="builder">The outbox builder.</param>
	/// <param name="connectionProvider">A factory function that provides the Redis connection.</param>
	/// <param name="configure">Action to configure the Redis outbox options.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when any argument is null.
	/// </exception>
	/// <example>
	/// <code>
	/// services.AddExcaliburOutbox(outbox =&gt;
	/// {
	///     outbox.UseRedis(
	///         sp =&gt; sp.GetRequiredService&lt;ConnectionMultiplexer&gt;(),
	///         options =&gt;
	///         {
	///             options.KeyPrefix = "outbox:";
	///         });
	/// });
	/// </code>
	/// </example>
	public static IOutboxBuilder UseRedis(
		this IOutboxBuilder builder,
		Func<IServiceProvider, ConnectionMultiplexer> connectionProvider,
		Action<RedisOutboxOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(connectionProvider);
		ArgumentNullException.ThrowIfNull(configure);

		_ = builder.Services.AddRedisOutboxStore(connectionProvider, configure);

		return builder;
	}
}
