// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.EventSourcing.Queries;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Excalibur.EventSourcing.DependencyInjection;

/// <summary>
/// Refuses, at startup, a composition in which the event-store health check can never take its probe.
/// </summary>
/// <remarks>
/// <para>
/// The check reads the global stream, so it needs <see cref="IGlobalStreamQuery" />. Without one the
/// check cannot be constructed, and the failure would otherwise surface when the health endpoint is
/// first scraped — which is the worst time, because a readiness probe that throws is reported by most
/// orchestrators as an unhealthy application rather than as a misconfiguration, and the deployment then
/// looks broken in a way that points nowhere near the registration that caused it.
/// </para>
/// <para>
/// Refusing at startup is also the only honest alternative to the two bad options. Handing back a check
/// that probes something else would make a green mean one thing on one composition and another thing on
/// another, with nothing in the result saying which. Handing back a check that always throws would
/// convert a registration mistake into a permanent outage signal.
/// </para>
/// <para>
/// This runs as BOTH a hosted service and a startup prerequisite validator, matching the sibling
/// validator in this package: the second registration is the load-bearing one for a host that runs no
/// hosted services, since such a host invokes the validators through its explicit startup gate.
/// </para>
/// </remarks>
internal sealed class EventStoreHealthCheckPrerequisiteValidator : IHostedService, IStartupPrerequisiteValidator
{
	private readonly IServiceProvider _services;

	public EventStoreHealthCheckPrerequisiteValidator(IServiceProvider services) =>
		_services = services ?? throw new ArgumentNullException(nameof(services));

	public Task StartAsync(CancellationToken cancellationToken)
	{
		Validate();
		return Task.CompletedTask;
	}

	public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

	public void Validate()
	{
		// RESOLVED, not merely asked about. A registration test answers "is it registered"; this check
		// owes "can it be built", and the two come apart on a real composition: the SQL Server provider
		// registers the global-stream reader through a factory that THROWS when a cold store is present
		// and the primary connection factory was supplied by the consumer rather than owned. A
		// registration test admits that composition, and the throw then lands in the health-check
		// registration's own factory -- which runs lazily, at the first scrape. That is the exact
		// failure this validator exists to move to startup, arriving through a different door.
		//
		// Resolving costs nothing measurable and no I/O: the provider's constructor assigns its
		// connection factory and formats a table name, and the binding factory opens no connection. On
		// a cold-start-sensitive host this builds one singleton the first scrape would build anyway.
		if (_services.GetService<IGlobalStreamQuery>() is null)
		{
			throw new InvalidOperationException(
				"The event-store health check is registered, but no global-stream reader is registered. "
				+ "The check verifies that the event table and the columns its read statement binds exist "
				+ "by reading the global stream, so without that reader it has nothing to probe. Register "
				+ "an event-store provider that supplies one (for example "
				+ "AddExcaliburEventSourcing(b => b.UseSqlServer(...)), or the Postgres, Oracle, SQLite or "
				+ "in-memory provider), or remove the AddEventStoreHealthCheck call. Document-store "
				+ "providers do not supply a global-stream reader, so they cannot serve this check; a "
				+ "connectivity check against the store is the available alternative, and it verifies "
				+ "reachability only, not schema.");
		}
	}
}
