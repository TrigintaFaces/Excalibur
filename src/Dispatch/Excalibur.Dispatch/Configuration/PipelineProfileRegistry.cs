// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Routing;

namespace Excalibur.Dispatch.Configuration;

/// <summary>
/// Default implementation of the pipeline profile registry.
/// </summary>
public sealed class PipelineProfileRegistry : IPipelineProfileRegistry
{
	private readonly ConcurrentDictionary<string, IPipelineProfile> _profiles = new(StringComparer.Ordinal);
	private readonly IMiddlewareApplicabilityStrategy? _applicabilityStrategy;

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
	[RequiresUnreferencedCode("Selects profiles using compatibility checks that rely on reflection.")]
	public IPipelineProfile? SelectProfile(IDispatchMessage message)
	{
		ArgumentNullException.ThrowIfNull(message);

		// Determine message kinds
		var messageKinds = _applicabilityStrategy?.DetermineMessageKinds(message) ?? MessageKinds.All;

		// Find the most specific compatible profile Prioritize strict profiles for Actions, lightweight for Events
		if ((messageKinds & MessageKinds.Action) != MessageKinds.None)
		{
			// Try to find strict profile for actions
			var strictProfile = _profiles.Values
				.FirstOrDefault(p => p.IsStrict && p.IsCompatible(message));

			if (strictProfile != null)
			{
				return strictProfile;
			}
		}

		if ((messageKinds & MessageKinds.Event) != MessageKinds.None)
		{
			// Try to find event-specific profile
			var eventProfile = _profiles.Values
				.FirstOrDefault(p => p is { IsStrict: false, SupportedMessageKinds: MessageKinds.Event } &&
									 p.IsCompatible(message));

			if (eventProfile != null)
			{
				return eventProfile;
			}
		}

		// Fall back to any compatible profile
		return _profiles.Values
			.Where(p => p.IsCompatible(message))
			.OrderByDescending(p => p.IsStrict) // Prefer strict profiles
			.FirstOrDefault();
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
