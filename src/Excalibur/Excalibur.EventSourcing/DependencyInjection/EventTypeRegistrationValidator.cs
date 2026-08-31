// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Excalibur.EventSourcing.DependencyInjection;

/// <summary>
/// Startup-time guard that fails loud at <see cref="IHost.StartAsync"/> when event sourcing is wired with
/// the default reflection <see cref="JsonEventSerializer"/> but <em>no</em> event types have been
/// registered — a configuration in which every aggregate load/replay throws
/// <c>UnknownEventTypeException</c> at runtime because the secure default serializer rejects unregistered
/// types.
/// </summary>
/// <remarks>
/// <para>
/// The secure default (the serializer rejecting unregistered types) is intentional and stays. This guard
/// only converts a certain, silent runtime brick into an honest startup failure that names the fix —
/// registration is build-time-frozen (the only registration path is the DI-config-time
/// <c>AddEventTypes*</c> / <c>RegisterEventTypes*</c> helpers; there is no supported runtime path), so an
/// empty registry at host start can never become non-empty later and the fail-fast can never
/// false-positive.
/// </para>
/// <para>
/// The guard is inert for every non-bricking configuration: a consumer-supplied or AOT serializer (not
/// <see cref="JsonEventSerializer"/>), or a populated registry, both pass silently. AOT-safe: no
/// reflection, no assembly scanning.
/// </para>
/// </remarks>
internal sealed class EventTypeRegistrationValidator : IHostedService, IStartupPrerequisiteValidator
{
	private readonly IServiceProvider _services;

	public EventTypeRegistrationValidator(IServiceProvider services)
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
		// Only the default reflection serializer rejects unregistered types. A consumer-supplied or AOT
		// serializer is exempt — it owns its own type-resolution contract.
		if (_services.GetService<IEventSerializer>() is not JsonEventSerializer)
		{
			return;
		}

		// An empty allow-list + the type-rejecting default serializer bricks every replay. "Empty" means
		// either no registry was registered at all (no AddEventTypes*/RegisterEventTypes* call — the
		// default serializer then resolves against a null registry and rejects everything) OR the internal
		// EventTypeRegistry (InternalsVisibleTo) exists but holds no types. A consumer-supplied custom
		// IEventTypeRegistry is exempt — it owns its own resolution contract.
		var registry = _services.GetService<IEventTypeRegistry>();
		if (registry is null or EventTypeRegistry { IsEmpty: true })
		{
			throw new InvalidOperationException(
				"Excalibur event sourcing is registered with the default JSON event serializer, which "
				+ "rejects unregistered event types for security, but no event types have been registered. "
				+ "Every aggregate load/replay would fail at runtime. Register your event types at startup — "
				+ "for example AddEventSourcing(es => es.RegisterEventTypesFromAssembly(typeof(Program).Assembly)), "
				+ "or services.AddEventTypes<MyEvent>() — before host startup. (An AOT or consumer-supplied "
				+ "IEventSerializer is exempt from this check.)");
		}
	}

	public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
