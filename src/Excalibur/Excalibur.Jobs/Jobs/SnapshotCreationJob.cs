// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Domain.Model;
using Excalibur.EventSourcing.Abstractions;
using Excalibur.Jobs.Abstractions;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Excalibur.Jobs.Jobs;

/// <summary>
/// Abstract background job for scheduled snapshot creation of event-sourced aggregates.
/// </summary>
/// <typeparam name="TAggregate">The aggregate type to snapshot.</typeparam>
/// <typeparam name="TKey">The aggregate identifier type.</typeparam>
/// <remarks>
/// <para>
/// Subclass this job for each aggregate type that needs scheduled snapshot creation,
/// and override <see cref="GetAggregateIdsAsync"/> to provide the stream identifiers
/// that should be snapshotted.
/// </para>
/// <para>
/// This follows the Microsoft <c>BackgroundService</c> pattern: abstract base class
/// with a single method to override.
/// </para>
/// <para>
/// <b>Usage:</b>
/// <code>
/// public class OrderSnapshotJob : SnapshotCreationJob&lt;OrderAggregate, Guid&gt;
/// {
///     public OrderSnapshotJob(
///         IServiceScopeFactory scopeFactory, ILogger&lt;OrderSnapshotJob&gt; logger)
///         : base(scopeFactory, logger) { }
///
///     protected override Task&lt;IReadOnlyList&lt;Guid&gt;&gt; GetAggregateIdsAsync(
///         IServiceProvider serviceProvider, CancellationToken cancellationToken)
///     {
///         // Return aggregate IDs that need snapshotting
///         // e.g., query a read model or use a known list
///         return Task.FromResult&lt;IReadOnlyList&lt;Guid&gt;&gt;([ /* ids */ ]);
///     }
/// }
///
/// // Register with scheduler
/// configurator.AddJob&lt;OrderSnapshotJob&gt;("0 0 * * * ?"); // Every hour
/// </code>
/// </para>
/// </remarks>
public abstract class SnapshotCreationJob<TAggregate, TKey>(
	IServiceScopeFactory scopeFactory,
	ILogger logger)
	: IBackgroundJob
	where TAggregate : class, IAggregateRoot<TKey>, IAggregateSnapshotSupport
	where TKey : notnull
{
	private readonly IServiceScopeFactory _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
	private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));

	/// <summary>
	/// Gets the aggregate identifiers that should be snapshotted during this job execution.
	/// </summary>
	/// <param name="serviceProvider">The scoped service provider for resolving dependencies.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A list of aggregate identifiers to create snapshots for.</returns>
	/// <remarks>
	/// Override this method to provide the aggregate IDs that need snapshotting.
	/// Common strategies include querying a read model for recently-modified aggregates
	/// or maintaining a list of high-traffic aggregate streams.
	/// </remarks>
	protected abstract Task<IReadOnlyList<TKey>> GetAggregateIdsAsync(
		IServiceProvider serviceProvider,
		CancellationToken cancellationToken);

	/// <inheritdoc />
	[RequiresUnreferencedCode("Aggregate rehydration may require types that cannot be statically analyzed.")]
	[RequiresDynamicCode("Aggregate rehydration may require dynamic code generation.")]
	public async Task ExecuteAsync(CancellationToken cancellationToken)
	{
		SnapshotCreationJobLog.JobStarting(_logger, typeof(TAggregate).Name);

		using var scope = _scopeFactory.CreateScope();
		var repository = scope.ServiceProvider.GetService<IEventSourcedRepository<TAggregate, TKey>>();
		var snapshotManager = scope.ServiceProvider.GetService<ISnapshotManager>();

		if (repository is null || snapshotManager is null)
		{
			SnapshotCreationJobLog.DependenciesMissing(_logger, typeof(TAggregate).Name);
			return;
		}

		try
		{
			var aggregateIds = await GetAggregateIdsAsync(scope.ServiceProvider, cancellationToken)
				.ConfigureAwait(false);

			var snapshotCount = 0;

			foreach (var id in aggregateIds)
			{
				cancellationToken.ThrowIfCancellationRequested();

				var aggregate = await repository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
				if (aggregate is null)
				{
					continue;
				}

				var stringId = id.ToString() ?? string.Empty;
				var snapshot = await snapshotManager.CreateSnapshotAsync(aggregate, cancellationToken)
					.ConfigureAwait(false);
				await snapshotManager.SaveSnapshotAsync(stringId, snapshot, cancellationToken)
					.ConfigureAwait(false);

				snapshotCount++;
			}

			SnapshotCreationJobLog.JobCompleted(_logger, typeof(TAggregate).Name, snapshotCount);
		}
		catch (OperationCanceledException ex) when (ex.CancellationToken.IsCancellationRequested)
		{
			SnapshotCreationJobLog.JobCancelled(_logger, typeof(TAggregate).Name);
			throw;
		}
		catch (Exception ex)
		{
			SnapshotCreationJobLog.JobFailed(_logger, typeof(TAggregate).Name, ex);
			throw;
		}
	}
}
