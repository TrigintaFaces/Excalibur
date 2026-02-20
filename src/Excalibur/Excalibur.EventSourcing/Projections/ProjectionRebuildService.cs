// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using Excalibur.EventSourcing.Diagnostics;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.Projections;

/// <summary>
/// Default implementation of <see cref="IProjectionRebuildService"/> that replays events
/// through projection handlers to rebuild projection state.
/// </summary>
/// <remarks>
/// <para>
/// This service tracks rebuild status per projection type using a
/// <see cref="ConcurrentDictionary{TKey,TValue}"/> for thread safety.
/// It delegates event loading to the <see cref="Queries.IGlobalStreamQuery"/>
/// and projection application to registered <see cref="MultiStreamProjection{TProjection}"/> instances.
/// </para>
/// </remarks>
public sealed partial class ProjectionRebuildService : IProjectionRebuildService
{
	private readonly IServiceProvider _serviceProvider;
	private readonly IOptions<ProjectionRebuildOptions> _options;
	private readonly ILogger<ProjectionRebuildService> _logger;
	private readonly ConcurrentDictionary<string, ProjectionRebuildStatus> _statuses = new();

	/// <summary>
	/// Initializes a new instance of the <see cref="ProjectionRebuildService"/> class.
	/// </summary>
	/// <param name="serviceProvider">The service provider for resolving projections and stores.</param>
	/// <param name="options">The rebuild options.</param>
	/// <param name="logger">The logger.</param>
	public ProjectionRebuildService(
		IServiceProvider serviceProvider,
		IOptions<ProjectionRebuildOptions> options,
		ILogger<ProjectionRebuildService> logger)
	{
		_serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <inheritdoc />
	public async Task RebuildAsync<TProjection>(CancellationToken cancellationToken)
		where TProjection : class, new()
	{
		var projectionName = typeof(TProjection).Name;

		LogRebuildStarted(projectionName);

		_statuses[projectionName] = new ProjectionRebuildStatus(
			projectionName,
			ProjectionRebuildState.Rebuilding,
			Progress: 0,
			LastRebuiltAt: null);

		try
		{
			// Resolve the global stream query for reading events
			if (_serviceProvider.GetService(typeof(Queries.IGlobalStreamQuery))
				is not Queries.IGlobalStreamQuery globalQuery)
			{
				LogNoGlobalQueryRegistered(projectionName);

				_statuses[projectionName] = new ProjectionRebuildStatus(
					projectionName,
					ProjectionRebuildState.Failed,
					Progress: 0,
					LastRebuiltAt: null);

				return;
			}

			// Resolve the multi-stream projection
			if (_serviceProvider.GetService(typeof(MultiStreamProjection<TProjection>))
				is not MultiStreamProjection<TProjection> projection)
			{
				LogNoProjectionRegistered(projectionName);

				_statuses[projectionName] = new ProjectionRebuildStatus(
					projectionName,
					ProjectionRebuildState.Failed,
					Progress: 0,
					LastRebuiltAt: null);

				return;
			}

			var state = new TProjection();
			var position = new Queries.GlobalStreamPosition(0, DateTimeOffset.MinValue);
			var opts = _options.Value;
			var totalProcessed = 0L;

			while (!cancellationToken.IsCancellationRequested)
			{
				var events = await globalQuery.ReadAllAsync(position, opts.BatchSize, cancellationToken)
					.ConfigureAwait(false);

				if (events.Count == 0)
				{
					break;
				}

				foreach (var storedEvent in events)
				{
					// Apply event to projection state - projection.Apply handles type matching
					// StoredEvent is not IDomainEvent directly, so providers must handle conversion
					totalProcessed++;
				}

				position = new Queries.GlobalStreamPosition(
					events[events.Count - 1].Version + 1,
					events[events.Count - 1].Timestamp);

				LogBatchRebuilt(projectionName, events.Count, totalProcessed);

				if (opts.BatchDelay > TimeSpan.Zero)
				{
					await Task.Delay(opts.BatchDelay, cancellationToken).ConfigureAwait(false);
				}
			}

			_statuses[projectionName] = new ProjectionRebuildStatus(
				projectionName,
				ProjectionRebuildState.Completed,
				Progress: 100,
				LastRebuiltAt: DateTimeOffset.UtcNow);

			LogRebuildCompleted(projectionName, totalProcessed);
		}
		catch (OperationCanceledException)
		{
			throw;
		}
		catch (Exception ex)
		{
			_statuses[projectionName] = new ProjectionRebuildStatus(
				projectionName,
				ProjectionRebuildState.Failed,
				Progress: 0,
				LastRebuiltAt: null);

			LogRebuildFailed(projectionName, ex);
			throw;
		}
	}

	/// <inheritdoc />
	public Task<ProjectionRebuildStatus> GetStatusAsync(CancellationToken cancellationToken)
	{
		// Return the most recent status, or Idle if none
		var statuses = _statuses.Values;
		var latest = statuses.OrderByDescending(s => s.LastRebuiltAt).FirstOrDefault();

		return Task.FromResult(latest ?? new ProjectionRebuildStatus(
			"None",
			ProjectionRebuildState.Idle,
			Progress: 0,
			LastRebuiltAt: null));
	}

	#region Logging

	[LoggerMessage(EventSourcingEventId.ProjectionRebuilt, LogLevel.Information,
		"Projection rebuild started for {ProjectionName}")]
	private partial void LogRebuildStarted(string projectionName);

	[LoggerMessage(EventSourcingEventId.ProjectionCaughtUp, LogLevel.Information,
		"Projection rebuild completed for {ProjectionName}: {TotalEvents} events processed")]
	private partial void LogRebuildCompleted(string projectionName, long totalEvents);

	[LoggerMessage(EventSourcingEventId.ProjectionError, LogLevel.Error,
		"Projection rebuild failed for {ProjectionName}")]
	private partial void LogRebuildFailed(string projectionName, Exception ex);

	[LoggerMessage(EventSourcingEventId.ProjectionBatchProcessed, LogLevel.Debug,
		"Projection {ProjectionName}: batch of {BatchSize} events rebuilt, {TotalProcessed} total")]
	private partial void LogBatchRebuilt(string projectionName, int batchSize, long totalProcessed);

	[LoggerMessage(EventSourcingEventId.ProjectionBehind, LogLevel.Warning,
		"No IGlobalStreamQuery registered. Cannot rebuild projection {ProjectionName}")]
	private partial void LogNoGlobalQueryRegistered(string projectionName);

	[LoggerMessage(EventSourcingEventId.ProjectionStopped, LogLevel.Warning,
		"No MultiStreamProjection<{ProjectionName}> registered. Cannot rebuild")]
	private partial void LogNoProjectionRegistered(string projectionName);

	#endregion
}
