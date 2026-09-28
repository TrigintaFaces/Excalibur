// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.EventSourcing.Projections;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Excalibur.EventSourcing.DependencyInjection;

/// <summary>
/// Checks one projection type's store for the positioned-write capability.
/// </summary>
/// <remarks>
/// Closed over the projection type at registration, so the check needs no reflection and stays
/// AOT-safe. A non-generic interface lets the validator enumerate every registered projection without
/// knowing any of their types.
/// </remarks>
internal interface IPositionedProjectionProbe
{
	/// <summary>The projection type this probe covers, for the failure message.</summary>
	string ProjectionTypeName { get; }

	/// <summary>
	/// Whether the store this projection resolves to presents the positioned capability.
	/// </summary>
	/// <param name="services">The provider, resolved exactly as the apply path resolves it.</param>
	/// <returns><see langword="true"/> when the capability is reachable.</returns>
	bool IsPositioned(IServiceProvider services);
}

/// <inheritdoc />
internal sealed class PositionedProjectionProbe<TProjection> : IPositionedProjectionProbe
	where TProjection : class
{
	/// <inheritdoc />
	public string ProjectionTypeName => typeof(TProjection).Name;

	/// <inheritdoc />
	public bool IsPositioned(IServiceProvider services)
	{
		ArgumentNullException.ThrowIfNull(services);

		// Resolved the way the APPLY PATH resolves it, decorators included. Checking the raw
		// registration instead would pass while the chain silently drops the capability -- the
		// decorator chain is precisely the thing that can drop it.
		var store = services.GetService<IProjectionStore<TProjection>>();
		return store is not null && PositionedProjectionWriter<TProjection>.Resolve(store) is not null;
	}
}

/// <summary>
/// Startup guard: refuses to start when a host has asked for position-conditional projection writes
/// and a projection's store cannot provide them.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this exists rather than a silent fallback.</b> The apply path can write either way: through
/// the positioned capability, which refuses a write that does not advance the projection's position, or
/// through the plain unconditional upsert. Those are not two speeds of the same answer — the plain
/// write is the double application the capability exists to make impossible. Degrading to it silently
/// would hand back exactly the defect, reported as success.
/// </para>
/// <para>
/// So the choice is made once, out loud, at startup: a host that asks for the guarantee gets it or
/// stops. This is deliberately unlike the optional capabilities beside it — paging and existence checks
/// correctly fall back, because falling back costs a round trip and nothing else.
/// </para>
/// <para>
/// <b>It probes the RESOLVED store, not the registration.</b> A capability can be present on the
/// provider and absent from the object the apply path actually receives, because a decorator in between
/// declined to mediate it. Resolving through the same path the apply uses is the only check that sees
/// what the writer will see.
/// </para>
/// </remarks>
internal sealed class PositionedProjectionWriteValidator : IHostedService, IStartupPrerequisiteValidator
{
	private readonly IServiceProvider _services;

	public PositionedProjectionWriteValidator(IServiceProvider services) =>
		_services = services ?? throw new ArgumentNullException(nameof(services));

	public Task StartAsync(CancellationToken cancellationToken)
	{
		Validate();
		return Task.CompletedTask;
	}

	public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

	public void Validate()
	{
		var unpositioned = new List<string>();

		foreach (var probe in _services.GetServices<IPositionedProjectionProbe>())
		{
			if (!probe.IsPositioned(_services))
			{
				unpositioned.Add(probe.ProjectionTypeName);
			}
		}

		if (unpositioned.Count == 0)
		{
			return;
		}

		throw new InvalidOperationException(
			"Position-conditional projection writes are required, but these projections resolve to a store "
			+ "that does not provide them: "
			+ string.Join(", ", unpositioned)
			+ ". Without the capability the apply path writes unconditionally, so an event re-delivered "
			+ "after a restart or a reconnect is folded into the projection a second time — an "
			+ "accumulating projection double-counts silently and without bound, and nothing downstream "
			+ "can detect it. Use a provider that supports positioned writes (SQL Server or PostgreSQL, "
			+ "with the last-applied-position migration applied), remove the decorator that is dropping "
			+ "the capability, or do not require positioned writes on this host.");
	}
}
