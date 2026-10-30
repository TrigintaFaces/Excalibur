// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.EventSourcing.Projections;
using Excalibur.EventSourcing.Queries;
using Excalibur.EventSourcing.Subscriptions;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Excalibur.EventSourcing.DependencyInjection;

/// <summary>
/// Refuses, at startup, a composition in which <c>Inline</c> projections can never complete.
/// </summary>
/// <remarks>
/// <para>
/// An Inline save waits for the ordered processor to reach its committed global position. Two
/// compositions make that impossible, and both used to surface on the FIRST WRITE instead: an event
/// store registration without a global-stream reader, and a missing checkpoint store. Discovering either at the
/// first write is the worst time to discover it, because by then the events are durably committed and
/// the only honest answer is that the read model is behind and will stay behind.
/// </para>
/// <para>
/// <see cref="IGlobalStreamQuery"/> supplies the ordered processor's reader. Its absence means the
/// processor cannot observe committed events, even if the store assigns global positions. Provider
/// registration must pair the reader with the event store; this check verifies that the reader is
/// available, not that every append result satisfies the provider's position contract.
/// </para>
/// <para>
/// This runs as BOTH a hosted service and a startup prerequisite validator. The second registration is
/// the load-bearing one for a host that runs no hosted services: the validator is invoked by the
/// explicit startup-gate call such a host makes, so the refusal still happens before the first write
/// rather than during it.
/// </para>
/// <para>
/// The per-save check in the completion path is kept as defence in depth, and the distinction matters:
/// this validator refuses a COMPOSITION error, while that check catches a provider that registers a
/// global-stream query and then reports no position anyway -- a violation of its own contract rather
/// than a mistake the consumer made.
/// </para>
/// </remarks>
internal sealed class InlineProjectionPrerequisiteValidator : IHostedService, IStartupPrerequisiteValidator
{
	private readonly IServiceProvider _services;

	public InlineProjectionPrerequisiteValidator(IServiceProvider services) =>
		_services = services ?? throw new ArgumentNullException(nameof(services));

	public Task StartAsync(CancellationToken cancellationToken)
	{
		Validate();
		return Task.CompletedTask;
	}

	public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

	public void Validate()
	{
		var isService = _services.GetRequiredService<IServiceProviderIsService>();

		if (!isService.IsService(typeof(IGlobalStreamQuery)))
		{
			throw new InvalidOperationException(
				"Inline projections are registered, but no global-stream reader is registered. Use a complete event-store provider registration that supplies IGlobalStreamQuery. An Inline "
				+ "save waits for the ordered processor to reach its committed position, so without one every "
				+ "save would commit its events and then report its read model behind -- permanently, because "
				+ "the host cannot observe the committed position. Register an event-store provider that supplies "
				+ "a global stream (for example AddExcaliburEventSourcing(b => b.UseSqlServer(...))). "
				+ "Async processing also requires this reader; changing projection mode does not repair the registration.");
		}

		// Mirrors the decision the coordinator makes when it builds its host, so the same refusal happens
		// at startup instead of on the first write. A source-affine pair supplies its own store, which is
		// why its presence short-circuits the check.
		if (_services.GetService<InlineProjectionSource>() is not null)
		{
			return;
		}

		if (!isService.IsService(typeof(ISubscriptionCheckpointDurability))
			&& !isService.IsService(typeof(InMemoryProjectionCheckpointAcknowledgement)))
		{
			throw new InvalidOperationException(
				"Inline projections require a durable checkpoint store, and none is registered. The Inline "
				+ "cursor is what makes a completed fold survive a restart; without it a save could not record "
				+ "that it had caught up, so recovery would re-fold from the beginning of the stream every time. "
				+ "UseSqlServer supplies one automatically; other providers must register their checkpoint store.");
		}
	}
}
