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
	private static readonly DispatchMiddlewareStage[] StageOrder =
	[
		DispatchMiddlewareStage.Start,
		DispatchMiddlewareStage.RateLimiting,
		DispatchMiddlewareStage.PreProcessing,
		DispatchMiddlewareStage.Instrumentation,
		DispatchMiddlewareStage.Authentication,
		DispatchMiddlewareStage.Validation,
		DispatchMiddlewareStage.Authorization,
		DispatchMiddlewareStage.Serialization,
		DispatchMiddlewareStage.Cache,
		DispatchMiddlewareStage.Routing,
		DispatchMiddlewareStage.Processing,
		DispatchMiddlewareStage.PostProcessing,
		DispatchMiddlewareStage.ErrorHandling,
		DispatchMiddlewareStage.End,
	];

	private static readonly MiddlewareRegistrationComparer RegistrationComparer = new();

	private readonly Dictionary<DispatchMiddlewareStage, List<MiddlewareRegistration>> _middlewareByStage = [];
	private int _registeredMiddlewareCount;

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

		var pipeline = new List<Type>(_registeredMiddlewareCount);

		// R7.5: Synthesize default pipelines based on configuration R7.6: Pipeline synthesis follows deterministic ordering R7.10: Default
		// synthesis follows canonical order

		// Apply middleware in stage order (R7.11)
		for (var stageIndex = 0; stageIndex < StageOrder.Length; stageIndex++)
		{
			var stage = StageOrder[stageIndex];
			if (!_middlewareByStage.TryGetValue(stage, out var middlewareList))
			{
				continue;
			}

			if (middlewareList.Count == 0)
			{
				continue;
			}

			var applicableMiddleware = new List<MiddlewareRegistration>(middlewareList.Count);
			for (var i = 0; i < middlewareList.Count; i++)
			{
				var middleware = middlewareList[i];
				if (IsApplicableForKinds(middleware, messageKinds) &&
					IsEnabledByConfiguration(middleware, options))
				{
					applicableMiddleware.Add(middleware);
				}
			}

			if (applicableMiddleware.Count == 0)
			{
				continue;
			}

			if (applicableMiddleware.Count > 1)
			{
				// R7.6: Deterministic ordering by priority, then registration order.
				applicableMiddleware.Sort(RegistrationComparer);
			}

			for (var i = 0; i < applicableMiddleware.Count; i++)
			{
				pipeline.Add(applicableMiddleware[i].MiddlewareType);
			}
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
		_registeredMiddlewareCount++;
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

	private sealed class MiddlewareRegistrationComparer : IComparer<MiddlewareRegistration>
	{
		public int Compare(MiddlewareRegistration? x, MiddlewareRegistration? y)
		{
			if (ReferenceEquals(x, y))
			{
				return 0;
			}

			if (x is null)
			{
				return -1;
			}

			if (y is null)
			{
				return 1;
			}

			var priorityComparison = x.Priority.CompareTo(y.Priority);
			return priorityComparison != 0
				? priorityComparison
				: x.RegistrationOrder.CompareTo(y.RegistrationOrder);
		}
	}
}
