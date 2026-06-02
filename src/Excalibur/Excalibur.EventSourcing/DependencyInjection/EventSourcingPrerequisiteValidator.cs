// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Excalibur.EventSourcing.DependencyInjection;

/// <summary>
/// Startup-time prerequisite validator that fails loud at <see cref="IHost.StartAsync"/>
/// if the consumer called <c>AddEventSourcing(...)</c> without registering a concrete
/// <see cref="IEventStore"/> provider (e.g., by omitting <c>.UseSqlServer(...)</c>,
/// <c>.UsePostgres(...)</c>, <c>.UseCosmosDb(...)</c>, or a consumer-supplied store).
/// </summary>
/// <remarks>
/// <para>
/// Per S809 bd-x6rg45 + COMPASS msg 2189: minimal-wiring validators must fail at host
/// start, not at first aggregate load. Registering this as an <see cref="IHostedService"/>
/// places the probe in the host's startup pipeline ahead of any domain workload.
/// </para>
/// <para>
/// AOT-safe: the probe uses <c>IServiceProvider.GetKeyedService&lt;IEventStore&gt;("default")</c>
/// — no reflection, no assembly scanning.
/// </para>
/// </remarks>
internal sealed class EventSourcingPrerequisiteValidator : IHostedService
{
	private readonly IServiceProvider _services;

	public EventSourcingPrerequisiteValidator(IServiceProvider services)
	{
		_services = services ?? throw new ArgumentNullException(nameof(services));
	}

	public Task StartAsync(CancellationToken cancellationToken)
	{
		// All providers register IEventStore as a keyed singleton (key = "default").
		// A non-keyed forwarding alias is also registered by AddExcaliburEventSourcing(),
		// but it depends on the keyed "default" — so we check keyed first.
		if (_services.GetKeyedService<IEventStore>("default") is null)
		{
			throw new InvalidOperationException(
				"Excalibur event sourcing is missing the required IEventStore implementation. " +
				"Call a provider extension inside AddEventSourcing(...) — for example " +
				"es => es.UseSqlServer(sql => sql.ConnectionString(...)), " +
				"es => es.UsePostgres(...), or es => es.UseCosmosDb(...) — before host startup.");
		}

		return Task.CompletedTask;
	}

	public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
