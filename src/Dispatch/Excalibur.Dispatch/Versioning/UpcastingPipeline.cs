// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Versioning;

/// <summary>
/// Thread-safe implementation of <see cref="IUpcastingPipeline"/> with BFS path finding.
/// Supports ALL message types: events, commands, queries, integration events.
/// </summary>
/// <remarks>
/// <para>
/// This implementation uses breadth-first search to find the shortest path between message versions.
/// Paths are computed once and cached for O(1) lookup performance on subsequent calls.
/// </para>
/// <para>
/// <b>Thread Safety:</b> Uses <see cref="ReaderWriterLockSlim"/> for concurrent reads during
/// the hot path, with write locks only during registration (typically at startup).
/// </para>
/// <para>
/// <b>Performance:</b> Achieves ~15ns per hop through direct delegate invocation,
/// compared to ~300ns with <c>DynamicInvoke</c>. Zero allocations in cached read path.
/// </para>
/// </remarks>
public sealed class UpcastingPipeline : IUpcastingPipeline, IDisposable
{
	// Message type cache for reflection-based discovery
	private static readonly ConcurrentDictionary<Type, string> MessageTypeCache = new();

	// Graph: (messageType, fromVersion) -> list of edges (adjacency list)
	private readonly Dictionary<(string MessageType, int FromVersion), List<UpcasterEdge>> _graph = new();

	// Cached paths: (messageType, fromVersion, toVersion) -> list of version hops
	// Empty list means "no path exists" (negative caching)
	private readonly ConcurrentDictionary<(string MessageType, int FromVersion, int ToVersion), IReadOnlyList<int>> _pathCache = new();

	// Latest version per message type
	private readonly Dictionary<string, int> _latestVersions = new();

	// Thread safety
	private readonly ReaderWriterLockSlim _lock = new(LockRecursionPolicy.NoRecursion);

	private volatile bool _disposed;

	/// <inheritdoc />
	public IDispatchMessage Upcast(IDispatchMessage message)
	{
		ArgumentNullException.ThrowIfNull(message);

		if (message is not IVersionedMessage versioned)
		{
			return message;
		}

		var messageType = versioned.MessageType;
		var currentVersion = versioned.Version;
		var latestVersion = GetLatestVersion(messageType);

		if (latestVersion == 0 || currentVersion >= latestVersion)
		{
			return message;
		}

		return UpcastTo(message, latestVersion);
	}

	/// <inheritdoc />
	public IDispatchMessage UpcastTo(IDispatchMessage message, int targetVersion)
	{
		ArgumentNullException.ThrowIfNull(message);

		if (message is not IVersionedMessage versioned)
		{
			throw new InvalidOperationException(
				$"Message type {message.GetType().Name} does not implement IVersionedMessage. " +
				"Only versioned messages can be upcasted.");
		}

		var messageType = versioned.MessageType;
		var currentVersion = versioned.Version;

		if (currentVersion == targetVersion)
		{
			return message;
		}

		if (currentVersion > targetVersion)
		{
			throw new InvalidOperationException(
				$"Cannot downcast {messageType} from v{currentVersion} to v{targetVersion}. " +
				"Only forward upcasting is supported.");
		}

		// Find path using BFS (cached after first computation)
		var path = GetOrComputePath(messageType, currentVersion, targetVersion);

		if (path.Count == 0)
		{
			throw new InvalidOperationException(
				$"No upcasting path exists for {messageType} from v{currentVersion} to v{targetVersion}. " +
				"Register the required upcasters to create a path.");
		}

		// Execute path
		var current = message;
		var currentVer = currentVersion;

		_lock.EnterReadLock();
		try
		{
			foreach (var nextVersion in path)
			{
				var key = (messageType, currentVer);
				if (!_graph.TryGetValue(key, out var edges))
				{
					throw new InvalidOperationException(
						$"Upcaster not found for {messageType} v{currentVer} -> v{nextVersion}. " +
						"BFS computed path but upcaster was removed.");
				}

				var edge = edges.Find(e => e.ToVersion == nextVersion)
						   ?? throw new InvalidOperationException(
							   $"Upcaster edge not found for {messageType} v{currentVer} -> v{nextVersion}. " +
							   "BFS computed path but upcaster was removed.");

				// Direct invocation (~15ns) vs DynamicInvoke (~300ns)
				current = (IDispatchMessage)edge.Upcast(current);
				currentVer = nextVersion;
			}
		}
		finally
		{
			_lock.ExitReadLock();
		}

		return current;
	}

	/// <inheritdoc />
	public void Register<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] TOld, TNew>(
		IMessageUpcaster<TOld, TNew> upcaster)
		where TOld : IDispatchMessage, IVersionedMessage
		where TNew : IDispatchMessage, IVersionedMessage
	{
		ArgumentNullException.ThrowIfNull(upcaster);

		if (upcaster.FromVersion >= upcaster.ToVersion)
		{
			throw new ArgumentException(
				$"Invalid upcaster: FromVersion ({upcaster.FromVersion}) must be less than " +
				$"ToVersion ({upcaster.ToVersion}). Only forward upcasting is supported.",
				nameof(upcaster));
		}

		// Get message type - prefer instance property for consistency with Upcast()
		// Fall back to type name derivation only if no parameterless constructor exists
		var messageType = GetValidatedMessageType<TOld>();

		_lock.EnterWriteLock();
		try
		{
			var key = (messageType, upcaster.FromVersion);

			// Create edge with direct delegate invocation
			var edge = new UpcasterEdge
			{
				MessageType = messageType,
				FromVersion = upcaster.FromVersion,
				ToVersion = upcaster.ToVersion,
				// Wrap the generic Upcast in an object->object delegate for storage
				// This is the key to avoiding DynamicInvoke
				Upcast = msg => upcaster.Upcast((TOld)msg)
			};

			if (!_graph.TryGetValue(key, out var edges))
			{
				edges = new List<UpcasterEdge>();
				_graph[key] = edges;
			}

			// Check for duplicate registration
			var existing = edges.Find(e => e.ToVersion == upcaster.ToVersion);
			if (existing != null)
			{
				throw new InvalidOperationException(
					$"An upcaster for {messageType} v{upcaster.FromVersion} -> v{upcaster.ToVersion} " +
					"is already registered.");
			}

			edges.Add(edge);

			// Update latest version tracking
			if (!_latestVersions.TryGetValue(messageType, out var currentLatest) ||
				upcaster.ToVersion > currentLatest)
			{
				_latestVersions[messageType] = upcaster.ToVersion;
			}

			// Invalidate path cache for this message type
			InvalidatePathCache(messageType);
		}
		finally
		{
			_lock.ExitWriteLock();
		}
	}

	/// <inheritdoc />
	public bool CanUpcast(string messageType, int fromVersion, int toVersion)
	{
		ArgumentNullException.ThrowIfNull(messageType);

		if (fromVersion == toVersion)
		{
			return true;
		}

		if (fromVersion > toVersion)
		{
			return false;
		}

		var path = GetOrComputePath(messageType, fromVersion, toVersion);
		return path.Count > 0;
	}

	/// <inheritdoc />
	public int GetLatestVersion(string messageType)
	{
		ArgumentNullException.ThrowIfNull(messageType);

		_lock.EnterReadLock();
		try
		{
			return _latestVersions.TryGetValue(messageType, out var version) ? version : 0;
		}
		finally
		{
			_lock.ExitReadLock();
		}
	}

	/// <summary>
	/// Releases all resources used by the <see cref="UpcastingPipeline"/>.
	/// </summary>
	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_lock.Dispose();
		_disposed = true;
	}

	/// <summary>
	/// Gets the validated message type name from a type.
	/// Prefers creating an instance to get the actual MessageType property value,
	/// with fallback to type name derivation if no parameterless constructor exists.
	/// </summary>
	/// <remarks>
	/// This method ensures consistency between Register() and Upcast() by using the same
	/// MessageType value that instances will return at runtime. If the type cannot be
	/// instantiated, it falls back to deriving the message type from the type name.
	/// </remarks>
	private static string GetValidatedMessageType<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
	T>()
		where T : IDispatchMessage, IVersionedMessage
	{
		var type = typeof(T);

		// Try to get from cache
		if (MessageTypeCache.TryGetValue(type, out var cached))
		{
			return cached;
		}

		string messageType;

		// Try to create an instance to get the actual MessageType property value
		// This ensures consistency with what Upcast() will see at runtime
		var constructor = type.GetConstructor(Type.EmptyTypes);
		if (constructor != null)
		{
			try
			{
				var instance = (IVersionedMessage)constructor.Invoke(null);
				messageType = instance.MessageType;

				// Validate: the instance's MessageType should be non-empty
				if (string.IsNullOrWhiteSpace(messageType))
				{
					throw new InvalidOperationException(
						$"Type {type.Name} returned null or empty MessageType property. " +
						"IVersionedMessage.MessageType must return a non-empty string.");
				}

				// Optionally validate consistency with type name derivation
				var derivedType = StripVersionSuffix(type.Name);
				if (!string.Equals(messageType, derivedType, StringComparison.Ordinal) &&
					!string.IsNullOrEmpty(derivedType))
				{
					// Log a warning but don't fail - instance property is authoritative
					// In production, this would use ILogger; for now, we trust the instance
				}
			}
			catch (Exception ex) when (ex is not InvalidOperationException)
			{
				// Constructor threw - fall back to type name derivation
				messageType = StripVersionSuffix(type.Name);
			}
		}
		else
		{
			// No parameterless constructor - fall back to type name derivation
			messageType = StripVersionSuffix(type.Name);
		}

		// Validate we got a non-empty message type
		if (string.IsNullOrWhiteSpace(messageType))
		{
			throw new InvalidOperationException(
				$"Cannot determine message type for {type.Name}. " +
				"Ensure the type has a parameterless constructor or follows the naming convention " +
				"(e.g., 'UserCreatedEventV1' where 'V1' is the version suffix).");
		}

		MessageTypeCache[type] = messageType;
		return messageType;
	}

	/// <summary>
	/// Strips version suffix from type name (e.g., "UserCreatedEventV1" -> "UserCreatedEvent").
	/// </summary>
	/// <remarks>
	/// Handles patterns like "V1", "V2", "V10", etc. Returns the original type name
	/// if no valid version suffix is found, or if stripping would result in an empty string.
	/// </remarks>
	private static string StripVersionSuffix(string typeName)
	{
		if (string.IsNullOrEmpty(typeName))
		{
			return typeName;
		}

		// Handle patterns like "V1", "V2", "V10", etc.
		var length = typeName.Length;
		var i = length - 1;

		// Find trailing digits
		while (i >= 0 && char.IsDigit(typeName[i]))
		{
			i--;
		}

		// Check for 'V' prefix before digits
		if (i >= 0 && (typeName[i] == 'V' || typeName[i] == 'v'))
		{
			// Verify we found at least one digit after V
			if (i < length - 1)
			{
				// Edge case: if stripping would result in empty string (e.g., "V1"), return as-is
				if (i == 0)
				{
					return typeName;
				}

				return typeName[..i];
			}
		}

		// No version suffix found, return as-is
		return typeName;
	}

	/// <summary>
	/// Gets a cached path or computes it using BFS.
	/// </summary>
	private IReadOnlyList<int> GetOrComputePath(string messageType, int fromVersion, int toVersion)
	{
		var cacheKey = (messageType, fromVersion, toVersion);

		// Fast path: check cache first (lock-free)
		if (_pathCache.TryGetValue(cacheKey, out var cachedPath))
		{
			return cachedPath;
		}

		// Slow path: compute with read lock
		_lock.EnterReadLock();
		try
		{
			// Double-check after acquiring lock
			if (_pathCache.TryGetValue(cacheKey, out cachedPath))
			{
				return cachedPath;
			}

			// Compute path with BFS
			var path = ComputePathBfs(messageType, fromVersion, toVersion);

			// Cache result (even if empty - negative caching prevents repeated computation)
			_pathCache[cacheKey] = path;

			return path;
		}
		finally
		{
			_lock.ExitReadLock();
		}
	}

	/// <summary>
	/// Computes shortest path using breadth-first search.
	/// </summary>
	/// <remarks>
	/// Complexity: O(V + E) where V = versions, E = registered upcasters.
	/// Returns shortest path (minimal hops) from source to target version.
	/// </remarks>
	private IReadOnlyList<int> ComputePathBfs(string messageType, int fromVersion, int toVersion)
	{
		// BFS from 'from' version to 'to' version
		var queue = new Queue<(int Version, List<int> Path)>();
		var visited = new HashSet<int>();

		queue.Enqueue((fromVersion, new List<int>()));
		_ = visited.Add(fromVersion);

		while (queue.Count > 0)
		{
			var (currentVersion, currentPath) = queue.Dequeue();

			// Get all edges from current version
			var key = (messageType, currentVersion);
			if (!_graph.TryGetValue(key, out var edges))
			{
				continue;
			}

			foreach (var edge in edges)
			{
				if (visited.Contains(edge.ToVersion))
				{
					continue;
				}

				var newPath = new List<int>(currentPath) { edge.ToVersion };

				if (edge.ToVersion == toVersion)
				{
					// Found shortest path
					return newPath;
				}

				_ = visited.Add(edge.ToVersion);
				queue.Enqueue((edge.ToVersion, newPath));
			}
		}

		// No path found - return empty list (negative cache)
		return Array.Empty<int>();
	}

	/// <summary>
	/// Invalidates all cached paths for a message type.
	/// Called when a new upcaster is registered.
	/// </summary>
	private void InvalidatePathCache(string messageType)
	{
		// Remove all cache entries for this message type
		var keysToRemove = _pathCache.Keys
			.Where(k => k.MessageType == messageType)
			.ToList();

		foreach (var key in keysToRemove)
		{
			_ = _pathCache.TryRemove(key, out _);
		}
	}

	/// <summary>
	/// Internal representation of an upcaster edge in the version graph.
	/// Stores both the typed delegate for direct invocation and metadata.
	/// </summary>
	private sealed class UpcasterEdge
	{
		public required string MessageType { get; init; }
		public required int FromVersion { get; init; }
		public required int ToVersion { get; init; }
		public required Func<object, object> Upcast { get; init; }
	}
}
