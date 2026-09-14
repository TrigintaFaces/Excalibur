// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.Projections;
using Excalibur.EventSourcing.Queries;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Excalibur.EventSourcing.DependencyInjection;

/// <summary>
/// Startup-time validator that fails loud when projection rebuild is registered on a host that cannot
/// perform one.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="IProjectionRebuildService"/> reaches its collaborators two different ways, and only one of
/// them is visible to the container. The constructor takes an event serializer, options and a logger, so a
/// missing serializer is already a hard resolution failure. The global stream query and the per-projection
/// types are pulled from <see cref="IServiceProvider"/> during the rebuild itself, where the container
/// cannot see them.
/// </para>
/// <para>
/// <b>The constructor half is validated by construction, not by enumeration.</b> Resolving the service is
/// what makes the container walk its constructor, so this probe cannot fall out of date when that
/// constructor changes — which a hand-written list of dependencies would.
/// </para>
/// <para>
/// <b>Of the call-time half, exactly one precondition is host-wide.</b>
/// <see cref="IGlobalStreamQuery"/> supplies every event a rebuild replays; without it no rebuild of any
/// projection can do anything at all. It is registered by the event-store provider seam, so a host that
/// opted into rebuild without a provider has a configuration error rather than an empty result. The
/// upcasting pipeline is genuinely optional and is deliberately not checked.
/// </para>
/// <para>
/// <b>The per-projection resolutions are deliberately NOT checked, and that is not a gap.</b> The service
/// resolves <c>MultiStreamProjection&lt;TProjection&gt;</c> and <c>IProjectionStore&lt;TProjection&gt;</c>
/// against a type argument known only at the call, so no startup probe can enumerate them. It does not need
/// to: those paths already fail soft and observably — the rebuild records
/// <see cref="ProjectionRebuildState.Failed"/> against the projection's name and returns, and a caller reads
/// that through <c>IProjectionRebuildService.GetStatusAsync</c>, which is part of the shipped contract. A
/// missing projection is a per-call answer, not a host misconfiguration.
/// </para>
/// </remarks>
internal sealed class ProjectionRebuildPrerequisiteValidator : IHostedService, IStartupPrerequisiteValidator
{
	private readonly IServiceProvider _services;

	public ProjectionRebuildPrerequisiteValidator(IServiceProvider services) =>
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
				"Projection rebuild is registered, but no IGlobalStreamQuery is. A rebuild replays the global "
				+ "event stream, so without one every rebuild would report Failed against every projection and "
				+ "the host would look healthy while no projection could ever be rebuilt. The query is supplied "
				+ "by the event-store provider seam — register a provider (for example "
				+ "AddExcaliburEventSourcing(b => b.UseSqlServer(...))), or do not call AddProjectionRebuild() on "
				+ "a host that has no event stream to replay.");
		}

		// Constructing the service is what validates its constructor dependencies. The container enumerates
		// them, so this stays correct when that constructor changes; a hand-written list would not.
		_ = _services.GetRequiredService<IProjectionRebuildService>();
	}
}
