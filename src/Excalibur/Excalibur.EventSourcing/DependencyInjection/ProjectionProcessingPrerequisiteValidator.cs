// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.EventSourcing.Queries;
using Excalibur.EventSourcing.Subscriptions;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Excalibur.EventSourcing.DependencyInjection;

/// <summary>
/// Startup-time validator that fails loud when async projection processing is registered on a host that
/// cannot perform it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this is a startup failure and not a log line.</b> The processing host is a
/// <c>BackgroundService</c>. Without an <see cref="IGlobalStreamQuery"/> it has no stream to poll, so it
/// returns from its execute method immediately. A background service that returns is indistinguishable
/// from one that is idle: the application starts, reports healthy, and processes no projection for the
/// rest of its life. Nothing downstream ever learns — which is the failure this validator converts into a
/// startup error a consumer cannot miss.
/// </para>
/// <para>
/// <b>Why the absence is a configuration error rather than an empty result.</b> The query is supplied by
/// the event-store provider seam, so a host that opted into projection processing without a provider has
/// asked for something it cannot have. This is the same precondition, and the same argument, as
/// <see cref="ProjectionRebuildPrerequisiteValidator"/> — a rebuild and a catch-up both replay the global
/// stream, and neither can do anything at all without one.
/// </para>
/// <para>
/// <b>Why the checkpoint durability decision is forced rather than defaulted.</b> Replaying an async
/// projection is not idempotent, and the framework cannot tell a safe handler from an unsafe one: an
/// assigning handler survives a replay while an accumulating one double-counts on every restart,
/// silently and without bound. A default in that position must be the safe one, because the consumer
/// who gets it wrong is by definition the one who never read the guidance. So the host states its
/// answer — a durable store, or an explicit acknowledgement — and a host that states neither stops.
/// </para>
/// <para>
/// <b>What this does NOT establish.</b> It prevents replay-from-zero on restart. It says nothing about
/// two live readers of one subscription, which are not mutually excluded by anything, and nothing about
/// a reader that loses the checkpoint race and exits permanently. Do not read a passing startup as
/// either of those being safe.
/// </para>
/// <para>
/// <b>It probes registration, never resolution.</b> The verdict is the same either way, and probing
/// constructs nothing — so the validator cannot itself start a store, open a connection, or fail for a
/// reason unrelated to the thing it is checking.
/// </para>
/// </remarks>
internal sealed class ProjectionProcessingPrerequisiteValidator : IHostedService, IStartupPrerequisiteValidator
{
	private readonly IServiceProvider _services;

	public ProjectionProcessingPrerequisiteValidator(IServiceProvider services) =>
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
				"Async projection processing is registered, but no IGlobalStreamQuery is. The processing host "
				+ "polls the global event stream, so without one it would stop immediately while the host "
				+ "reported healthy and no projection was ever processed. The query is supplied by the "
				+ "event-store provider seam — register a provider (for example "
				+ "AddExcaliburEventSourcing(b => b.UseSqlServer(...))), or do not call "
				+ "EnableProjectionProcessing() on a host that has no event stream to poll.");
		}

		// The checkpoint durability decision is FORCED rather than defaulted, because the two safe
		// answers are indistinguishable to the framework and only one of them is safe for a given
		// consumer's handlers. See the remarks above.
		if (!isService.IsService(typeof(ISubscriptionCheckpointDurability))
			&& !isService.IsService(typeof(InMemoryProjectionCheckpointAcknowledgement)))
		{
			throw new InvalidOperationException(
				"Async projection processing is registered, but this host has not said whether its "
				+ "projection checkpoints survive a restart. Without a durable checkpoint store the "
				+ "checkpoint resets on every restart and every async projection is re-applied from the "
				+ "beginning of the stream. That is not idempotent: a projection is applied by loading "
				+ "the stored projection, applying the event and writing it back, and the stored "
				+ "projection records no last-applied position, so nothing can detect a second "
				+ "application. A handler that assigns a value survives it; one that accumulates — "
				+ "incrementing a total, appending to a list — double-counts on every restart, silently "
				+ "and without bound. Register a durable checkpoint store for your provider (for example "
				+ "AddSqlServerSubscriptionCheckpointStore(...)), or call "
				+ "AllowInMemoryProjectionCheckpoints() to accept the replay and confirm your handlers "
				+ "are idempotent.");
		}
	}
}
