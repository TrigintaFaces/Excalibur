// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch;
using Excalibur.Dispatch.Middleware.Ordering;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering the ordering-validation middleware.
/// </summary>
public static class OrderingValidationServiceCollectionExtensions
{
	/// <summary>
	/// Adds the ordering-validation middleware to the dispatch pipeline.
	/// </summary>
	/// <remarks>
	/// The middleware enforces strictly-increasing per-key ordering and is <strong>fail-closed</strong>:
	/// once registered it requires every message to carry an ordering sequence (auto-stamped for Kafka /
	/// Azure Service Bus, or consumer-stamped via
	/// <see cref="OrderingContextExtensions.SetOrderingSequence(IMessageContext, long, string?)"/>). Both
	/// an out-of-order sequence and a <em>missing</em> sequence throw
	/// <see cref="OutOfOrderMessageException"/> — a missing sequence is treated as an advertised-but-unfed
	/// misconfiguration, never a silent pass. Register it only on pipelines whose messages are ordered.
	/// Registration is idempotent; a single stateful instance tracks the per-key high-water marks.
	/// </remarks>
	/// <param name="services"> The service collection. </param>
	/// <returns> The service collection for chaining. </returns>
	/// <exception cref="ArgumentNullException"> <paramref name="services"/> is <see langword="null"/>. </exception>
	public static IServiceCollection AddOrderingValidation(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IDispatchMiddleware, OrderingValidationMiddleware>());

		return services;
	}
}
