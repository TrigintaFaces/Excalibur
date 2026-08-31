// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

using Excalibur.Dispatch.Delivery;

namespace Excalibur.Dispatch.Configuration;

/// <summary>
/// A pipeline profile that was synthesized automatically.
/// </summary>
internal sealed class SynthesizedPipelineProfile : IPipelineProfile, IPipelineProfileMatcher
{
	private readonly ConcurrentDictionary<Type, bool> _compatibilityCache = new();
	/// <summary>
	/// Initializes a new instance of the <see cref="SynthesizedPipelineProfile"/> class.
	/// Creates a new synthesized pipeline profile.
	/// </summary>
	public SynthesizedPipelineProfile(
		string name,
		string description,
		Type[] middlewareTypes,
		bool isStrict,
		MessageKinds supportedMessageKinds,
		int includedCount,
		int omittedCount)
	{
		Name = name ?? throw new ArgumentNullException(nameof(name));
		Description = description ?? throw new ArgumentNullException(nameof(description));
		ArgumentNullException.ThrowIfNull(middlewareTypes);
		MiddlewareEntries = BuildRequiredEntries(middlewareTypes);
		IsStrict = isStrict;
		SupportedMessageKinds = supportedMessageKinds;
		// Store synthesis metadata
		Metadata = new Dictionary<string, object>
(StringComparer.Ordinal)
		{
			["Synthesized"] = true,
			["SynthesisTimestamp"] = DateTimeOffset.UtcNow,
			["IncludedMiddleware"] = includedCount,
			["OmittedMiddleware"] = omittedCount,
		};
	}

	/// <inheritdoc />
	public string Name { get; }

	/// <inheritdoc />
	public string Description { get; }

	/// <inheritdoc />
	public bool IsStrict { get; }

	/// <inheritdoc />
	public MessageKinds SupportedMessageKinds { get; }

	/// <inheritdoc />
	/// <remarks>
	/// A synthesized profile has already decided which middleware belongs in it, so every surviving entry is
	/// <see cref="MiddlewareCriticality.Required" />. Middleware the synthesizer chose to omit is absent from this list entirely rather than
	/// present-and-optional, which keeps "omitted by synthesis" distinguishable from "declared but unresolvable at build".
	/// </remarks>
	public IReadOnlyList<MiddlewareEntry> MiddlewareEntries { get; }

	private static IReadOnlyList<MiddlewareEntry> BuildRequiredEntries(Type[] middlewareTypes)
	{
		if (middlewareTypes.Length == 0)
		{
			return [];
		}

		var entries = new MiddlewareEntry[middlewareTypes.Length];
		for (var i = 0; i < middlewareTypes.Length; i++)
		{
			entries[i] = new MiddlewareEntry(middlewareTypes[i], MiddlewareCriticality.Required);
		}

		return Array.AsReadOnly(entries);
	}

	/// <summary>
	/// Gets metadata about the synthesis process.
	/// </summary>
	/// <value>The current <see cref="Metadata"/> value.</value>
	public IReadOnlyDictionary<string, object> Metadata { get; }

	/// <summary>
	/// Gets the middleware types for this profile.
	/// </summary>
	public IEnumerable<Type> GetMiddlewareTypes() => MiddlewareEntries.Select(static e => e.MiddlewareType);

	/// <inheritdoc />
	public bool IsCompatible(IDispatchMessage message)
	{
		ArgumentNullException.ThrowIfNull(message);

		// Check if the message kind is supported
		var messageType = message.GetType();
		if (_compatibilityCache.TryGetValue(messageType, out var cached))
		{
			return cached;
		}

		var isCompatible = IsCompatibleForType(messageType);
		_compatibilityCache.TryAdd(messageType, isCompatible);
		return isCompatible;
	}

	private bool IsCompatibleForType(Type messageType) =>
		(SupportedMessageKinds & Delivery.DefaultMiddlewareApplicabilityStrategy.DetermineMessageKinds(messageType))
			!= MessageKinds.None;
}
