// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch;

/// <summary>
/// Marks an exception as an <strong>intentional fail-closed signal</strong> that middleware raise to
/// reject a message, rather than an unexpected error.
/// </summary>
/// <remarks>
/// The dispatch middleware base wraps an unexpected <see cref="System.Exception"/> from a middleware's
/// processing in a diagnostic <see cref="System.InvalidOperationException"/> (carrying the middleware
/// name). That wrapping would mask a deliberate fail-closed rejection — a consumer catching, say,
/// <see cref="OutOfOrderMessageException"/> would instead see an opaque
/// <see cref="System.InvalidOperationException"/>. Exceptions implementing this marker are therefore
/// <strong>rethrown unwrapped</strong> (via <see cref="System.Runtime.ExceptionServices.ExceptionDispatchInfo"/>,
/// preserving the original stack trace) so the typed fail-closed signal reaches the caller intact.
/// Unmarked exceptions keep the diagnostic wrap.
/// </remarks>
public interface IFailClosedException
{
}
