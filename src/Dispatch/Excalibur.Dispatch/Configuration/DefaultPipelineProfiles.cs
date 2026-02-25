// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Middleware;

namespace Excalibur.Dispatch.Configuration;

/// <summary>
/// Provides default pipeline profiles for the dispatch system. Implements requirements R7.5-R7.12.
/// </summary>
public static class DefaultPipelineProfiles
{
	/// <summary>
	/// Gets the default pipeline profile name.
	/// </summary>
	public const string Default = "default";

	/// <summary>
	/// Gets the strict pipeline profile name for external/partner inputs.
	/// </summary>
	public const string Strict = "strict";

	/// <summary>
	/// Gets the internal event pipeline profile name.
	/// </summary>
	public const string InternalEvent = "internal-event";

	/// <summary>
	/// Gets the batch/backfill pipeline profile name.
	/// </summary>
	public const string Batch = "batch";

	/// <summary>
	/// Gets the direct pipeline profile name for high-frequency message processing.
	/// </summary>
	public const string Direct = "direct";

	/// <summary>
	/// Creates the default pipeline profile with canonical middleware ordering. Implements requirement R7.6 baseline order.
	/// </summary>
	public static PipelineProfile CreateDefaultProfile()
	{
		var profile = new PipelineProfile(Default, MessageKinds.All)
		{
			Description = "Default pipeline profile with canonical middleware ordering",
		};

		// R7.6 Default Baseline Order
		// Note: CorrelationMiddleware removed in Sprint 70 - correlation now handled at Dispatcher level
		profile.AddMiddleware<TenantIdentityMiddleware>(1); // 1. TenantIdentityMiddleware (All)
		profile.AddMiddleware<ContractVersionCheckMiddleware>(2); // 2. ContractVersionCheckMiddleware (Event|Document)
		profile.AddMiddleware<ValidationMiddleware>(3); // 3. ValidationMiddleware (Action)
		profile.AddMiddleware<AuthorizationMiddleware>(4); // 4. AuthorizationMiddleware (Action)
		profile.AddMiddleware<TimeoutMiddleware>(5); // 5. TimeoutMiddleware (Action|Event)
		profile.AddMiddleware<TransactionMiddleware>(6); // 6. TransactionMiddleware (Action)
		profile.AddMiddleware<OutboxStagingMiddleware>(7); // 7. OutboxStagingMiddleware (Action|Event)
		profile.AddMiddleware<MetricsLoggingMiddleware>(8); // 8. MetricsLoggingMiddleware (All)

		return profile;
	}

	/// <summary>
	/// Creates the strict pipeline profile for external/partner inputs. Includes full validation, authentication, authorization, and rate limiting.
	/// </summary>
	public static PipelineProfile CreateStrictProfile()
	{
		var profile = new PipelineProfile(Strict, MessageKinds.Action | MessageKinds.Event)
		{
			Description = "Strict pipeline for external/partner inputs with full validation and security",
		};

		// Order matters - security checks first
		// Note: CorrelationMiddleware removed in Sprint 70 - correlation now handled at Dispatcher level
		profile.AddMiddleware<RateLimitingMiddleware>(1);
		profile.AddMiddleware<AuthenticationMiddleware>(2);
		profile.AddMiddleware<TenantIdentityMiddleware>(3);
		profile.AddMiddleware<InputSanitizationMiddleware>(4);
		profile.AddMiddleware<ValidationMiddleware>(5);
		profile.AddMiddleware<AuthorizationMiddleware>(6);
		profile.AddMiddleware<ContractVersionCheckMiddleware>(7);
		profile.AddMiddleware<TimeoutMiddleware>(8);
		profile.AddMiddleware<CircuitBreakerMiddleware>(9);
		profile.AddMiddleware<TransactionMiddleware>(10);
		profile.AddMiddleware<OutboxStagingMiddleware>(11);
		profile.AddMiddleware<AuditLoggingMiddleware>(12);
		profile.AddMiddleware<MetricsLoggingMiddleware>(13);

		return profile;
	}

	/// <summary>
	/// Creates the internal event pipeline profile. Minimal overhead for trusted internal event processing.
	/// </summary>
	public static PipelineProfile CreateInternalEventProfile()
	{
		var profile = new PipelineProfile(InternalEvent, MessageKinds.Event)
		{
			Description = "Lightweight pipeline for internal event processing",
		};

		// Minimal middleware for internal events
		// Note: CorrelationMiddleware removed in Sprint 70 - correlation now handled at Dispatcher level
		profile.AddMiddleware<TenantIdentityMiddleware>(1);
		profile.AddMiddleware<ContractVersionCheckMiddleware>(2);
		profile.AddMiddleware<TimeoutMiddleware>(3);
		profile.AddMiddleware<OutboxStagingMiddleware>(4);
		profile.AddMiddleware<MetricsLoggingMiddleware>(5);

		return profile;
	}

	/// <summary>
	/// Creates the batch/backfill pipeline profile. Optimized for high-throughput batch processing.
	/// </summary>
	public static PipelineProfile CreateBatchProfile()
	{
		var profile = new PipelineProfile(Batch, MessageKinds.All)
		{
			Description = "Optimized pipeline for batch processing and backfill operations",
		};

		// Minimal middleware for batch processing
		// Note: CorrelationMiddleware removed in Sprint 70 - correlation now handled at Dispatcher level
		profile.AddMiddleware<UnifiedBatchingMiddleware>(1);
		profile.AddMiddleware<MetricsLoggingMiddleware>(2);

		return profile;
	}

	/// <summary>
	/// Creates the direct pipeline profile for high-frequency message processing. Minimizes middleware overhead for maximum throughput
	/// scenarios. Implements R7.12.
	/// </summary>
	/// <remarks>
	/// Correlation and context management is handled directly in the Dispatcher,
	/// allowing direct profiles to have zero middleware overhead while still maintaining message tracing.
	/// </remarks>
	public static PipelineProfile CreateDirectProfile()
	{
		var profile = new PipelineProfile(Direct, MessageKinds.All)
		{
			Description = "Ultra-lightweight pipeline for direct message processing with zero middleware overhead",
		};

		// No middleware needed - correlation is now handled at the Dispatcher level (Sprint 70)
		// This provides maximum throughput with zero allocation overhead
		return profile;
	}

	/// <summary>
	/// Registers all default profiles with the given registry.
	/// </summary>
	public static void RegisterDefaultProfiles(IPipelineProfileRegistry registry)
	{
		ArgumentNullException.ThrowIfNull(registry);

		RegisterIfMissing(registry, Default, CreateDefaultProfile);
		RegisterIfMissing(registry, Strict, CreateStrictProfile);
		RegisterIfMissing(registry, InternalEvent, CreateInternalEventProfile);
		RegisterIfMissing(registry, Batch, CreateBatchProfile);
		RegisterIfMissing(registry, Direct, CreateDirectProfile);
	}

	private static void RegisterIfMissing(
		IPipelineProfileRegistry registry,
		string profileName,
		Func<PipelineProfile> profileFactory)
	{
		if (registry.GetProfile(profileName) is null)
		{
			registry.RegisterProfile(profileFactory());
		}
	}
}
