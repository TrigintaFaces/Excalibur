// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.ErrorHandling;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering the optional dead-letter-on-exhaustion middleware.
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
	/// This method registers <strong>no</strong> dead-letter store. An
	/// <see cref="IDeadLetterQueue"/> is a required collaborator: routing exhausted messages to a
	/// store that cannot retain them is indistinguishable, to the caller and to the log, from routing them to
	/// one that can. Resolving the middleware without a registered queue therefore throws an
	/// <see cref="InvalidOperationException"/> naming the registration you are missing.
	/// </para>
	/// <para>
	/// To discard exhausted messages deliberately, register
	/// <see cref="NullDeadLetterQueue"/> yourself —
	/// <c>services.AddSingleton&lt;IDeadLetterQueue&gt;(NullDeadLetterQueue.Instance)</c>. The middleware then
	/// logs each exhausted message as <em>discarded</em> rather than reporting it as dead-lettered, so the
	/// choice is visible both in the composition root and in the log.
	/// </para>
	/// </remarks>
	/// <param name="services">The service collection.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddDeadLetterOnExhaustion(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		// Constructed through a factory rather than by type so the missing-store failure is a composition
		// error the developer can act on. The previous form registered a no-op default by TYPE
		// (TryAddSingleton<IDeadLetterQueue, NullDeadLetterQueue>), which the container cannot activate at all
		// — NullDeadLetterQueue's constructor is private and only its static Instance is reachable — so the
		// documented quickstart threw "A suitable constructor for type 'NullDeadLetterQueue' could not be
		// located." Registering that instance instead would have been worse: the middleware would have
		// enqueued into the no-op and logged the message as routed to the dead-letter queue while dropping it.
		services.TryAddSingleton(sp =>
			new DeadLetterOnExhaustionMiddleware(
				sp.GetService<IDeadLetterQueue>() ?? throw new InvalidOperationException(
					"AddDeadLetterOnExhaustion() requires an IDeadLetterQueue, and none is registered. The "
					+ "middleware routes messages that exhaust every retry attempt, so a host without a store "
					+ "would drop them. Register one before building the host - for example "
					+ "AddSqlServerOutbox(...), which registers SqlServerDeadLetterQueue. If discarding "
					+ "exhausted messages is genuinely intended, make that choice explicit with "
					+ "services.AddSingleton<IDeadLetterQueue>(NullDeadLetterQueue.Instance); each discarded "
					+ "message is then logged as discarded rather than reported as dead-lettered."),
				sp.GetRequiredService<ILogger<DeadLetterOnExhaustionMiddleware>>()));

		return services;
	}
}
