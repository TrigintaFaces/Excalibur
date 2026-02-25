// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions.Configuration;
using Excalibur.Dispatch.Options.Configuration;

namespace Excalibur.Dispatch.Configuration;

/// <summary>
/// Extension methods for configuring Dispatch options in a fluent manner.
/// </summary>
public static class DispatchBuilderOptionsExtensions
{
	/// <summary>
	/// Configures Dispatch options using a fluent syntax.
	/// </summary>
	/// <param name="builder"> The dispatch builder. </param>
	/// <param name="configure"> Configuration action for dispatch options. </param>
	/// <returns> The builder for fluent configuration. </returns>
	/// <example>
	/// <code>
	/// builder.WithOptions(o =&gt; {
	/// o.Inbox.Enabled = true;                    // false = light mode
	/// o.Consumer.Dedupe.Enabled = !o.Inbox.Enabled;
	/// o.Consumer.AckAfterHandle = true;
	/// o.Outbox.BatchSize = 100;
	/// o.Outbox.PublishIntervalMs = 1000;
	/// });
	/// </code>
	/// </example>
	public static IDispatchBuilder WithOptions(this IDispatchBuilder builder, Action<DispatchOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		return builder.ConfigureOptions(configure);
	}

	/// <summary>
	/// Configures inbox-specific options.
	/// </summary>
	/// <param name="builder"> The dispatch builder. </param>
	/// <param name="configure"> Configuration action for inbox options. </param>
	/// <returns> The builder for fluent configuration. </returns>
	public static IDispatchBuilder WithInbox(this IDispatchBuilder builder, Action<InboxOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		return builder.ConfigureOptions<DispatchOptions>(options => configure(options.Inbox));
	}

	/// <summary>
	/// Configures outbox-specific options.
	/// </summary>
	/// <param name="builder"> The dispatch builder. </param>
	/// <param name="configure"> Configuration action for outbox options. </param>
	/// <returns> The builder for fluent configuration. </returns>
	public static IDispatchBuilder WithOutbox(this IDispatchBuilder builder, Action<OutboxOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		return builder.ConfigureOptions<DispatchOptions>(options => configure(options.Outbox));
	}

	/// <summary>
	/// Configures consumer-specific options.
	/// </summary>
	/// <param name="builder"> The dispatch builder. </param>
	/// <param name="configure"> Configuration action for consumer options. </param>
	/// <returns> The builder for fluent configuration. </returns>
	public static IDispatchBuilder WithConsumer(this IDispatchBuilder builder, Action<ConsumerOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		return builder.ConfigureOptions<DispatchOptions>(options => configure(options.Consumer));
	}

	/// <summary>
	/// Enables full inbox mode with persistent storage.
	/// </summary>
	/// <param name="builder"> The dispatch builder. </param>
	/// <param name="configure"> Optional additional inbox configuration. </param>
	/// <returns> The builder for fluent configuration. </returns>
	public static IDispatchBuilder EnableInboxMode(this IDispatchBuilder builder, Action<InboxOptions>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(builder);

		return builder.WithOptions(options =>
		{
			options.Inbox.Enabled = true;
			options.Consumer.Dedupe.Enabled = false; // Disable deduplication when inbox is enabled
			configure?.Invoke(options.Inbox);
		});
	}

	/// <summary>
	/// Enables light mode with in-memory deduplication only.
	/// </summary>
	/// <param name="builder"> The dispatch builder. </param>
	/// <param name="configure"> Optional additional consumer configuration. </param>
	/// <returns> The builder for fluent configuration. </returns>
	public static IDispatchBuilder EnableLightMode(this IDispatchBuilder builder, Action<ConsumerOptions>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(builder);

		return builder.WithOptions(options =>
		{
			options.Inbox.Enabled = false;
			options.Consumer.Dedupe.Enabled = true; // Enable deduplication when inbox is disabled
			options.Outbox.UseInMemoryStorage = true;
			configure?.Invoke(options.Consumer);
		});
	}

	/// <summary>
	/// Configures options for high-performance scenarios.
	/// </summary>
	/// <param name="builder"> The dispatch builder. </param>
	/// <param name="enableInbox"> Whether to enable full inbox mode for durability. </param>
	/// <returns> The builder for fluent configuration. </returns>
	public static IDispatchBuilder WithPerformanceOptimizations(this IDispatchBuilder builder, bool enableInbox = false)
	{
		ArgumentNullException.ThrowIfNull(builder);

		return builder.WithOptions(options =>
		{
			options.Features.EnableCacheMiddleware = true;
			options.MaxConcurrency = Environment.ProcessorCount * 4;

			if (enableInbox)
			{
				options.Inbox.Enabled = true;
				options.Consumer.Dedupe.Enabled = false;
			}
			else
			{
				options.Inbox.Enabled = false;
				options.Consumer.Dedupe.Enabled = true;
				options.Outbox.UseInMemoryStorage = true;
			}

			options.Outbox.BatchSize = 500;
			options.Outbox.PublishIntervalMs = 500;
			options.Consumer.MaxConcurrentMessages = Environment.ProcessorCount * 2;
		});
	}
}
