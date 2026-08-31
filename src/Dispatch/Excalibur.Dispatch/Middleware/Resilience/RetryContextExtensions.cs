// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Middleware.Resilience;

/// <summary>
/// Records on the message context that <see cref="RetryMiddleware"/> exhausted every attempt, so an
/// upstream decorator can act on the exhaustion whether it observes a returned failure or a propagating
/// exception. Exhaustion leaves the middleware by both mechanisms, so no returned problem type can carry
/// the fact on both paths; the context can.
/// </summary>
internal static class RetryContextExtensions
{
	private const string RetryExhaustedKey = "__RetryExhausted";

	/// <summary>
	/// Records that every retry attempt was exhausted for the dispatch this context belongs to.
	/// </summary>
	/// <param name="context">The message context.</param>
	internal static void MarkRetryExhausted(this IMessageContext context) =>
		context.SetProperty(RetryExhaustedKey, value: true);

	/// <summary>
	/// Gets a value indicating whether every retry attempt was exhausted for this dispatch.
	/// </summary>
	/// <param name="context">The message context.</param>
	/// <returns><see langword="true"/> when retry exhausted its attempts; otherwise, <see langword="false"/>.</returns>
	internal static bool RetryExhausted(this IMessageContext context) =>
		context.GetProperty<bool>(RetryExhaustedKey);
}
