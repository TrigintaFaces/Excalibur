// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Options.Middleware;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

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

		// The DELIBERATE call is the signal. A host that reaches UseOutbox() has asked for staging, so a
		// missing store is a misconfiguration and startup must refuse. A host that merely took the default
		// pipeline never asked, and for it an absent store means "no outbox", handled by the middleware
		// staying inert. One constructor could not separate those populations; this registration site can,
		// because only one of them runs this line.
		builder.Services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<OutboxStagingOptions>, OutboxStagingWiringValidator>());
		_ = builder.Services.AddOptions<OutboxStagingOptions>().ValidateOnStart();

		// Cascade runs at DispatchMiddlewareStage.Cascade (inside outbox staging); stage-sorting places it
		// correctly regardless of registration order, and it is a no-op unless a handler returns ICascade.
		return builder
			.UseMiddleware<OutboxStagingMiddleware>()
			.UseMiddleware<CascadeMiddleware>();
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
		// The DELIBERATE call is the signal. A host that reaches UseOutbox() has asked for staging, so a
		// missing store is a misconfiguration and startup must refuse. A host that merely took the default
		// pipeline never asked, and for it an absent store means "no outbox", handled by the middleware
		// staying inert. One constructor could not separate those populations; this registration site can,
		// because only one of them runs this line.
		builder.Services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<OutboxStagingOptions>, OutboxStagingWiringValidator>());
		_ = builder.Services.AddOptions<OutboxStagingOptions>().ValidateOnStart();

		return builder
			.UseMiddleware<OutboxStagingMiddleware>()
			.UseMiddleware<CascadeMiddleware>();
	}
}
