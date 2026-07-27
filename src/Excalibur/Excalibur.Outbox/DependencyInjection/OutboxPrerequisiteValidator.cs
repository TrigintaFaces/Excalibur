// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Options.Delivery;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Excalibur.Outbox.DependencyInjection;

/// <summary>
/// Startup-time prerequisite validator that fails loud at <see cref="IHost.StartAsync"/>
/// if the consumer called <c>AddOutbox(...)</c> without registering a concrete
/// <see cref="IOutboxStore"/> provider (e.g., by omitting <c>.UseSqlServer(...)</c>,
/// <c>.UsePostgres(...)</c>, <c>.UseMongoDB(...)</c>, or a consumer-supplied store).
/// </summary>
/// <remarks>
/// <para>
/// minimal-wiring validators must fail at host
/// start, not at first message enqueue. Registering this as an <see cref="IHostedService"/>
/// places the probe in the host's startup pipeline ahead of any domain workload.
/// </para>
/// <para>
/// AOT-safe: the probe uses <c>IServiceProvider.GetKeyedService&lt;IOutboxStore&gt;("default")</c>
/// — no reflection, no assembly scanning.
/// </para>
/// </remarks>
internal sealed class OutboxPrerequisiteValidator : IHostedService
{
	private readonly IServiceProvider _services;

	public OutboxPrerequisiteValidator(IServiceProvider services)
	{
		_services = services ?? throw new ArgumentNullException(nameof(services));
	}

	public Task StartAsync(CancellationToken cancellationToken)
	{
		var store = _services.GetKeyedService<IOutboxStore>("default")
			?? throw new InvalidOperationException(
				"Excalibur outbox is missing the required IOutboxStore implementation. " +
				"Call a provider extension inside AddOutbox(...) — for example " +
				"o => o.UseSqlServer(sql => sql.ConnectionString(...)), " +
				"o => o.UsePostgres(...), or o => o.UseMongoDB(...) — before host startup.");

		// Fencing composition invariant — enforced at host startup so it covers EVERY drain path,
		// including the default OutboxBackgroundService -> IOutboxPublisher drain, which never
		// constructs OutboxProcessor (so the processor's own defense-in-depth check cannot fire on it).
		// When a leader election is registered and the consumer has not opted out via AsSingleWriter(),
		// a store that cannot enforce a fencing high-water mark would let a superseded leader drain
		// unfenced — refuse to start instead of draining unfenced. Shared source of truth with the
		// OutboxProcessor constructor (OutboxFencingStartupInvariant).
		var leaderGate = _services.GetService<ILeaderProcessingGate>();
		var deliveryOptions = _services.GetRequiredService<IOptions<OutboxDeliveryOptions>>().Value;
		OutboxFencingStartupInvariant.EnsureFencingCapableStore(leaderGate, deliveryOptions.SingleActiveWriter, store);

		return Task.CompletedTask;
	}

	public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
