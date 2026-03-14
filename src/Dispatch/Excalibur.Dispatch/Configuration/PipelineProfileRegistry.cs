// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Routing;

namespace Excalibur.Dispatch.Configuration;

/// <summary>
/// Default implementation of the pipeline profile registry.
/// </summary>
internal sealed class PipelineProfileRegistry : IPipelineProfileRegistry
{
	private readonly ConcurrentDictionary<string, IPipelineProfile> _profiles = new(StringComparer.Ordinal);
	private readonly IMiddlewareApplicabilityStrategy? _applicabilityStrategy;

	/// <summary>
	/// Per-message-type profile selection cache. Avoids re-iterating all profiles on every dispatch.
	/// Uses three-phase freeze pattern: ConcurrentDictionary during warmup → FrozenDictionary after freeze.
	/// </summary>
	private ConcurrentDictionary<Type, IPipelineProfile?>? _profileSelectionCache = new();
	private FrozenDictionary<Type, IPipelineProfile?>? _frozenProfileSelectionCache;
	private volatile bool _profileSelectionFrozen;

	/// <summary>
	/// Initializes a new instance of the <see cref="PipelineProfileRegistry"/> class.
	/// Creates a new pipeline profile registry.
	/// </summary>
	public PipelineProfileRegistry(IMiddlewareApplicabilityStrategy? applicabilityStrategy = null)
	{
		_applicabilityStrategy = applicabilityStrategy;

		// Register default profiles
		RegisterDefaultProfiles();
	}

	/// <summary>
	/// Gets a value indicating whether the profile selection cache has been frozen.
	/// </summary>
	public bool IsProfileSelectionCacheFrozen => _profileSelectionFrozen;

	/// <inheritdoc />
	public void RegisterProfile(IPipelineProfile profile)
	{
		ArgumentNullException.ThrowIfNull(profile);

		if (!_profiles.TryAdd(profile.Name, profile))
		{
			throw new InvalidOperationException(
				$"A profile with name '{profile.Name}' is already registered");
		}
	}

	/// <inheritdoc />
	public IPipelineProfile? GetProfile(string profileName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(profileName);

		return _profiles.TryGetValue(profileName, out var profile) ? profile : null;
	}

	/// <inheritdoc />
	public IEnumerable<IPipelineProfile> GetAllProfiles() => _profiles.Values.ToList();

	/// <inheritdoc />
	public IEnumerable<string> GetProfileNames() => _profiles.Keys.ToList();

	/// <inheritdoc />
	public bool RemoveProfile(string profileName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(profileName);
		return _profiles.TryRemove(profileName, out _);
	}

	/// <inheritdoc />
	[RequiresUnreferencedCode("Selects profiles using compatibility checks that rely on reflection.")]
	public IPipelineProfile? SelectProfile(IDispatchMessage message)
	{
		ArgumentNullException.ThrowIfNull(message);

		var messageType = message.GetType();

		// Fast path: check frozen cache first (O(1), zero synchronization)
		if (_profileSelectionFrozen && _frozenProfileSelectionCache!.TryGetValue(messageType, out var cachedProfile))
		{
			return cachedProfile;
		}

		// Warm path: check mutable cache
		if (_profileSelectionCache is { } cache && cache.TryGetValue(messageType, out cachedProfile))
		{
			return cachedProfile;
		}

		// Cold path: compute profile selection and cache result
		var selected = SelectProfileCore(message);

		// Cache the result (including null for message types with no matching profile)
		_profileSelectionCache?.TryAdd(messageType, selected);

		return selected;
	}

	/// <summary>
	/// Core profile selection logic. Called once per message type, then cached.
	/// </summary>
	[RequiresUnreferencedCode("Selects profiles using compatibility checks that rely on reflection.")]
	private IPipelineProfile? SelectProfileCore(IDispatchMessage message)
	{
		// Determine message kinds
		var messageKinds = _applicabilityStrategy?.DetermineMessageKinds(message) ?? MessageKinds.All;

		// Snapshot values to avoid repeated dictionary enumeration
		var profileValues = _profiles.Values;

		// Find the most specific compatible profile. Prioritize strict profiles for Actions, lightweight for Events.
		if ((messageKinds & MessageKinds.Action) != MessageKinds.None)
		{
			foreach (var p in profileValues)
			{
				if (p.IsStrict && p.IsCompatible(message))
				{
					return p;
				}
			}
		}

		if ((messageKinds & MessageKinds.Event) != MessageKinds.None)
		{
			foreach (var p in profileValues)
			{
				if (!p.IsStrict && p.SupportedMessageKinds == MessageKinds.Event && p.IsCompatible(message))
				{
					return p;
				}
			}
		}

		// Fall back to any compatible profile, preferring strict
		IPipelineProfile? bestFallback = null;
		foreach (var p in profileValues)
		{
			if (p.IsCompatible(message))
			{
				if (p.IsStrict)
				{
					return p; // Strict profiles have highest priority
				}

				bestFallback ??= p;
			}
		}

		return bestFallback;
	}

	/// <summary>
	/// Freezes the profile selection cache for maximum hot-path performance.
	/// After freezing, profile lookups use a <see cref="FrozenDictionary{TKey, TValue}"/>
	/// for O(1) zero-synchronization reads.
	/// </summary>
	public void FreezeProfileSelectionCache()
	{
		if (_profileSelectionFrozen)
		{
			return;
		}

		var cache = _profileSelectionCache;
		if (cache is null)
		{
			return;
		}

		_frozenProfileSelectionCache = cache.ToFrozenDictionary();
		_profileSelectionFrozen = true;
		_profileSelectionCache = null; // Allow GC of mutable dictionary
	}

	/// <inheritdoc />
	public void SetDefaultProfile(string profileName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(profileName);

		if (!_profiles.ContainsKey(profileName))
		{
			throw new InvalidOperationException($"Profile '{profileName}' is not registered");
		}
	}

	private void RegisterDefaultProfiles()
	{
		// Register the built-in profiles
		RegisterProfile(PipelineProfile.CreateStrictProfile());
		RegisterProfile(PipelineProfile.CreateInternalEventProfile());
		RegisterProfile(DefaultPipelineProfiles.CreateDirectProfile());

		// Register a default profile for documents
		RegisterProfile(new PipelineProfile(
			"Document",
			"Standard pipeline for document/data transfer messages",
			[
				typeof(RoutingMiddleware), // Documents need routing for delivery
			],
			isStrict: false,
			supportedMessageKinds: MessageKinds.Document));

		// Register a minimal profile
		RegisterProfile(new PipelineProfile(
			"Minimal",
			"Minimal pipeline with only essential middleware",
			[
				typeof(RoutingMiddleware), // Minimal profile needs routing for message delivery
			],
			isStrict: false,
			supportedMessageKinds: MessageKinds.All));
	}
}
