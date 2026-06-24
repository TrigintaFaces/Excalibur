// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Options.Delivery;

/// <summary>
/// Validates, at startup, that the registered full-inbox <see cref="IInboxStore"/> can atomically claim a message
/// for idempotent processing by implementing <see cref="IClaimableInboxStore"/>.
/// </summary>
/// <remarks>
/// <para>
/// The idempotency middleware admits exactly one of N concurrent duplicate deliveries by atomically claiming the
/// message <em>before</em> the handler runs and releasing the claim if the handler fails. A store that cannot claim
/// atomically would force the middleware back onto a non-atomic check-then-act, under which two concurrent duplicates
/// can both observe "not processed" and both execute the handler. This validator (registered with
/// <c>ValidateOnStart()</c>) makes that silent race structurally inexpressible: a persistent inbox registered with a
/// store lacking the capability fails fast at startup rather than running with a non-atomic idempotency guard
/// (ADR-336 clause 2).
/// </para>
/// <para>
/// The in-memory and SQL Server / PostgreSQL inbox stores implement <see cref="IClaimableInboxStore"/>. Other
/// providers that have not yet added the capability trip this guard (fail loud, never silent-race) until the
/// capability is implemented for them.
/// </para>
/// </remarks>
internal sealed class IdempotencyClaimCapabilityValidator : IValidateOptions<InboxOptions>
{
	private readonly IInboxStore _inboxStore;

	/// <summary>
	/// Initializes a new instance of the <see cref="IdempotencyClaimCapabilityValidator"/> class.
	/// </summary>
	/// <param name="inboxStore">The registered default inbox store.</param>
	public IdempotencyClaimCapabilityValidator([FromKeyedServices("default")] IInboxStore inboxStore) =>
		_inboxStore = inboxStore;

	/// <inheritdoc/>
	public ValidateOptionsResult Validate(string? name, InboxOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		if (_inboxStore is IClaimableInboxStore)
		{
			return ValidateOptionsResult.Success;
		}

		return ValidateOptionsResult.Fail(
			$"The registered inbox store '{_inboxStore.GetType().FullName}' does not implement " +
			$"'{nameof(IClaimableInboxStore)}'. The idempotency middleware admits exactly one of N concurrent " +
			"duplicate deliveries by atomically claiming a message before the handler runs and releasing the claim " +
			"on failure; without the capability it would fall back to a non-atomic check-then-act under which " +
			"concurrent duplicates can both execute the handler. Register an inbox store that supports atomic " +
			"claiming (the in-memory, SQL Server, and PostgreSQL inbox stores do), or implement IClaimableInboxStore " +
			"on your custom inbox store.");
	}
}
