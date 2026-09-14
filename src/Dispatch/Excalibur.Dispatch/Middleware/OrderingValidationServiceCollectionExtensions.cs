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
	/// The middleware enforces strictly-increasing per-key ordering and is <strong>fail-closed</strong>
	/// for the messages it applies to: those that arrived through a receive path where ordering is
	/// enforced. Such a message carrying an out-of-order sequence, or none at all, throws
	/// <see cref="OutOfOrderMessageException"/> — a missing sequence there means the transport did not
	/// supply one and nothing stamped it, which is a fault rather than a reason to pass silently.
	/// <para>
	/// This registers globally. There is no per-pipeline middleware selector, so the middleware sees
	/// every dispatch in the process; what bounds it is the receive-path marking, not where you
	/// register it. Messages that never entered such a path — outbound sends, in-process commands —
	/// pass through. Stamping is the consumer's: call
	/// <c>TransportOrderingMetadata.TryStampOrdering</c> in the bridge that turns a received message
	/// into a dispatch, or
	/// <see cref="OrderingContextExtensions.SetOrderingSequence(IMessageContext, long, string?)"/> with
	/// <see cref="OrderingContextExtensions.MarkOrderingEnforced(IMessageContext)"/> where the transport
	/// has no native sequence.
	/// </para>
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
