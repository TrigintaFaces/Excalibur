// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Messaging;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Excalibur.Saga.DependencyInjection;

/// <summary>
/// Startup-time prerequisite validator that fails loud at <see cref="IHost.StartAsync"/> if sagas were
/// configured (via <c>AddSagas(...)</c> / <c>AddExcaliburOrchestration()</c>) without registering a
/// concrete <see cref="ISagaStore"/> — by omitting both a persistent provider (e.g.
/// <c>.UseSqlServer(...)</c>) and the explicit in-memory opt-in (<c>AddInMemorySagaStore()</c> /
/// <c>ISagaBuilder.UseInMemoryStore()</c>).
/// </summary>
/// <remarks>
/// <para>
/// Saga state is as stateful as the outbox and event store, so saga registration adopts the same
/// fail-fast posture as <c>EventSourcingPrerequisiteValidator</c> and the signing-key guard: the
/// framework never silently binds an in-memory store as the production default (it loses all in-flight
/// saga state on restart/scale-out). The in-memory store remains available, but only via an explicit,
/// visible opt-in.
/// </para>
/// <para>
/// AOT-safe: the probe uses <c>IServiceProvider.GetKeyedService&lt;ISagaStore&gt;("default")</c> — no
/// reflection, no assembly scanning.
/// </para>
/// </remarks>
internal sealed class SagaPrerequisiteValidator : IHostedService
{
	private readonly IServiceProvider _services;

	public SagaPrerequisiteValidator(IServiceProvider services)
	{
		_services = services ?? throw new ArgumentNullException(nameof(services));
	}

	public Task StartAsync(CancellationToken cancellationToken)
	{
		// All saga stores register ISagaStore as the keyed "default" singleton (a non-keyed forwarding
		// alias also exists but depends on the keyed "default"), so we probe the keyed registration.
		if (_services.GetKeyedService<ISagaStore>("default") is null)
		{
			throw new InvalidOperationException(
				"Excalibur sagas are configured but no ISagaStore is registered. Register a persistent saga " +
				"store (for example a provider's UseSqlServer(...) extension) or explicitly opt into the " +
				"in-memory store via AddInMemorySagaStore() / ISagaBuilder.UseInMemoryStore() before host " +
				"startup. The in-memory store is never the silent default because it loses all in-flight " +
				"saga state on restart or scale-out.");
		}

		return Task.CompletedTask;
	}

	public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
