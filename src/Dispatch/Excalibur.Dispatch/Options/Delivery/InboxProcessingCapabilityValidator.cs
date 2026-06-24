// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Options.Delivery;

/// <summary>
/// Validates, at startup, that the registered full-inbox <see cref="IInboxStore"/> can durably persist the
/// in-flight <see cref="InboxStatus.Processing"/> status by implementing <see cref="IProcessingTrackingInboxStore"/>.
/// </summary>
/// <remarks>
/// <para>
/// The inbox middleware's full-inbox mode relies on a durable <see cref="InboxStatus.Processing"/> status to power
/// the at-most-once concurrency guard and the stuck-processing timeout. A store that cannot persist that status
/// would silently degrade those guarantees. This validator (registered with <c>ValidateOnStart()</c>) makes that
/// degrade structurally inexpressible: a full inbox registered with a store lacking the capability fails fast at
/// startup rather than running with a dead at-most-once guard (ADR-336 clause 2).
/// </para>
/// <para>
/// All shipped Excalibur inbox stores implement <see cref="IProcessingTrackingInboxStore"/>, so this guard only
/// fires for a custom store that omits the capability.
/// </para>
/// </remarks>
internal sealed class InboxProcessingCapabilityValidator : IValidateOptions<InboxOptions>
{
	private readonly IInboxStore _inboxStore;

	/// <summary>
	/// Initializes a new instance of the <see cref="InboxProcessingCapabilityValidator"/> class.
	/// </summary>
	/// <param name="inboxStore">The registered default inbox store.</param>
	public InboxProcessingCapabilityValidator([FromKeyedServices("default")] IInboxStore inboxStore) =>
		_inboxStore = inboxStore;

	/// <inheritdoc/>
	public ValidateOptionsResult Validate(string? name, InboxOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		if (_inboxStore is IProcessingTrackingInboxStore)
		{
			return ValidateOptionsResult.Success;
		}

		return ValidateOptionsResult.Fail(
			$"The registered inbox store '{_inboxStore.GetType().FullName}' does not implement " +
			$"'{nameof(IProcessingTrackingInboxStore)}'. The full-inbox at-most-once concurrency guard and the " +
			"stuck-processing timeout require the Processing status to be durably persisted; without the capability " +
			"they would silently degrade and duplicate handler execution could occur under concurrent delivery. " +
			"Register an inbox store that supports durable Processing tracking (all shipped Excalibur inbox stores do), " +
			"or implement IProcessingTrackingInboxStore on your custom inbox store.");
	}
}
