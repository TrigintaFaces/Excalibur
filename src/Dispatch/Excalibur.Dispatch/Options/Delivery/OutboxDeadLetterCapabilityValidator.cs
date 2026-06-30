// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Options.Delivery;

/// <summary>
/// Validates, at startup, that the registered polling <see cref="IOutboxStore"/> can durably transition a
/// retry-exhausted message to the terminal <see cref="OutboxStatus.DeadLettered"/> status by implementing
/// <see cref="IDeadLetterableOutboxStore"/>.
/// </summary>
/// <remarks>
/// <para>
/// The polling outbox dead-letters a message once its retry policy is exhausted. Without a terminal status
/// transition the message stays <see cref="OutboxStatus.Failed"/>, is re-claimed after its lease expires, and is
/// re-delivered and re-dead-lettered forever (duplicate delivery + unbounded dead-letter-queue growth). This
/// validator (registered with <c>ValidateOnStart()</c>) makes that degrade structurally inexpressible: a polling
/// outbox registered with a store that cannot terminalize fails fast at startup.
/// </para>
/// <para>
/// All shipped Excalibur outbox stores implement <see cref="IDeadLetterableOutboxStore"/>, so this guard only
/// fires for a custom store that omits the capability.
/// </para>
/// </remarks>
internal sealed class OutboxDeadLetterCapabilityValidator : IValidateOptions<OutboxDeliveryOptions>
{
	private readonly IOutboxStore _outboxStore;

	/// <summary>
	/// Initializes a new instance of the <see cref="OutboxDeadLetterCapabilityValidator"/> class.
	/// </summary>
	/// <param name="outboxStore">The registered default outbox store.</param>
	public OutboxDeadLetterCapabilityValidator([FromKeyedServices("default")] IOutboxStore outboxStore) =>
		_outboxStore = outboxStore;

	/// <inheritdoc/>
	public ValidateOptionsResult Validate(string? name, OutboxDeliveryOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		if (_outboxStore is IDeadLetterableOutboxStore)
		{
			return ValidateOptionsResult.Success;
		}

		return ValidateOptionsResult.Fail(
			$"The registered outbox store '{_outboxStore.GetType().FullName}' does not implement " +
			$"'{nameof(IDeadLetterableOutboxStore)}'. The polling outbox transitions a retry-exhausted message to the " +
			"terminal DeadLettered status so it is never re-claimed; without the capability the message would stay " +
			"Failed, be re-claimed after its lease expires, and be re-delivered and re-dead-lettered indefinitely. " +
			"Register an outbox store that supports terminal dead-lettering (all shipped Excalibur outbox stores do), " +
			"or implement IDeadLetterableOutboxStore on your custom outbox store.");
	}
}
