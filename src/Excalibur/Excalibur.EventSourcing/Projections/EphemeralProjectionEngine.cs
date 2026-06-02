// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

using Excalibur.Dispatch;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace Excalibur.EventSourcing.Projections;

/// <summary>
/// Builds projections on-demand by replaying events without persistence.
/// Supports optional caching via <see cref="IDistributedCache"/>.
/// </summary>
internal sealed class EphemeralProjectionEngine : IEphemeralProjectionEngine
{
	private static readonly JsonSerializerOptions DefaultJsonOptions = new()
	{
		PropertyNameCaseInsensitive = true,
	};

	private readonly IEventStore _eventStore;
	private readonly IEventSerializer _eventSerializer;
	private readonly IProjectionRegistry _registry;
	private readonly IDistributedCache? _cache;
	private readonly JsonSerializerOptions _jsonOptions;
	private readonly ILogger<EphemeralProjectionEngine> _logger;

	public EphemeralProjectionEngine(
		IEventStore eventStore,
		IEventSerializer eventSerializer,
		IProjectionRegistry registry,
		ILogger<EphemeralProjectionEngine> logger,
		IDistributedCache? cache = null,
		JsonSerializerOptions? jsonOptions = null)
	{
		ArgumentNullException.ThrowIfNull(eventStore);
		ArgumentNullException.ThrowIfNull(eventSerializer);
		ArgumentNullException.ThrowIfNull(registry);
		ArgumentNullException.ThrowIfNull(logger);

		_eventStore = eventStore;
		_eventSerializer = eventSerializer;
		_registry = registry;
		_logger = logger;
		_cache = cache;
		_jsonOptions = jsonOptions ?? DefaultJsonOptions;
	}

	/// <inheritdoc />
	[UnconditionalSuppressMessage("AOT", "IL3050",
		Justification = "Ephemeral projections use JSON serialization and event deserialization which are inherently dynamic.")]
	[UnconditionalSuppressMessage("Trimming", "IL2026",
		Justification = "Ephemeral projections require type metadata for JSON and event deserialization; consumers must preserve types.")]
	public async Task<TProjection> BuildAsync<TProjection>(
		string aggregateId,
		string aggregateType,
		CancellationToken cancellationToken)
		where TProjection : class, new()
	{
		ArgumentNullException.ThrowIfNull(aggregateId);
		ArgumentNullException.ThrowIfNull(aggregateType);

		var registration = _registry.GetRegistration(typeof(TProjection))
			?? throw new InvalidOperationException(
				$"No projection registration found for {typeof(TProjection).Name}. " +
				$"Register it via AddProjection<{typeof(TProjection).Name}>().");

		// Try cache first if caching is configured
		if (_cache is not null && registration.CacheTtl.HasValue)
		{
			var cacheKey = $"ephemeral:{typeof(TProjection).Name}:{aggregateId}";
			var cached = await _cache.GetAsync(cacheKey, cancellationToken).ConfigureAwait(false);
			if (cached is not null)
			{
				var deserialized = JsonSerializer.Deserialize<TProjection>(cached, _jsonOptions);
				if (deserialized is not null)
				{
					return deserialized;
				}
			}
		}

		// Load all events for the aggregate
		var storedEvents = await _eventStore.LoadAsync(aggregateId, aggregateType, cancellationToken)
			.ConfigureAwait(false);

		// Create a fresh projection instance -- no shared mutable state
		var projection = new TProjection();

		// Get the multi-stream projection containing the registered When<T> handlers
		var multiStreamProjection = (MultiStreamProjection<TProjection>)registration.Projection;

		// Apply all events through the same handlers used by inline/async (R27.42)
		foreach (var storedEvent in storedEvents)
		{
			var eventType = _eventSerializer.ResolveType(storedEvent.EventType);
			var domainEvent = _eventSerializer.DeserializeEvent(storedEvent.EventData, eventType);

			if (domainEvent is null)
			{
				_logger.LogWarning(
					"Skipping event {EventId} ({EventType}) during ephemeral build — deserialization returned null.",
					storedEvent.EventId, storedEvent.EventType);
				continue;
			}

			multiStreamProjection.Apply(projection, domainEvent);
		}

		// Cache the result if caching is configured
		if (_cache is not null && registration.CacheTtl.HasValue)
		{
			var cacheKey = $"ephemeral:{typeof(TProjection).Name}:{aggregateId}";
			var serialized = JsonSerializer.SerializeToUtf8Bytes(projection, _jsonOptions);
			var options = new DistributedCacheEntryOptions
			{
				AbsoluteExpirationRelativeToNow = registration.CacheTtl.Value,
			};
			await _cache.SetAsync(cacheKey, serialized, options, cancellationToken).ConfigureAwait(false);
		}

		return projection;
	}
}
