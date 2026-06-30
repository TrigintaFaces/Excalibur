// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch;
using Excalibur.EventSourcing.Implementation;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.DependencyInjection;

/// <summary>
/// Validates, at startup, that <see cref="OutboxStagingStrategy.EventuallyConsistent"/> is only selected when the
/// outbox store it requires is actually registered.
/// </summary>
/// <remarks>
/// <para>
/// The <see cref="OutboxStagingStrategy.EventuallyConsistent"/> strategy promises that integration events are staged
/// into an outbox for at-least-once delivery after the event append commits. It requires a registered
/// <see cref="IOutboxStore"/>. When the strategy is selected explicitly but no store is registered, the repository's
/// staging branches gate on <c>_outboxStore is not null</c> and are silently skipped — the append succeeds, no events
/// are staged, and there is no diagnostic. Integration events are lost with no error.
/// </para>
/// <para>
/// This validator (registered with <c>ValidateOnStart()</c>) makes that silent loss inexpressible: an explicit
/// <see cref="OutboxStagingStrategy.EventuallyConsistent"/> without a registered <see cref="IOutboxStore"/> fails fast
/// at startup, naming exactly what is missing. Only the <em>explicit</em>
/// <see cref="OutboxStagingStrategy.EventuallyConsistent"/> value trips the guard. The default
/// <see cref="OutboxStagingStrategy.Auto"/> never sets the value to <see cref="OutboxStagingStrategy.EventuallyConsistent"/>
/// on the options — the auto-derived <see cref="OutboxStagingStrategy.EventuallyConsistent"/> is computed at runtime
/// only when a store is present (safe by construction), so it never reaches this guard.
/// </para>
/// </remarks>
internal sealed class EventSourcedRepositoryStagingCapabilityValidator : IValidateOptions<EventSourcedRepositoryOptions>
{
	private readonly IServiceProvider _serviceProvider;

	/// <summary>
	/// Initializes a new instance of the <see cref="EventSourcedRepositoryStagingCapabilityValidator"/> class.
	/// </summary>
	/// <param name="serviceProvider">
	/// The service provider, used to probe for the optionally-registered <see cref="IOutboxStore"/>.
	/// </param>
	public EventSourcedRepositoryStagingCapabilityValidator(IServiceProvider serviceProvider) =>
		_serviceProvider = serviceProvider;

	/// <inheritdoc/>
	public ValidateOptionsResult Validate(string? name, EventSourcedRepositoryOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		// Only the EXPLICIT EventuallyConsistent strategy must fail-fast. The default is Auto, which never assigns
		// EventuallyConsistent to the options; the auto-derived EventuallyConsistent (ResolveEffectiveStagingStrategy)
		// is computed at runtime only when a store is present, so it is safe by construction and never reaches here.
		if (options.OutboxStagingStrategy != OutboxStagingStrategy.EventuallyConsistent)
		{
			return ValidateOptionsResult.Success;
		}

		// Probe the keyed "default" outbox store — the canonical registration shape every outbox provider uses
		// (TryAddKeyedSingleton<IOutboxStore>("default")) and the same key the repository factory resolves. A
		// keyed-only service is invisible to a non-keyed GetService, so probing keyed here keeps the guard
		// consistent with how the repository actually receives the store (advertised-or-fail-loud).
		var hasOutboxStore = _serviceProvider.GetKeyedService<IOutboxStore>("default") is not null;
		if (hasOutboxStore)
		{
			return ValidateOptionsResult.Success;
		}

		return ValidateOptionsResult.Fail(
			"OutboxStagingStrategy.EventuallyConsistent was explicitly configured but no IOutboxStore is registered. " +
			"EventuallyConsistent staging requires an outbox store to persist integration events for at-least-once " +
			"delivery after the event append commits; without one the repository would silently skip staging and the " +
			"integration events would be lost with no diagnostic. Register an IOutboxStore (for example via an outbox " +
			"provider package), or use OutboxStagingStrategy.Auto (which falls back gracefully), Transactional, or " +
			"Deferred.");
	}
}
