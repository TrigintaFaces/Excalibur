// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch;
using Excalibur.EventSourcing.Implementation;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.DependencyInjection;

/// <summary>
/// Validates, at startup, that <see cref="OutboxStagingStrategy.Transactional"/> is only selected when the
/// transactional infrastructure it requires is actually registered.
/// </summary>
/// <remarks>
/// <para>
/// The <see cref="OutboxStagingStrategy.Transactional"/> strategy promises an atomic commit of event-append and
/// outbox-staging. It requires both an <see cref="ITransactionalOutboxWriter"/> and a transactional event store
/// (<see cref="ITransactionalEventStore"/>). When the strategy is selected explicitly but that infrastructure is
/// absent, the repository silently degrades to non-atomic eventually-consistent staging — integration events can
/// be lost on a crash between append and stage, with no diagnostic.
/// </para>
/// <para>
/// This validator (registered with <c>ValidateOnStart()</c>) makes that silent downgrade inexpressible: an
/// explicit <see cref="OutboxStagingStrategy.Transactional"/> without the required infrastructure fails fast at
/// startup, naming exactly what is missing. Only the <em>explicit</em>
/// <see cref="OutboxStagingStrategy.Transactional"/> value trips the guard;
/// <see cref="OutboxStagingStrategy.Auto"/> (which documents its own graceful fallback),
/// <see cref="OutboxStagingStrategy.EventuallyConsistent"/>, and <see cref="OutboxStagingStrategy.Deferred"/>
/// never do.
/// </para>
/// </remarks>
internal sealed class TransactionalStagingCapabilityValidator : IValidateOptions<EventSourcedRepositoryOptions>
{
	private readonly IServiceProvider _serviceProvider;

	/// <summary>
	/// Initializes a new instance of the <see cref="TransactionalStagingCapabilityValidator"/> class.
	/// </summary>
	/// <param name="serviceProvider">
	/// The service provider, used to probe for the optionally-registered transactional infrastructure.
	/// </param>
	public TransactionalStagingCapabilityValidator(IServiceProvider serviceProvider) =>
		_serviceProvider = serviceProvider;

	/// <inheritdoc/>
	public ValidateOptionsResult Validate(string? name, EventSourcedRepositoryOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		// Only the EXPLICIT Transactional strategy must fail-fast. Auto/EventuallyConsistent/Deferred keep their
		// documented behavior (Auto gracefully falls back) and never trip the guard.
		if (options.OutboxStagingStrategy != OutboxStagingStrategy.Transactional)
		{
			return ValidateOptionsResult.Success;
		}

		// Probe REGISTRATION, never RESOLUTION. This validator is a singleton and holds the ROOT provider:
		// resolving a scoped IEventStore (e.g. MongoDB) from it throws InvalidOperationException under scope
		// validation, and a telemetry/decorator wrapper hides the capability from an instance check. So a
		// resolve both false-fails the correctly-wired scoped case AND cannot distinguish "no store" from
		// "scoped store under scope validation". IServiceProviderIsService answers "is this registered?"
		// without constructing anything; a wire-time capability marker (registered only by providers whose
		// store implements ITransactionalEventStore) attests the transactional-append capability. Same verdict
		// with or without scope validation; AOT-safe, no reflection.
		var isService = _serviceProvider.GetService<IServiceProviderIsService>();
		var hasTransactionalWriter = isService?.IsService(typeof(ITransactionalOutboxWriter)) ?? false;
		var hasTransactionalEventStore = isService?.IsService(typeof(TransactionalEventStoreMarker)) ?? false;

		if (hasTransactionalWriter && hasTransactionalEventStore)
		{
			return ValidateOptionsResult.Success;
		}

		var missing = new List<string>(2);
		if (!hasTransactionalWriter)
		{
			missing.Add("an ITransactionalOutboxWriter");
		}

		if (!hasTransactionalEventStore)
		{
			missing.Add("a transactional event store (ITransactionalEventStore)");
		}

		return ValidateOptionsResult.Fail(
			$"OutboxStagingStrategy.Transactional was explicitly configured but the required transactional " +
			$"infrastructure is not registered: {string.Join(" and ", missing)} " +
			$"{(missing.Count == 1 ? "is" : "are")} missing. Transactional staging requires both an " +
			"ITransactionalOutboxWriter and a transactional event store to atomically commit the event append " +
			"and the outbox staging; without them the strategy would silently degrade to non-atomic " +
			"eventually-consistent staging (integration events can be lost on a crash between append and stage). " +
			"Register the transactional infrastructure, or use OutboxStagingStrategy.Auto (which falls back " +
			"gracefully), EventuallyConsistent, or Deferred.");
	}
}
