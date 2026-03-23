// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Configuration;
using Excalibur.Dispatch.Options.Middleware;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Middleware.Outbox;

/// <summary>
/// Extension methods for adding outbox middleware to the dispatch pipeline.
/// </summary>
public static class OutboxPipelineExtensions
{
	/// <summary>
	/// Adds outbox middleware to the dispatch pipeline.
	/// </summary>
	/// <param name="builder"> The dispatch builder. </param>
	/// <returns> The builder for fluent configuration. </returns>
	/// <remarks>
	/// <para>
	/// The outbox middleware intercepts outgoing messages and stores them in an outbox
	/// for reliable delivery, ensuring at-least-once message publishing even if the
	/// transport is temporarily unavailable.
	/// </para>
	/// <para>
	/// Outbox services (including an <c>IOutboxStore</c> implementation) must be registered
	/// separately in the DI container. This method only adds the middleware to the pipeline.
	/// </para>
	/// <para>
	/// Recommended pipeline order:
	/// <code>
	/// builder.UseAuthentication()
	///        .UseAuthorization()
	///        .UseValidation()
	///        .UseTransaction()
	///        .UseOutbox();             // Store messages for reliable delivery
	/// </code>
	/// </para>
	/// </remarks>
	public static IDispatchBuilder UseOutbox(this IDispatchBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		return builder.UseMiddleware<OutboxMiddleware>();
	}

	/// <summary>
	/// Adds outbox middleware to the dispatch pipeline with configuration.
	/// </summary>
	/// <param name="builder"> The dispatch builder. </param>
	/// <param name="configure"> Action to configure <see cref="OutboxStagingOptions"/>. </param>
	/// <returns> The builder for fluent configuration. </returns>
	public static IDispatchBuilder UseOutbox(this IDispatchBuilder builder, Action<OutboxStagingOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		builder.Services.Configure(configure);
		return builder.UseMiddleware<OutboxMiddleware>();
	}
}
