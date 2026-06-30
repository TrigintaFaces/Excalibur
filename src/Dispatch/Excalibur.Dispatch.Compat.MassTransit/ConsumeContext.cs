// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Threading;

namespace Excalibur.Dispatch.Compat.MassTransit;

/// <summary>
/// Source-compatible, minimal shape of MassTransit's <c>ConsumeContext&lt;TMessage&gt;</c>, exposing the
/// consumed message and the cancellation token.
/// </summary>
/// <typeparam name="TMessage">The consumed message type.</typeparam>
/// <remarks>
/// Only the deterministically-portable members are shimmed (<see cref="Message"/>,
/// <see cref="CancellationToken"/>). MassTransit-specific capabilities such as <c>Respond</c>,
/// <c>Publish</c>, <c>Send</c>, and <c>Redeliver</c> are <b>not</b> provided — consumer code using them
/// will not compile after the swap, surfacing the required manual migration step (no silent gap).
/// </remarks>
/// <remarks>
/// CA1715 (interface 'I' prefix) is intentionally suppressed: the type name must match MassTransit's
/// published <c>ConsumeContext&lt;T&gt;</c> verbatim for the source-compatible namespace swap to compile.
/// </remarks>
#pragma warning disable CA1715 // Identifiers should have correct prefix — intentional source-compat name.
public interface ConsumeContext<out TMessage>
#pragma warning restore CA1715
	where TMessage : class
{
	/// <summary>
	/// Gets the consumed message.
	/// </summary>
	TMessage Message { get; }

	/// <summary>
	/// Gets the cancellation token for the consume operation.
	/// </summary>
	CancellationToken CancellationToken { get; }
}
