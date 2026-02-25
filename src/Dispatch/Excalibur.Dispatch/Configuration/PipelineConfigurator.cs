// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Configuration;

/// <summary>
/// Provides configuration utilities for pipeline optimizations. Implements R7.12 high-performance message processing patterns.
/// </summary>
public static class PipelineConfigurator
{
	/// <summary>
	/// Creates a custom pipeline profile with minimal middleware configuration.
	/// </summary>
	/// <param name="name"> The name of the custom profile. </param>
	/// <param name="supportedMessageKinds"> The message kinds this profile supports. </param>
	/// <param name="includeBasicTelemetry"> Whether to include minimal telemetry (adds ~2% overhead). </param>
	/// <returns> A configured pipeline profile. </returns>
	/// <remarks>
	/// Correlation and context management is handled directly in the Dispatcher,
	/// allowing profiles to have zero middleware overhead while still maintaining message tracing.
	/// </remarks>
	public static PipelineProfile CreateCustomHotPathProfile(
		string name,
		MessageKinds supportedMessageKinds,
		bool includeBasicTelemetry = false)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(name);

		var profile = new PipelineProfile(name, supportedMessageKinds)
		{
			Description = $"Custom hot-path pipeline for {supportedMessageKinds} with zero middleware overhead",
		};

		// No middleware needed - correlation is now handled at the Dispatcher level (Sprint 70)
		// Optional telemetry can be added via external observability hooks if needed
		_ = includeBasicTelemetry; // Reserved for future telemetry middleware

		return profile;
	}

	/// <summary>
	/// Creates a direct pipeline profile for scenarios where every nanosecond counts.
	/// Warning: This profile provides no middleware overhead but also no observability.
	/// </summary>
	/// <param name="name"> The name of the direct profile. </param>
	/// <param name="supportedMessageKinds"> The message kinds this profile supports. </param>
	/// <returns> A direct pipeline profile with zero middleware. </returns>
	public static PipelineProfile CreateDirectProfile(string name, MessageKinds supportedMessageKinds)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(name);

		var profile = new PipelineProfile(name, supportedMessageKinds)
		{
			Description = $"Direct pipeline for {supportedMessageKinds} with zero middleware overhead",
		};

		// No middleware at all - maximum performance, zero observability Use only when performance is absolutely critical and observability
		// is not needed
		return profile;
	}

	/// <summary>
	/// Creates a benchmark-optimized pipeline profile for performance testing scenarios.
	/// </summary>
	/// <param name="supportedMessageKinds"> The message kinds this profile supports. </param>
	/// <returns> A benchmark-optimized pipeline profile. </returns>
	/// <remarks>
	/// Correlation is handled at the Dispatcher level, allowing benchmark
	/// profiles to have zero middleware overhead while still maintaining traceability.
	/// </remarks>
	public static PipelineProfile CreateBenchmarkProfile(MessageKinds supportedMessageKinds)
	{
		var profile = new PipelineProfile("benchmark", supportedMessageKinds)
		{
			Description = $"Benchmark-optimized pipeline for {supportedMessageKinds} with zero middleware overhead",
		};

		// No middleware needed - correlation is now handled at the Dispatcher level (Sprint 70)
		return profile;
	}

	/// <summary>
	/// Validates that a pipeline profile is optimized for high-performance scenarios.
	/// </summary>
	/// <param name="profile"> The pipeline profile to validate. </param>
	/// <returns> A validation result indicating whether the profile is optimized. </returns>
	public static PipelineValidationResult ValidateHotPathOptimization(PipelineProfile profile)
	{
		ArgumentNullException.ThrowIfNull(profile);

		var middlewareCount = profile.MiddlewareTypes.Count;
		var result = new PipelineValidationResult();

		// Optimized profiles should have minimal middleware
		if (middlewareCount == 0)
		{
			result.IsOptimized = true;
			result.Complexity = PipelineComplexity.Direct;
			result.Notes.Add("Zero middleware - maximum performance");
		}
		else if (middlewareCount == 1)
		{
			result.IsOptimized = true;
			result.Complexity = PipelineComplexity.Minimal;
			result.Notes.Add("Single middleware - excellent performance");
		}
		else if (middlewareCount <= 3)
		{
			result.IsOptimized = true;
			result.Complexity = PipelineComplexity.Reduced;
			result.Notes.Add("Low middleware count - good performance");
		}
		else
		{
			result.IsOptimized = false;
			result.Complexity = PipelineComplexity.Standard;
			result.Notes.Add($"High middleware count ({middlewareCount}) - consider reducing for hot-path scenarios");
		}

		// Check for hot-path optimized middleware
		var hasHotPathMiddleware =
			profile.MiddlewareTypes.Any(static t => t.Namespace?.Contains("HotPath", StringComparison.Ordinal) == true);
		if (hasHotPathMiddleware)
		{
			result.Notes.Add("Uses hot-path optimized middleware");
		}

		return result;
	}
}
