// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Excalibur.Inbox.DependencyInjection;

/// <summary>
/// Startup-time prerequisite validator that fails loud at <see cref="IHost.StartAsync"/>
/// if the consumer called <c>AddInbox(...)</c> without registering a concrete
/// <see cref="IInboxStore"/> provider (e.g., by omitting <c>.UseSqlServer(...)</c>,
/// <c>.UsePostgres(...)</c>, <c>.UseInMemory()</c>, or a consumer-supplied store), or
/// registered a store that cannot atomically claim (<see cref="IClaimableInboxStore"/>) —
/// which would silently degrade the exactly-once concurrent-duplicate guard to a racy
/// check-then-act.
/// </summary>
/// <remarks>
/// <para>
/// minimal-wiring validators must fail at host
/// start, not at first message deduplication. Registering this as an <see cref="IHostedService"/>
/// places the probe in the host's startup pipeline ahead of any domain workload.
/// </para>
/// <para>
/// AOT-safe: the probe uses <c>IServiceProvider.GetKeyedService&lt;IInboxStore&gt;("default")</c>
/// — no reflection, no assembly scanning.
/// </para>
/// </remarks>
internal sealed class InboxPrerequisiteValidator : IHostedService, IStartupPrerequisiteValidator
{
	private readonly IServiceProvider _services;

	public InboxPrerequisiteValidator(IServiceProvider services)
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
		var store = _services.GetKeyedService<IInboxStore>("default")
			?? throw new InvalidOperationException(
				"Excalibur inbox is missing the required IInboxStore implementation. " +
				"Call a provider extension inside AddInbox(...) — for example " +
				"i => i.UseSqlServer(sql => sql.ConnectionString(...)), " +
				"i => i.UsePostgres(...), or i => i.UseInMemory() — before host startup.");

		// Fail-closed capability gate: the inbox admits exactly one of N concurrent duplicate deliveries by
		// atomically CLAIMING a message before the handler runs. A store that cannot claim atomically would
		// silently fall back to a non-atomic check-then-act, under which concurrent duplicates both execute the
		// handler — so a non-claimable store must FAIL TO START here, not throw NotSupportedException at first
		// deduplication. Probe the EFFECTIVE capability (IInboxStoreCapabilities) so a decorator that statically
		// declares IClaimableInboxStore while wrapping a non-claimable inner is still rejected — matching the
		// delivery-path IdempotencyClaimCapabilityValidator so both AddInbox and AddDelivery fail fast identically.
		var supportsClaim = store is IInboxStoreCapabilities capabilities
			? capabilities.SupportsClaim
			: store is IClaimableInboxStore;

		if (!supportsClaim)
		{
			throw new InvalidOperationException(
				$"The registered Excalibur inbox store '{store.GetType().FullName}' does not support atomic claiming " +
				$"({nameof(IClaimableInboxStore)}). The inbox admits exactly one of N concurrent duplicate deliveries by " +
				"atomically claiming a message before the handler runs; without the capability it would degrade to a " +
				"non-atomic check-then-act under which concurrent duplicates can both execute the handler. Register an " +
				"inbox store that supports atomic claiming (the in-memory, SQL Server, and PostgreSQL inbox stores do), " +
				"or implement IClaimableInboxStore on your custom inbox store.");
		}
	}

	public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
