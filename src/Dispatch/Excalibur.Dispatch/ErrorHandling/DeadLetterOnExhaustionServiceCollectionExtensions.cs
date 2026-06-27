// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.ErrorHandling;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering the optional dead-letter-on-exhaustion middleware (8o3c3p).
/// </summary>
public static class DeadLetterOnExhaustionServiceCollectionExtensions
{
	/// <summary>
	/// Registers the opt-in <see cref="DeadLetterOnExhaustionMiddleware"/>, which routes in-process dispatches
	/// that exhaust every retry attempt to the dead-letter queue.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Place the middleware <strong>upstream</strong> of the retry middleware in the pipeline (e.g. via the
	/// dispatch builder's <c>UseMiddleware&lt;DeadLetterOnExhaustionMiddleware&gt;()</c> before the retry
	/// middleware) so the retry middleware runs as its <c>next</c> delegate and the decorator can observe the
	/// retry-exhaustion terminal it returns.
	/// </para>
	/// <para>
	/// A no-op <see cref="ErrorHandling.NullDeadLetterQueue"/> is registered via <c>TryAdd</c> as the fail-safe
	/// default, so a consumer that has not registered a real <see cref="ErrorHandling.IDeadLetterQueue"/> gets
	/// the no-op (the exhaustion is logged, never crashes) rather than a missing-service resolution failure. A
	/// consumer-registered queue takes precedence.
	/// </para>
	/// </remarks>
	/// <param name="services">The service collection.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddDeadLetterOnExhaustion(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		// Fail-safe default: no-op queue unless the consumer registered a real one.
		services.TryAddSingleton<IDeadLetterQueue, NullDeadLetterQueue>();

		// Register the middleware concrete type for pipeline resolution. Consumers add it to the pipeline
		// upstream of the retry middleware (e.g. UseMiddleware<DeadLetterOnExhaustionMiddleware>()), consistent
		// with the other public pipeline middleware (PoisonMessageMiddleware, RetryMiddleware, …).
		services.TryAddSingleton<DeadLetterOnExhaustionMiddleware>();

		return services;
	}
}
