// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Reflection;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Diagnostics;
using Excalibur.Dispatch.Options.Delivery;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Delivery.Pipeline;

/// <summary>
/// Evaluates middleware applicability based on message kinds and feature requirements. Implements requirement R2.4-R2.6.
/// </summary>
/// <remarks>
/// <para>
/// Initializes a new instance of the <see cref="MiddlewareApplicabilityEvaluator" /> class.
/// </para>
/// <para>
/// PERF-13/PERF-14: Uses three-phase lazy freeze pattern for optimal lookup performance:
/// <list type="number">
/// <item>Warmup phase: ConcurrentDictionary for thread-safe population during startup</item>
/// <item>Freeze transition: ToFrozenDictionary() when cache stabilizes</item>
/// <item>Frozen phase: FrozenDictionary for zero-sync O(1) lookups</item>
/// </list>
/// Call <see cref="FreezeCache"/> after middleware registration is complete (e.g., via UseOptimizedDispatch).
/// </para>
/// </remarks>
/// <param name="options"> The configuration options. </param>
/// <param name="logger"> The logger. </param>
public sealed partial class MiddlewareApplicabilityEvaluator(
	IOptions<MiddlewareApplicabilityOptions> options,
	ILogger<MiddlewareApplicabilityEvaluator> logger) : IDispatchMiddlewareApplicabilityEvaluator
{
	/// <summary>
	/// Warmup cache for thread-safe population during startup (PERF-13/PERF-14).
	/// Null after freeze is called.
	/// </summary>
	private static ConcurrentDictionary<Type, MiddlewareMetadata>? _warmupCache = new();

	/// <summary>
	/// Frozen cache for optimal read performance after warmup (PERF-13/PERF-14).
	/// Null until freeze is called.
	/// </summary>
	private static FrozenDictionary<Type, MiddlewareMetadata>? _frozenCache;

	/// <summary>
	/// Flag indicating if the cache has been frozen.
	/// </summary>
	private static volatile bool _isFrozen;

	private readonly ILogger<MiddlewareApplicabilityEvaluator> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
	private readonly MiddlewareApplicabilityOptions _options = options?.Value ?? throw new ArgumentNullException(nameof(options));

	/// <inheritdoc />
	public bool IsApplicable(Type middlewareType, MessageKinds messageKind)
	{
		ArgumentNullException.ThrowIfNull(middlewareType);

		var metadata = GetMiddlewareMetadata(middlewareType);

		// R2.5: Check exclusions first - Exclude overrides include
		if ((metadata.ExcludedKinds & messageKind) != MessageKinds.None)
		{
			LogMiddlewareExcluded(middlewareType.Name, messageKind);
			return false;
		}

		// R2.4: Check applicability
		var isApplicable = (metadata.ApplicableKinds & messageKind) != MessageKinds.None;

		if (!isApplicable)
		{
			LogMiddlewareNotApplicable(middlewareType.Name, messageKind);
		}

		return isApplicable;
	}

	/// <inheritdoc />
	public bool IsApplicable(Type middlewareType, MessageKinds messageKind, IReadOnlySet<DispatchFeatures> enabledFeatures)
	{
		ArgumentNullException.ThrowIfNull(middlewareType);
		ArgumentNullException.ThrowIfNull(enabledFeatures);

		// First check basic message kind applicability
		if (!IsApplicable(middlewareType, messageKind))
		{
			return false;
		}

		// R2.6: Check feature requirements
		var metadata = GetMiddlewareMetadata(middlewareType);

		// Check if all required features are enabled
		foreach (var requiredFeature in metadata.RequiredFeatures)
		{
			if (!enabledFeatures.Contains(requiredFeature))
			{
				LogMiddlewareRequiresFeature(middlewareType.Name, requiredFeature);
				return false;
			}
		}

		return true;
	}

	/// <inheritdoc />
	public bool IsApplicable(IDispatchMiddleware middleware, MessageKinds messageKind)
	{
		ArgumentNullException.ThrowIfNull(middleware);

		var middlewareType = middleware.GetType();
		var metadata = GetMiddlewareMetadata(middlewareType);

		// R2.5: Check exclusions first - Exclude overrides include
		if ((metadata.ExcludedKinds & messageKind) != MessageKinds.None)
		{
			return false;
		}

		// Prefer attribute-based configuration over interface property
		if (metadata.HasAppliesToAttribute)
		{
			return (metadata.ApplicableKinds & messageKind) != MessageKinds.None;
		}

		// Fall back to interface property
		return (middleware.ApplicableMessageKinds & messageKind) != MessageKinds.None;
	}

	/// <inheritdoc />
	public IEnumerable<Type> FilterApplicableMiddleware(
		IEnumerable<Type> middlewareTypes,
		MessageKinds messageKind,
		IReadOnlySet<DispatchFeatures>? enabledFeatures = null)
	{
		ArgumentNullException.ThrowIfNull(middlewareTypes);

		// Pre-size list based on source count to avoid resizing allocations
		var initialCapacity = middlewareTypes.TryGetNonEnumeratedCount(out var count) ? count : 8;
		var applicableMiddleware = new List<Type>(initialCapacity);

		foreach (var middlewareType in middlewareTypes)
		{
			try
			{
				var isApplicable = enabledFeatures != null
					? IsApplicable(middlewareType, messageKind, enabledFeatures)
					: IsApplicable(middlewareType, messageKind);

				if (isApplicable)
				{
					applicableMiddleware.Add(middlewareType);
				}
			}
			catch (Exception ex)
			{
				LogEvaluationError(middlewareType.Name, ex);

				// Based on options, either include or exclude on error
				if (_options.IncludeOnError)
				{
					applicableMiddleware.Add(middlewareType);
				}
			}
		}

		return applicableMiddleware;
	}

	/// <summary>
	/// Gets a value indicating whether the cache has been frozen.
	/// </summary>
	public static bool IsCacheFrozen => _isFrozen;

	/// <summary>
	/// Gets middleware metadata from cache or creates it using reflection.
	/// </summary>
	/// <param name="middlewareType"> The middleware type. </param>
	/// <returns> The middleware metadata. </returns>
	private static MiddlewareMetadata GetMiddlewareMetadata(Type middlewareType)
	{
		// PERF-13/PERF-14: Three-phase lazy freeze pattern
		if (_isFrozen)
		{
			// Phase 3 (frozen): Fast path with zero synchronization overhead
			if (_frozenCache.TryGetValue(middlewareType, out var frozenMetadata))
			{
				return frozenMetadata;
			}

			// Cache miss after freeze - build but don't cache (rare case)
			return BuildMetadata(middlewareType);
		}

		// Phase 1 (warmup): Thread-safe population using ConcurrentDictionary
		return _warmupCache.GetOrAdd(middlewareType, BuildMetadata);
	}

	/// <summary>
	/// Builds metadata for the specified middleware type using reflection.
	/// </summary>
	private static MiddlewareMetadata BuildMetadata(Type type)
	{
		var metadata = new MiddlewareMetadata();

		// Check for AppliesTo attribute
		var appliesToAttr = type.GetCustomAttribute<AppliesToAttribute>(inherit: true);
		if (appliesToAttr != null)
		{
			metadata.ApplicableKinds = appliesToAttr.MessageKinds;
			metadata.HasAppliesToAttribute = true;
		}
		else
		{
			// Default to All if no attribute found
			metadata.ApplicableKinds = MessageKinds.All;
		}

		// Check for ExcludeKinds attribute
		var excludeKindsAttr = type.GetCustomAttribute<ExcludeKindsAttribute>(inherit: true);
		if (excludeKindsAttr != null)
		{
			metadata.ExcludedKinds = excludeKindsAttr.ExcludedKinds;
		}

		// Check for RequiresFeatures attribute
		var requiresFeaturesAttr = type.GetCustomAttribute<RequiresFeaturesAttribute>(inherit: true);
		if (requiresFeaturesAttr != null)
		{
			metadata.RequiredFeatures = [.. requiresFeaturesAttr.Features];
		}

		return metadata;
	}

	/// <summary>
	/// Freezes the metadata cache for optimal read performance (PERF-13/PERF-14).
	/// </summary>
	/// <remarks>
	/// <para>
	/// Call this method after all middleware types have been registered (e.g., after DI container build).
	/// Once frozen, the cache uses <see cref="FrozenDictionary{TKey, TValue}"/> for O(1) lookups
	/// with zero synchronization overhead.
	/// </para>
	/// <para>
	/// This method is idempotent - calling it multiple times has no effect after the first call.
	/// </para>
	/// </remarks>
	public static void FreezeCache()
	{
		if (_isFrozen)
		{
			return;
		}

		var warmup = _warmupCache;
		if (warmup is null)
		{
			return;
		}

		// Phase 2 (freeze transition): Convert to FrozenDictionary
		_frozenCache = warmup.ToFrozenDictionary();
		_isFrozen = true;
		_warmupCache = null; // Allow GC to collect warmup dictionary
	}

	/// <summary>
	/// Clears the internal metadata cache. Primarily intended for testing scenarios.
	/// </summary>
	/// <remarks>
	/// This method resets the cache to its initial state (unfrozen, empty warmup dictionary).
	/// </remarks>
	internal static void ClearCache()
	{
		_isFrozen = false;
		_frozenCache = null;
		_warmupCache = new();
	}

	// Source-generated logging methods
	[LoggerMessage(DeliveryEventId.MiddlewareExcluded, LogLevel.Trace,
		"Middleware {MiddlewareType} excluded for message kind {MessageKind}")]
	private partial void LogMiddlewareExcluded(string middlewareType, MessageKinds messageKind);

	[LoggerMessage(DeliveryEventId.MiddlewareNotApplicable, LogLevel.Trace,
		"Middleware {MiddlewareType} not applicable for message kind {MessageKind}")]
	private partial void LogMiddlewareNotApplicable(string middlewareType, MessageKinds messageKind);

	[LoggerMessage(DeliveryEventId.MiddlewareRequiresFeature, LogLevel.Trace,
		"Middleware {MiddlewareType} requires feature {RequiredFeature} which is not enabled")]
	private partial void LogMiddlewareRequiresFeature(string middlewareType, DispatchFeatures requiredFeature);

	[LoggerMessage(DeliveryEventId.ApplicabilityEvaluationError, LogLevel.Warning,
		"Error evaluating applicability for middleware {MiddlewareType}")]
	private partial void LogEvaluationError(string middlewareType, Exception ex);

	/// <summary>
	/// Cached metadata for middleware types.
	/// </summary>
	private sealed class MiddlewareMetadata
	{
		public MessageKinds ApplicableKinds { get; set; } = MessageKinds.All;

		public MessageKinds ExcludedKinds { get; set; } = MessageKinds.None;

		public DispatchFeatures[] RequiredFeatures { get; set; } = [];

		public bool HasAppliesToAttribute { get; set; }
	}
}
