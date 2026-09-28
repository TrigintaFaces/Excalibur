// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.EventSourcing.Subscriptions;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Records that this host has accepted process-lifetime subscription checkpoints.
/// </summary>
/// <remarks>
/// Carries no state and is never resolved for behavior; its presence in the container is the whole
/// signal.
/// </remarks>
internal sealed class InMemoryProjectionCheckpointAcknowledgement;

/// <summary>
/// Opts a host into process-lifetime subscription checkpoints.
/// </summary>
public static class ProjectionCheckpointAcknowledgementServiceCollectionExtensions
{
	/// <summary>
	/// Accepts that this host's projection checkpoints do not survive a restart, and that every async
	/// projection will therefore be re-applied from the beginning of the stream each time the process
	/// starts.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>Call this only if your projection handlers are idempotent.</b> An async projection is applied
	/// by loading the stored projection, applying the event to it and writing it back, and the stored
	/// projection records no last-applied position — so nothing can detect a second application. A
	/// handler that assigns a value survives a replay; one that accumulates (incrementing a total,
	/// appending to a list) double-counts on every restart, silently and without bound.
	/// </para>
	/// <para>
	/// The alternative is to register a durable checkpoint store for your provider, which is the right
	/// answer for any process that restarts in production. This method exists for hosts where a
	/// process-lifetime checkpoint is coherent — tests, and an event store that is itself in-process.
	/// </para>
	/// </remarks>
	/// <param name="services">The service collection.</param>
	/// <returns>The service collection, for chaining.</returns>
	public static IServiceCollection AllowInMemoryProjectionCheckpoints(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		services.TryAddSingleton<InMemoryProjectionCheckpointAcknowledgement>();
		services.TryAddSingleton<ISubscriptionCheckpointStore, InMemorySubscriptionCheckpointStore>();

		return services;
	}
}
