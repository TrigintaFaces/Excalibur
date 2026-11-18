// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Dispatch;

/// <summary>
/// Holds the declared correlation for each saga type and message type, so the coordinator can decide
/// which saga instance a message belongs to without the publisher computing an identifier.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this is a registry and not a method on the saga.</b> The coordinator needs the correlation
/// BEFORE it has an instance, because the correlation is what produces the identifier it would load the
/// instance by. An instance method could not be reached in time, so the declaration is registered per
/// saga type at composition and read from here.
/// </para>
/// <para>
/// <b>What it stores is a typed closure, not a property name.</b> A registration captures the consumer's
/// own <c>Func&lt;TMessage, string&gt;</c>, so extracting the key is a direct call. Nothing is looked up
/// by name, nothing is reflected over, and the whole path survives trimming and Native AOT — which a
/// property-name-based correlation would not.
/// </para>
/// </remarks>
public interface ISagaCorrelationRegistry
{
	/// <summary>
	/// Resolves the saga identifier for <paramref name="message"/> from the correlation declared for
	/// <paramref name="sagaType"/>.
	/// </summary>
	/// <param name="sagaType">The saga type the message is being routed to.</param>
	/// <param name="message">The message carrying the business key.</param>
	/// <param name="sagaId">The derived identifier, when a correlation is declared and yielded a key.</param>
	/// <returns>
	/// <see langword="true"/> when a correlation is declared for this saga and message type and it produced
	/// an identifier; <see langword="false"/> when no correlation is declared, which leaves the caller to
	/// use whatever identifier the message carries itself.
	/// </returns>
	/// <remarks>
	/// <b>The two negative answers are deliberately not the same thing.</b> Returning
	/// <see langword="false"/> means NOTHING WAS DECLARED, and the caller should fall back. A declaration
	/// that exists and yields no key is a different situation entirely — the consumer said this message
	/// correlates and it does not — and an implementation must THROW rather than return
	/// <see langword="false"/>, because falling back there would route the message by whatever identifier
	/// it happened to carry and silently start a second saga.
	/// </remarks>
	bool TryResolveSagaId(Type sagaType, object message, out Guid sagaId);
}
