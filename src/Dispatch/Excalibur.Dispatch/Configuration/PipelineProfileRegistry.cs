// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;

using Excalibur.Dispatch.Routing;

namespace Excalibur.Dispatch.Configuration;

/// <summary>
/// Default implementation of the pipeline profile registry.
/// </summary>
internal sealed class PipelineProfileRegistry : IPipelineProfileRegistry
{
	private readonly ConcurrentDictionary<string, IPipelineProfile> _profiles = new(StringComparer.Ordinal);
	private volatile string? _defaultProfileName;
	private readonly IMiddlewareApplicabilityStrategy? _applicabilityStrategy;

	/// <summary>
	/// Per-message-type profile selection cache. Avoids re-iterating all profiles on every dispatch.
	/// </summary>
	/// <remarks>
	/// Deliberately never frozen. A prior freeze-to-<see cref="System.Collections.Frozen.FrozenDictionary{TKey,TValue}"/>
	/// design measured slower than this plain dictionary at every message-type count tested,
	/// and it disabled the fall-through for a message type first seen after the freeze, forcing that type to
	/// re-run the full profile scan on every subsequent dispatch forever. <see cref="ConcurrentDictionary{TKey,TValue}"/>
	/// gives O(1) lookups on the fast path with no such cliff.
	/// </remarks>
	private readonly ConcurrentDictionary<Type, IPipelineProfile?> _profileSelectionCache = new();

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
	public IPipelineProfile? SelectProfile(IDispatchMessage message)
	{
		ArgumentNullException.ThrowIfNull(message);

		var messageType = message.GetType();

		if (_profileSelectionCache.TryGetValue(messageType, out var cachedProfile))
		{
			return cachedProfile;
		}

		// Cold path: compute profile selection and cache result
		var selected = SelectProfileCore(message);

		// Cache the result (including null for message types with no matching profile)
		_ = _profileSelectionCache.TryAdd(messageType, selected);

		return selected;
	}

	/// <summary>
	/// Core profile selection logic. Called once per message type, then cached.
	/// </summary>
	private IPipelineProfile? SelectProfileCore(IDispatchMessage message)
	{
		// Determine message kinds
		// One classifier answers this question. Without a configured strategy the default one is used
		// directly rather than assuming a kind, so profile selection cannot disagree with middleware
		// applicability about what a message is.
		var messageKinds = _applicabilityStrategy?.DetermineMessageKinds(message)
			?? Delivery.DefaultMiddlewareApplicabilityStrategy.DetermineMessageKinds(message.GetType());

		// Snapshot values to avoid repeated dictionary enumeration
		var profileValues = _profiles.Values;

		// Find the most specific compatible profile. Prioritize strict profiles for Actions, lightweight for Events.
		if ((messageKinds & MessageKinds.Action) != MessageKinds.None)
		{
			foreach (var p in profileValues)
			{
				if (p.IsStrict && IsProfileCompatible(p, message))
				{
					return p;
				}
			}
		}

		if ((messageKinds & MessageKinds.Event) != MessageKinds.None)
		{
			foreach (var p in profileValues)
			{
				if (!p.IsStrict && p.SupportedMessageKinds == MessageKinds.Event && IsProfileCompatible(p, message))
				{
					return p;
				}
			}
		}

		// Fall back to any compatible profile, preferring strict
		IPipelineProfile? bestFallback = null;
		foreach (var p in profileValues)
		{
			if (IsProfileCompatible(p, message))
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
	/// Checks whether a profile is compatible with a message by delegating to <see cref="IPipelineProfileMatcher"/>.
	/// </summary>
	private static bool IsProfileCompatible(IPipelineProfile profile, IDispatchMessage message) =>
		profile is IPipelineProfileMatcher matcher && matcher.IsCompatible(message);

	/// <inheritdoc />
	public void SetDefaultProfile(string profileName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(profileName);

		if (!_profiles.ContainsKey(profileName))
		{
			throw new InvalidOperationException($"Profile '{profileName}' is not registered");
		}

		_defaultProfileName = profileName;
	}

	/// <inheritdoc />
	public string? GetDefaultProfileName() =>
		_defaultProfileName ?? (_profiles.ContainsKey(DefaultPipelineProfiles.Default)
			? DefaultPipelineProfiles.Default
			: null);

	private void RegisterDefaultProfiles()
	{
		// Register the built-in profiles with their REAL middleware lists.
		// Previously these registered empty PipelineProfile.Create*Profile() shells
		// (Array.Empty<Type>(), "filled by synthesizer") while the only
		// registrar carrying real middleware (DefaultPipelineProfiles) had zero call
		// sites and was dead code. The synthesizer never ran (it short-circuits when
		// the registry is non-empty), so "default" was never registered and the empty
		// Strict/InternalEvent shells resolved to zero-middleware pipelines.
		// Reviving DefaultPipelineProfiles.Create*Profile() wires the canonical
		// middleware (incl. OutboxStagingMiddleware) into the registered profiles.
		// Profile keys are lowercase/hyphenated ("default"/"strict"/"internal-event").
		RegisterProfile(DefaultPipelineProfiles.CreateDefaultProfile());
		RegisterProfile(DefaultPipelineProfiles.CreateStrictProfile());
		RegisterProfile(DefaultPipelineProfiles.CreateInternalEventProfile());
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
