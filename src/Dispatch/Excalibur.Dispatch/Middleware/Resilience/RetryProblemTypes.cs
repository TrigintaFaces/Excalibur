// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Middleware.Resilience;

/// <summary>
/// Canonical <see cref="MessageProblemDetails.Type"/> discriminators emitted by <see cref="RetryMiddleware"/>
/// on a terminal failure. Shared so downstream middleware (e.g. a dead-letter-on-exhaustion decorator) can
/// match the exhaustion terminal by the same const instead of a duplicated string literal (no stringly-typed
/// drift).
/// </summary>
internal static class RetryProblemTypes
{
	/// <summary>
	/// The <see cref="MessageProblemDetails.Type"/> of the distinct retry-exhaustion terminal returned when
	/// every retry attempt has been exhausted (both the failed-result and retryable-exception paths converge
	/// on it). Pairs with the <c>dispatch.retry.exhausted</c> counter.
	/// </summary>
	internal const string RetryExhausted = "RetryExhausted";

	/// <summary>
	/// The <see cref="MessageProblemDetails.Type"/> returned when a non-retryable exception is abandoned
	/// immediately (not an exhaustion — no exhausted-count).
	/// </summary>
	internal const string RetryError = "RetryError";
}
