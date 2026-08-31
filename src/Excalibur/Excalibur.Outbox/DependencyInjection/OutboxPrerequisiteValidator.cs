// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.CloudNative;
using Excalibur.Dispatch;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Options.Delivery;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Excalibur.Outbox.DependencyInjection;

/// <summary>
/// Startup-time prerequisite validator that fails loud at <see cref="IHost.StartAsync"/>
/// if the consumer called <c>AddOutbox(...)</c> without registering a concrete outbox store
/// provider — neither a polling <see cref="IOutboxStore"/> (e.g., by omitting
/// <c>.UseSqlServer(...)</c>, <c>.UsePostgres(...)</c>, <c>.UseMongoDB(...)</c>, or a
/// consumer-supplied store) nor a change-feed <see cref="ICloudNativeOutboxStore"/> (e.g., by
/// omitting <c>.UseCosmosDb(...)</c>, <c>.UseDynamoDb(...)</c>, or <c>.UseFirestore(...)</c>).
/// </summary>
/// <remarks>
/// <para>
/// minimal-wiring validators must fail at host
/// start, not at first message enqueue. Registering this as an <see cref="IHostedService"/>
/// places the probe in the host's startup pipeline ahead of any domain workload.
/// </para>
/// <para>
/// AOT-safe: the probe uses <c>IServiceProvider.GetKeyedService&lt;IOutboxStore&gt;("default")</c>
/// and <c>IServiceProvider.GetService&lt;ICloudNativeOutboxStore&gt;()</c> — no reflection, no
/// assembly scanning.
/// </para>
/// <para>
/// <see cref="IOutboxStore"/> (polling) and <see cref="ICloudNativeOutboxStore"/> (change-feed) are
/// intentionally separate contracts serving different outbox patterns — see the remarks on
/// <see cref="ICloudNativeOutboxStore"/>. A host is satisfied by either.
/// </para>
/// </remarks>
internal sealed class OutboxPrerequisiteValidator : IHostedService, IStartupPrerequisiteValidator
{
	private readonly IServiceProvider _services;

	public OutboxPrerequisiteValidator(IServiceProvider services)
	{
		_services = services ?? throw new ArgumentNullException(nameof(services));
	}

	public Task StartAsync(CancellationToken cancellationToken)
	{
		Validate();
		return Task.CompletedTask;
	}

	public void Validate()
	{
		// Fall back to the non-keyed registration: a provider extension supplies the keyed "default"
		// (aliased onto the non-keyed contract), but a consumer supplying their own store registers it
		// directly and non-keyed -- and that is the instance OutboxProcessor/MessageOutbox are handed,
		// so it is the one the fencing invariant below must judge.
		var store = _services.GetKeyedService<IOutboxStore>("default") ?? _services.GetService<IOutboxStore>();
		if (store is null)
		{
			// Change-feed providers (Cosmos DB, DynamoDB, Firestore) satisfy the outbox prerequisite
			// through ICloudNativeOutboxStore, not the keyed "default" IOutboxStore -- the two are
			// intentionally separate contracts (see ICloudNativeOutboxStore remarks), so their absence
			// here is not itself a missing-provider error.
			if (_services.GetService<ICloudNativeOutboxStore>() is not null)
			{
				// Change-feed stores are addressed by partition key with deliberately estate-wide reads,
				// and are never drained by OutboxProcessor/OutboxBackgroundService (which require
				// IOutboxStore) -- so the polling family's fencing high-water-mark invariant below has no
				// drain path to protect here and does not apply to this store family.
				return;
			}

			// Shared with the ValidateOnStart hook and the IOutboxProcessor/IOutboxDispatcher factories:
			// one condition must not have three wordings.
			throw new InvalidOperationException(OutboxStorePrerequisite.MissingStoreMessage);
		}

		// Fencing composition invariant — enforced at host startup so it covers EVERY drain path,
		// including the default OutboxBackgroundService -> IOutboxPublisher drain, which never
		// constructs OutboxProcessor (so the processor's own defense-in-depth check cannot fire on it).
		// When a leader election is registered and the consumer has not opted out via AsSingleWriter(),
		// an ungated drain, or a store that cannot enforce a fencing high-water mark, would let a
		// superseded leader drain unfenced — refuse to start instead. The predicate is the presence of
		// the ELECTION, not of the gate: keying on the gate would let a host that registered an election
		// through a path that never wired the gate read as single-instance and start silently unfenced.
		// Shared source of truth with the OutboxProcessor constructor (OutboxFencingStartupInvariant).
		var electionRegistered = OutboxFencingStartupInvariant.IsLeaderElectionRegistered(_services);
		var leaderGate = _services.GetService<ILeaderProcessingGate>();
		var deliveryOptions = _services.GetRequiredService<IOptions<OutboxDeliveryOptions>>().Value;
		OutboxFencingStartupInvariant.EnsureFencingCapableStore(
			electionRegistered, leaderGate, deliveryOptions.SingleActiveWriter, store);
	}

	public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
