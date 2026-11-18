// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections.Frozen;
using System.Runtime.CompilerServices;
using System.Diagnostics.CodeAnalysis;

namespace Excalibur.Dispatch.Versioning;

/// <summary>
/// Thread-safe implementation of <see cref="IUpcastingPipeline"/> that resolves the shortest chain of
/// upcasters between two message versions.
/// </summary>
/// <remarks>
/// <para>
/// <b>Registry reads do not acquire the registration lock.</b> Path-cache population may synchronize
/// internally. The registered upcasters live in an immutable registry that a reader
/// obtains with a single volatile read and then only inspects. Registration builds a wholly new registry
/// and publishes it with one volatile write, so a reader sees either the registry before that
/// registration or the one after it, never a half-built state.
/// </para>
/// <para>
/// <b>The premise a future edit must not break:</b> every public operation reads the registry field
/// exactly once and threads that snapshot down — no helper re-reads the field, and no consumer code runs
/// inside the registration lock — because two reads of the field in one operation can straddle a
/// registration, measuring a message against one graph and traversing it against another.
/// </para>
/// <para>
/// The second half of that sentence is load-bearing and less obvious than the first. Re-entrant
/// registration is safe today only because no consumer-supplied code executes while the registration
/// lock is held: the message-type derivation happens before the lock is taken, and the registry is
/// rebuilt from values already in hand. Move a consumer callback inside that lock and a nested
/// registration would publish, return successfully, and then have its edge silently discarded by the
/// outer publish.
/// </para>
/// <para>
/// <b>Why not a reader-writer lock.</b> This type previously guarded every read with a
/// <c>ReaderWriterLockSlim</c>, so the commonest case in a long-lived system — a message already at the
/// latest version, which needs no work at all — still paid lock acquisition on the dispatch path.
/// Registration is rare and reads are constant, which is the shape
/// <see cref="FrozenDictionary{TKey, TValue}"/> and copy-on-write publication exist for.
/// </para>
/// <para>
/// <b>Visible ordering.</b> A reader that obtained its registry before a concurrent registration
/// completes its whole traversal against the pre-registration graph. That is the same answer it would
/// have given had the registration happened immediately after the call returned, and it matches the
/// behaviour of the lock-based design it replaces, where a writer waited for in-flight readers to leave.
/// </para>
/// </remarks>
public sealed class UpcastingPipeline : IUpcastingPipeline, IDisposable
{
	/// <summary>
	/// Derived message-type names, associated with the upcaster source type through WEAK keys.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Two distinct properties are in play and only one of them is about size. A previous revision
	/// capped this at a fixed entry count and SKIPPED caching once full, which bounds nothing: it turns
	/// a one-time derivation into a permanent one for every consumer past the cap, and the derivation
	/// invokes a constructor reflectively. That is a cliff, not a bound, and the cap is not coming back.
	/// </para>
	/// <para>
	/// The property that does matter is RETENTION. A <see langword="static"/> dictionary holding strong
	/// references to <see cref="Type"/> instances roots those types, and rooting a type roots its
	/// assembly — so a collectible <c>AssemblyLoadContext</c> that ever registered an upcaster could
	/// never unload. That breaks plugin and some serverless hosts, which are deployment scenarios this
	/// framework supports. <see cref="ConditionalWeakTable{TKey, TValue}"/> is the first-party primitive
	/// for type-associated data: the key is weak, so an entry dies with its type and the load context
	/// stays collectible. Do not replace it with a dictionary for lookup speed — this sits on the
	/// registration path, not the dispatch path.
	/// </para>
	/// </remarks>
	private static readonly ConditionalWeakTable<Type, string> MessageTypeNames = new();

	/// <summary> Serializes registration only. Readers never contend for it. </summary>
	private readonly Lock _registrationLock = new();

	private Registry _registry = Registry.Empty;

	/// <inheritdoc />
	public IDispatchMessage Upcast(IDispatchMessage message)
	{
		ArgumentNullException.ThrowIfNull(message);

		// A message carrying no version needs no registry at all, so this path touches nothing shared.
		if (message is not IVersionedMessage versioned)
		{
			return message;
		}

		var registry = GetRegistry();

		if (!registry.ByMessageType.TryGetValue(versioned.MessageType, out var graph) ||
			versioned.Version >= graph.LatestVersion)
		{
			return message;
		}

		return Traverse(graph, message, versioned.MessageType, versioned.Version, graph.LatestVersion);
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

		var registry = GetRegistry();

		// Refuse targets above the registered maximum early. The resolver separately requires both
		// endpoints to be registered vertices before memoizing, including sparse or negative queries.
		if (!registry.ByMessageType.TryGetValue(messageType, out var graph) ||
			targetVersion > graph.LatestVersion)
		{
			throw new InvalidOperationException(
				$"No upcasting path exists for {messageType} from v{currentVersion} to v{targetVersion}. " +
				"Register the required upcasters to create a path.");
		}

		return Traverse(graph, message, messageType, currentVersion, targetVersion);
	}

	/// <inheritdoc />
	public void Register<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] TOld, TNew>(
		IMessageUpcaster<TOld, TNew> upcaster)
		where TOld : IDispatchMessage, IVersionedMessage
		where TNew : IDispatchMessage, IVersionedMessage
	{
		ArgumentNullException.ThrowIfNull(upcaster);

		var fromVersion = upcaster.FromVersion;
		var toVersion = upcaster.ToVersion;

		if (fromVersion >= toVersion)
		{
			throw new ArgumentException(
				$"Invalid upcaster: FromVersion ({fromVersion}) must be less than " +
				$"ToVersion ({toVersion}). Only forward upcasting is supported.",
				nameof(upcaster));
		}

		// Prefer the instance property over the type name so the key matches what Upcast will see.
		var messageType = GetValidatedMessageType<TOld>();

		var edge = new UpcasterEdge
		{
			MessageType = messageType,
			FromVersion = fromVersion,
			ToVersion = toVersion,

			// The generic call is captured here, so traversal needs no reflection and no DynamicInvoke.
			Upcast = msg => upcaster.Upcast((TOld)msg),
		};

		// The lock covers read, validate and publish as one step, so two concurrent registrations of the
		// same edge cannot both pass the duplicate check.
		lock (_registrationLock)
		{
			var current = GetRegistry();
			var existingEdges = current.ByMessageType.TryGetValue(messageType, out var graph)
				? graph.Edges
				: [];

			for (var index = 0; index < existingEdges.Count; index++)
			{
				var candidate = existingEdges[index];

				if (candidate.FromVersion == edge.FromVersion && candidate.ToVersion == edge.ToVersion)
				{
					throw new InvalidOperationException(
						$"An upcaster for {messageType} v{fromVersion} -> v{toVersion} " +
						"is already registered.");
				}
			}

			Volatile.Write(ref _registry, current.With(messageType, edge));
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

		var registry = GetRegistry();

		// Endpoint membership inside the resolver bounds memo keys to this graph's vertices.
		return registry.ByMessageType.TryGetValue(messageType, out var graph) &&
			toVersion <= graph.LatestVersion &&
			graph.Paths.Resolve(messageType, graph.Edges, fromVersion, toVersion) is not null;
	}

	/// <inheritdoc />
	public int GetLatestVersion(string messageType)
	{
		ArgumentNullException.ThrowIfNull(messageType);

		var registry = GetRegistry();

		return registry.ByMessageType.TryGetValue(messageType, out var graph) ? graph.LatestVersion : 0;
	}

	/// <summary> Stops new registry-dependent operations; admitted traversals may finish. </summary>
	/// <remarks>
	/// Repeated disposal is harmless. Consumer-owned upcasters are not disposed. Registry-independent
	/// identity and downcast checks retain their normal behavior. Cached CanUpcast queries now follow
	/// the same disposed-state check as uncached queries instead of exposing cache-dependent lifetime.
	/// </remarks>
	public void Dispose()
	{
		lock (_registrationLock)
		{
			Volatile.Write(ref _registry, Registry.Disposed);
		}
	}

	private Registry GetRegistry()
	{
		var registry = Volatile.Read(ref _registry);
		ObjectDisposedException.ThrowIf(registry.IsDisposed, this);
		return registry;
	}

	/// <summary>
	/// Applies the resolved chain of upcasters in order.
	/// </summary>
	/// <remarks>
	/// The resolved chain holds the upcaster delegates themselves, so applying it needs no further graph
	/// lookup and no per-hop search for the matching edge. The caller passes the graph it already read;
	/// this method never consults the registry field, which is what keeps one operation on one snapshot.
	/// </remarks>
	private static IDispatchMessage Traverse(
		TypeGraph graph,
		IDispatchMessage message,
		string messageType,
		int fromVersion,
		int toVersion)
	{
		// An absent chain and an empty one are different answers: null means no route exists, while empty
		// means the message is already where it was asked to be.
		var chain = graph.Paths.Resolve(messageType, graph.Edges, fromVersion, toVersion)
			?? throw new InvalidOperationException(
				$"No upcasting path exists for {messageType} from v{fromVersion} to v{toVersion}. " +
				"Register the required upcasters to create a path.");

		var current = message;

		for (var index = 0; index < chain.Count; index++)
		{
			current = (IDispatchMessage)chain[index].Upcast(current);
		}

		return current;
	}

	/// <summary>
	/// Resolves the message-type key for an upcaster's source type.
	/// </summary>
	/// <remarks>
	/// Prefers the value a real instance reports from <see cref="IVersionedMessage.MessageType"/>, so the
	/// registration key matches what the dispatch path will present. Falls back to stripping the version
	/// suffix from the type name when the type cannot be instantiated.
	/// </remarks>
	private static string GetValidatedMessageType<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
	T>()
		where T : IDispatchMessage, IVersionedMessage
	{
		var type = typeof(T);

		if (MessageTypeNames.TryGetValue(type, out var cached))
		{
			return cached;
		}

		var messageType = DeriveMessageType(type);

		if (string.IsNullOrWhiteSpace(messageType))
		{
			throw new InvalidOperationException(
				$"Cannot determine message type for {type.Name}. " +
				"Ensure the type has a parameterless constructor or follows the naming convention " +
				"(e.g., 'UserCreatedEventV1' where 'V1' is the version suffix).");
		}

		// The factory may run more than once under contention; every caller derives the same value from
		// the same type, so duplicated work is wasted rather than wrong.
		return MessageTypeNames.GetValue(type, _ => messageType);
	}

	private static string DeriveMessageType(
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] Type type)
	{
		var constructor = type.GetConstructor(Type.EmptyTypes);

		if (constructor is null)
		{
			return StripVersionSuffix(type.Name);
		}

		try
		{
			var instance = (IVersionedMessage)constructor.Invoke(null);
			var declared = instance.MessageType;

			if (string.IsNullOrWhiteSpace(declared))
			{
				throw new InvalidOperationException(
					$"Type {type.Name} returned null or empty MessageType property. " +
					"IVersionedMessage.MessageType must return a non-empty string.");
			}

			return declared;
		}
		catch (Exception ex) when (ex is not InvalidOperationException)
		{
			// The constructor threw for its own reasons; the naming convention is the remaining signal.
			return StripVersionSuffix(type.Name);
		}
	}

	/// <summary>
	/// Strips a trailing version suffix from a type name, so "UserCreatedEventV1" becomes
	/// "UserCreatedEvent".
	/// </summary>
	/// <remarks>
	/// Returns the name unchanged when there is no valid suffix, or when stripping it would leave nothing.
	/// </remarks>
	private static string StripVersionSuffix(string typeName)
	{
		if (string.IsNullOrEmpty(typeName))
		{
			return typeName;
		}

		var length = typeName.Length;
		var index = length - 1;

		while (index >= 0 && char.IsDigit(typeName[index]))
		{
			index--;
		}

		if (index >= 0 && (typeName[index] == 'V' || typeName[index] == 'v') && index < length - 1)
		{
			// "V1" alone would strip to nothing, so it is kept as written.
			return index == 0 ? typeName : typeName[..index];
		}

		return typeName;
	}

	/// <summary> One upcaster edge in the version graph, holding the typed call as a delegate. </summary>
	private sealed class UpcasterEdge
	{
		public required string MessageType { get; init; }

		public required int FromVersion { get; init; }

		public required int ToVersion { get; init; }

		public required Func<object, object> Upcast { get; init; }
	}

	/// <summary>
	/// Everything registered for one message type: its edges, its highest registered version, and the
	/// resolver that memoizes chains between versions.
	/// </summary>
	private sealed class TypeGraph
	{
		public required IReadOnlyList<UpcasterEdge> Edges { get; init; }

		public required int LatestVersion { get; init; }

		public required UpgradePathResolver<UpcasterEdge> Paths { get; init; }
	}

	/// <summary>
	/// An immutable set of per-type graphs, replaced wholesale on registration.
	/// </summary>
	/// <remarks>
	/// The resolver's memo cache lives inside the <see cref="TypeGraph"/>, so publishing a new registry
	/// discards the stale chains for the type that changed as a single atomic act. There is deliberately
	/// no invalidation method: the previous design scanned a shared cache for matching keys on every
	/// registration, and a cache that is replaced wholesale cannot be swept incorrectly.
	/// </remarks>
	private sealed class Registry
	{
		public static readonly Registry Empty = new()
		{
			ByMessageType = FrozenDictionary<string, TypeGraph>.Empty,
		};

		public static readonly Registry Disposed = new()
		{
			ByMessageType = FrozenDictionary<string, TypeGraph>.Empty,
			IsDisposed = true,
		};

		public bool IsDisposed { get; init; }

		public required FrozenDictionary<string, TypeGraph> ByMessageType { get; init; }

		/// <summary>
		/// Returns a new registry carrying this one's graphs plus <paramref name="edge"/>.
		/// </summary>
		/// <param name="messageType"> The message type the edge belongs to. </param>
		/// <param name="edge"> The newly registered edge. </param>
		/// <returns> The registry to publish. </returns>
		public Registry With(string messageType, UpcasterEdge edge)
		{
			var builder = new Dictionary<string, TypeGraph>(ByMessageType.Count + 1, StringComparer.Ordinal);

			foreach (var entry in ByMessageType)
			{
				builder[entry.Key] = entry.Value;
			}

			var existing = ByMessageType.TryGetValue(messageType, out var graph) ? graph : null;

			builder[messageType] = new TypeGraph
			{
				Edges = existing is null
					? [edge]
					: new List<UpcasterEdge>(existing.Edges) { edge },
				LatestVersion = Math.Max(existing?.LatestVersion ?? 0, edge.ToVersion),

				// A fresh resolver for the changed type only. Other types keep their graph object, and
				// therefore keep the chains they have already resolved.
				Paths = new UpgradePathResolver<UpcasterEdge>(
					static candidate => candidate.FromVersion,
					static candidate => candidate.ToVersion),
			};

			return new Registry { ByMessageType = builder.ToFrozenDictionary(StringComparer.Ordinal) };
		}
	}
}
