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
	/// <summary>The exact projection type whose resolved store is probed.</summary>
	Type ProjectionType { get; }

	/// <summary>The projection type this probe covers, for the failure message.</summary>
	string ProjectionTypeName { get; }

	/// <summary>
	/// Whether the store this projection resolves to presents the positioned capability.
	/// </summary>
	/// <param name="services">The provider, resolved exactly as the apply path resolves it.</param>
	/// <returns><see langword="true"/> when the capability is reachable.</returns>
	bool IsPositioned(IServiceProvider services);

	/// <summary>Waits for the store's configured query visibility, including replayed or skipped folds.</summary>
	Task EnsureVisibleAsync(IServiceProvider services, CancellationToken cancellationToken);

	/// <summary>Gets the provider's configured target identity within the consumer subscription scope.</summary>
	string? GetDestinationIdentity(IServiceProvider services);

	/// <summary>Gets the actual destination cluster configuration, when available.</summary>
	string? GetDestinationScope(IServiceProvider services) => null;
}

/// <inheritdoc />
internal sealed class PositionedProjectionProbe<TProjection> : IPositionedProjectionProbe
	where TProjection : class
{
	/// <inheritdoc />
	public Type ProjectionType => typeof(TProjection);

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
	/// <inheritdoc />
	public string? GetDestinationIdentity(IServiceProvider services)
	{
		var store = services.GetRequiredService<IProjectionStore<TProjection>>();
		if (store.GetService(typeof(IProjectionDestinationIdentity)) is not IProjectionDestinationIdentity destination)
		{
			return null;
		}
		var identity = destination.Identity;
		if (string.IsNullOrWhiteSpace(identity))
		{
			throw new InvalidOperationException($"Projection '{ProjectionTypeName}' advertised an empty destination identity.");
		}
		return identity;
	}

	public string? GetDestinationScope(IServiceProvider services)
	{
		var store = services.GetRequiredService<IProjectionStore<TProjection>>();
		return (store.GetService(typeof(IProjectionDestinationScope)) as IProjectionDestinationScope)?.Scope;
	}

	/// <inheritdoc />
	public Task EnsureVisibleAsync(IServiceProvider services, CancellationToken cancellationToken)
	{
		var store = services.GetRequiredService<IProjectionStore<TProjection>>();
		return store.GetService(typeof(IProjectionVisibilityBarrier)) is IProjectionVisibilityBarrier barrier
			? barrier.EnsureVisibleAsync(cancellationToken)
			: Task.CompletedTask;
	}

}

/// <summary>
/// Additional opt-in startup guard that probes registered projection stores for positioned writes.
/// </summary>
/// <remarks>
/// <para>
/// This guard supplies extra startup coverage for consumers and custom processing paths that request it.
/// The built-in persisted asynchronous processor independently requires positioned stores during startup
/// and each apply invocation. Omitting this optional guard does not permit that processor to fall back
/// to unconditional writes. Ephemeral projections persist no rows and have a different processing path.
/// </para>
/// <para>
/// The probe asks the resolved store, including decorators, for its capability. A raw provider registration
/// is insufficient evidence when a decorator can hide the capability the apply path would receive.
/// A successful probe establishes capability availability, not correctness of the provider's atomic writes.
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
			+ "can detect it. Use a provider that exposes positioned writes through the complete decorator "
			+ "chain. The built-in persisted asynchronous processor always requires this capability; "
			+ "disabling this additional startup guard is not an opt-out for that processor.");
	}
}
