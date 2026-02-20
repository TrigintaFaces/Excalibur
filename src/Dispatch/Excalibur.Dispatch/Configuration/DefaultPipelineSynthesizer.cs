// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Options.Configuration;

namespace Excalibur.Dispatch.Configuration;

/// <summary>
/// Synthesizes default pipelines based on configuration and registered middleware. Implements requirements R7.5 through R7.12 for
/// deterministic middleware ordering.
/// </summary>
public sealed class DefaultPipelineSynthesizer : IDefaultPipelineSynthesizer
{
	private readonly Dictionary<DispatchMiddlewareStage, List<MiddlewareRegistration>> _middlewareByStage = [];

	/// <summary>
	/// Initializes a new instance of the <see cref="DefaultPipelineSynthesizer" /> class.
	/// </summary>
	public DefaultPipelineSynthesizer(IPipelineProfileRegistry profileRegistry)
	{
		ArgumentNullException.ThrowIfNull(profileRegistry);
		InitializeStages();
	}

	/// <summary>
	/// Synthesizes a default pipeline for the given message kinds and options.
	/// </summary>
	/// <param name="messageKinds"> The message kinds to create the pipeline for. </param>
	/// <param name="options"> The dispatch options. </param>
	/// <returns> An ordered list of middleware types. </returns>
	public IReadOnlyList<Type> SynthesizePipeline(MessageKinds messageKinds, DispatchOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var pipeline = new List<Type>();

		// R7.5: Synthesize default pipelines based on configuration R7.6: Pipeline synthesis follows deterministic ordering R7.10: Default
		// synthesis follows canonical order

		// Apply middleware in stage order (R7.11)
		foreach (var stage in GetStagesInOrder())
		{
			if (!_middlewareByStage.TryGetValue(stage, out var middlewareList))
			{
				continue;
			}

			// Filter middleware by message kinds and configuration
			var applicableMiddleware = middlewareList
				.Where(m => IsApplicableForKinds(m, messageKinds) &&
							IsEnabledByConfiguration(m, options))
				.OrderBy(m => m.Priority) // R7.6: Deterministic ordering by priority
				.ThenBy(m => m.RegistrationOrder) // Then by registration order
				.Select(m => m.MiddlewareType)
				.ToList();

			pipeline.AddRange(applicableMiddleware);
		}

		// R7.12: Apply profile overrides if specified
		// Note: Profile override functionality can be added when DefaultProfileName is added to DispatchOptions
		return pipeline;
	}

	/// <summary>
	/// Registers middleware for automatic inclusion in synthesized pipelines.
	/// </summary>
	public void RegisterMiddleware(
		Type middlewareType,
		DispatchMiddlewareStage stage,
		int priority = 0,
		MessageKinds? applicableKinds = null)
	{
		ArgumentNullException.ThrowIfNull(middlewareType);

		if (!_middlewareByStage.TryGetValue(stage, out var middlewareList))
		{
			middlewareList = [];
			_middlewareByStage[stage] = middlewareList;
		}

		var registration = new MiddlewareRegistration
		{
			MiddlewareType = middlewareType,
			Stage = stage,
			Priority = priority,
			ApplicableKinds = applicableKinds ?? MessageKinds.All,
			RegistrationOrder = middlewareList.Count,
		};

		middlewareList.Add(registration);
	}

	private static IEnumerable<DispatchMiddlewareStage> GetStagesInOrder()
	{
		// R7.10: Canonical order for stages (sorted by enum value)
		yield return DispatchMiddlewareStage.Start;
		yield return DispatchMiddlewareStage.RateLimiting;
		yield return DispatchMiddlewareStage.PreProcessing;
		yield return DispatchMiddlewareStage.Instrumentation;
		yield return DispatchMiddlewareStage.Authentication;
		yield return DispatchMiddlewareStage.Validation;
		yield return DispatchMiddlewareStage.Serialization;
		yield return DispatchMiddlewareStage.Authorization;
		yield return DispatchMiddlewareStage.Cache;
		yield return DispatchMiddlewareStage.Routing;
		yield return DispatchMiddlewareStage.Processing;
		yield return DispatchMiddlewareStage.PostProcessing;
		yield return DispatchMiddlewareStage.Error;
		yield return DispatchMiddlewareStage.End;
	}

	private static bool IsApplicableForKinds(MiddlewareRegistration registration, MessageKinds messageKinds)
	{
		// R7.7: Filter by message kinds
		if (registration.ApplicableKinds == MessageKinds.All)
		{
			return true;
		}

		return (registration.ApplicableKinds & messageKinds) != MessageKinds.None;
	}

	private static bool IsEnabledByConfiguration(MiddlewareRegistration registration, DispatchOptions options)
	{
		// R7.8: Configuration-based inclusion Check if middleware should be included based on configuration

		// Example: Instrumentation middleware only if metrics are enabled
		if (registration.Stage == DispatchMiddlewareStage.Instrumentation)
		{
			return options.Features.EnableMetrics;
		}

		// Cache middleware only if cache middleware is enabled
		if (registration.Stage == DispatchMiddlewareStage.Cache)
		{
			return options.Features.EnableCacheMiddleware;
		}

		// Example: RateLimiting based on max concurrency
		if (registration.Stage == DispatchMiddlewareStage.RateLimiting)
		{
			return options.MaxConcurrency > 0;
		}

		// Default: include
		return true;
	}

	private void InitializeStages()
	{
		// Initialize all stages
		foreach (var stage in Enum.GetValues<DispatchMiddlewareStage>())
		{
			_middlewareByStage[stage] = [];
		}
	}

	/// <summary>
	/// Internal registration for tracking middleware.
	/// </summary>
	private sealed class MiddlewareRegistration
	{
		public required Type MiddlewareType { get; init; }

		public required DispatchMiddlewareStage Stage { get; init; }

		public required int Priority { get; init; }

		public required MessageKinds ApplicableKinds { get; init; }

		public required int RegistrationOrder { get; init; }
	}
}
