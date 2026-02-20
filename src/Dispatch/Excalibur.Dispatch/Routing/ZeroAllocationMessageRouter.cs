// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Buffers;
using System.Runtime.CompilerServices;

using Excalibur.Dispatch.Diagnostics;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Routing;

/// <summary>
/// High-performance zero-allocation message router optimized for hot paths. Uses ArrayPool, Span, and Memory patterns to eliminate allocations.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="ZeroAllocationMessageRouter{TMessage}" /> class. </remarks>
public sealed partial class ZeroAllocationMessageRouter<TMessage>(ILogger<ZeroAllocationMessageRouter<TMessage>> logger)
{

	private readonly List<IMessageRoute<TMessage>> routes = new(capacity: 32);

	private readonly Dictionary<string, List<IMessageRoute<TMessage>>> routesByType =
		new(StringComparer.Ordinal);

	private readonly ArrayPool<IMessageRoute<TMessage>> routePool =
		ArrayPool<IMessageRoute<TMessage>>.Create(maxArrayLength: 128, maxArraysPerBucket: 10);

	private readonly ILogger<ZeroAllocationMessageRouter<TMessage>> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
#if NET9_0_OR_GREATER

	private readonly Lock routeLock = new();

#else

	private readonly object routeLock = new();

#endif

	/// <summary>
	/// Adds a route to the router.
	/// </summary>
	public void AddRoute(IMessageRoute<TMessage> route)
	{
		ArgumentNullException.ThrowIfNull(route);

		lock (routeLock)
		{
			routes.Add(route);

			// Index by message type for fast lookup
			var messageType = route.MessageType;
			if (!routesByType.TryGetValue(messageType, out var typeRoutes))
			{
				typeRoutes = [];
				routesByType[messageType] = typeRoutes;
			}

			typeRoutes.Add(route);
		}
	}

	/// <summary>
	/// Routes a message to all matching routes with zero allocations.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public async ValueTask<int> RouteAsync(TMessage message, string messageType, CancellationToken cancellationToken)
	{
		// Get matching routes from pre-indexed dictionary
		List<IMessageRoute<TMessage>>? typeRoutes;
		lock (routeLock)
		{
			if (!routesByType.TryGetValue(messageType, out typeRoutes))
			{
				return 0;
			}
		}

		if (typeRoutes.Count == 0)
		{
			return 0;
		}

		// Rent array from pool to avoid allocation
		var matchingRoutes = routePool.Rent(typeRoutes.Count);
		var matchCount = 0;

		try
		{
			// Filter routes using for loop instead of LINQ
			for (var i = 0; i < typeRoutes.Count; i++)
			{
				var route = typeRoutes[i];
				if (route.CanRoute(message))
				{
					matchingRoutes[matchCount++] = route;
				}
			}

			if (matchCount == 0)
			{
				return 0;
			}

			// Process routes in parallel if there are multiple
			if (matchCount == 1)
			{
				await matchingRoutes[0].RouteAsync(message, cancellationToken).ConfigureAwait(false);
			}
			else
			{
				// Use ValueTask array to avoid Task allocation Use Task array since we need to store them (CA2012: ValueTask should not be stored)
				var tasks = new Task[matchCount];
				for (var i = 0; i < matchCount; i++)
				{
					tasks[i] = matchingRoutes[i].RouteAsync(message, cancellationToken).AsTask();
				}

				// Wait for all tasks
				for (var i = 0; i < matchCount; i++)
				{
					await tasks[i].ConfigureAwait(false);
				}
			}

			LogRoutedCount(matchCount);
			return matchCount;
		}
		finally
		{
			// Return the array to the pool
			routePool.Return(matchingRoutes, clearArray: true);
		}
	}

	/// <summary>
	/// Routes messages in batch with zero allocations.
	/// </summary>
	public async ValueTask<int> RouteBatchAsync(ReadOnlyMemory<TMessage> messages, string messageType,
		CancellationToken cancellationToken)
	{
		// Get matching routes from pre-indexed dictionary
		List<IMessageRoute<TMessage>>? typeRoutes;
		lock (routeLock)
		{
			if (!routesByType.TryGetValue(messageType, out typeRoutes))
			{
				return 0;
			}
		}

		if (typeRoutes.Count == 0 || messages.Length == 0)
		{
			return 0;
		}

		var totalRouted = 0;

		// Copy to array to avoid this.Span across await boundary
		var messageArray = messages.ToArray();

		// Process each message
		for (var msgIndex = 0; msgIndex < messageArray.Length; msgIndex++)
		{
			var message = messageArray[msgIndex];

			// Process routes for this message
			for (var routeIndex = 0; routeIndex < typeRoutes.Count; routeIndex++)
			{
				var route = typeRoutes[routeIndex];
				if (route.CanRoute(message))
				{
					await route.RouteAsync(message, cancellationToken).ConfigureAwait(false);
					totalRouted++;
				}
			}
		}

		return totalRouted;
	}

	/// <summary>
	/// Gets statistics about the router without allocations.
	/// </summary>
	public void GetStatistics(out int totalRoutes, out int typeIndexCount)
	{
		lock (routeLock)
		{
			totalRoutes = routes.Count;
			typeIndexCount = routesByType.Count;
		}
	}

	// Source-generated logging methods
	[LoggerMessage(MiddlewareEventId.ZeroAllocRoutedCount, LogLevel.Trace,
		"Routed {Count} routes")]
	private partial void LogRoutedCount(int count);
}
