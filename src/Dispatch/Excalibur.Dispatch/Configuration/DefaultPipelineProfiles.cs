// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Middleware.Auth;
using Excalibur.Dispatch.Middleware.Batch;
using Excalibur.Dispatch.Middleware.Logging;
using Excalibur.Dispatch.Middleware.Outbox;
using Excalibur.Dispatch.Middleware.Resilience;
using Excalibur.Dispatch.Middleware.Timeout;
using Excalibur.Dispatch.Middleware.Transaction;
using Excalibur.Dispatch.Middleware.Validation;
using Excalibur.Dispatch.Middleware.Versioning;

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
	public static IPipelineProfile CreateDefaultProfile()
	{
		var profile = new PipelineProfile(Default, MessageKinds.All)
		{
			Description = "Default pipeline profile with canonical middleware ordering",
		};

		// R7.6 Default Baseline Order
		// Note: CorrelationMiddleware removed in Sprint 70 - correlation now handled at Dispatcher level
		// AuthorizationMiddleware is intentionally NOT in the Default profile: it is a security-sensitive
		// middleware that depends on consumer-supplied authorization services. Because profile materialization
		// null-skips any middleware whose services are unregistered (Microsoft fail-open), including it here
		// would silently no-op when a consumer selects "Default" without wiring auth — a silent authorization
		// bypass. Authorization is opt-in via the Strict profile, which the consumer deliberately selects.
		// Every entry states its criticality EXPLICITLY, for the same reason as the strict profile: a
		// shipped profile must not depend on the MiddlewareEntry default.
		//
		// All seven are Optional, and that is not a weakening. This profile is what a consumer gets from
		// a bare registration with no configuration at all, and it declares no security boundary — the
		// comment above records that AuthorizationMiddleware is deliberately absent precisely so that
		// selecting "default" cannot look like authorization. Marking these Required would stop every
		// zero-configuration host from starting while protecting nothing, because the protection a
		// consumer must opt into is not declared here in the first place.
		profile.AddMiddleware<TenantIdentityMiddleware>(1, MiddlewareCriticality.Optional); // 1. TenantIdentityMiddleware (All)
		profile.AddMiddleware<ContractVersionCheckMiddleware>(2, MiddlewareCriticality.Optional); // 2. ContractVersionCheckMiddleware (Event|Document)
		profile.AddMiddleware<ValidationMiddleware>(3, MiddlewareCriticality.Optional); // 3. ValidationMiddleware (Action)
		profile.AddMiddleware<TimeoutMiddleware>(4, MiddlewareCriticality.Optional); // 4. TimeoutMiddleware (Action|Event)
		profile.AddMiddleware<TransactionMiddleware>(5, MiddlewareCriticality.Optional); // 5. TransactionMiddleware (Action)
		profile.AddMiddleware<OutboxStagingMiddleware>(6, MiddlewareCriticality.Optional); // 6. OutboxStagingMiddleware (Action|Event)
		profile.AddMiddleware<MetricsLoggingMiddleware>(7, MiddlewareCriticality.Optional); // 7. MetricsLoggingMiddleware (All)

		return profile;
	}

	/// <summary>
	/// Creates the strict pipeline profile for external/partner inputs. Includes authentication, authorization, tenant isolation, input
	/// sanitization and rate limiting, each of which the pipeline refuses to build without.
	/// </summary>
	public static IPipelineProfile CreateStrictProfile()
	{
		var profile = new PipelineProfile(Strict, MessageKinds.Action | MessageKinds.Event)
		{
			Description = "Strict pipeline for external/partner inputs with full validation and security",
		};

		// Order matters - security checks first
		// Note: CorrelationMiddleware removed in Sprint 70 - correlation now handled at Dispatcher level
		// Every entry states its criticality EXPLICITLY. A shipped profile must not depend on the
		// MiddlewareEntry default, so a future change to that default cannot silently alter what the
		// framework ships. The default governs consumer-authored entries only.
		//
		// The five Required entries are the protections a consumer is deliberately asking for when they
		// select "strict" for external and partner traffic. If one of them cannot be materialized, the
		// build fails and names it, rather than serving hostile traffic through a pipeline that silently
		// lacks it. The remainder are infrastructure whose absence degrades behaviour without removing a
		// security boundary, so they are skipped and logged as before.
		profile.AddMiddleware<ThrottlingMiddleware>(1, MiddlewareCriticality.Required);
		profile.AddMiddleware<AuthenticationMiddleware>(2, MiddlewareCriticality.Required);
		profile.AddMiddleware<TenantIdentityMiddleware>(3, MiddlewareCriticality.Required);
		profile.AddMiddleware<InputSanitizationMiddleware>(4, MiddlewareCriticality.Required);
		profile.AddMiddleware<ValidationMiddleware>(5, MiddlewareCriticality.Optional);
		profile.AddMiddleware<AuthorizationMiddleware>(6, MiddlewareCriticality.Required);
		profile.AddMiddleware<ContractVersionCheckMiddleware>(7, MiddlewareCriticality.Optional);
		profile.AddMiddleware<TimeoutMiddleware>(8, MiddlewareCriticality.Optional);
		profile.AddMiddleware<CircuitBreakerMiddleware>(9, MiddlewareCriticality.Optional);
		profile.AddMiddleware<TransactionMiddleware>(10, MiddlewareCriticality.Optional);
		profile.AddMiddleware<OutboxStagingMiddleware>(11, MiddlewareCriticality.Optional);
		profile.AddMiddleware<AuditLoggingMiddleware>(12, MiddlewareCriticality.Optional);
		profile.AddMiddleware<MetricsLoggingMiddleware>(13, MiddlewareCriticality.Optional);

		return profile;
	}

	/// <summary>
	/// Creates the internal event pipeline profile. Minimal overhead for trusted internal event processing.
	/// </summary>
	public static IPipelineProfile CreateInternalEventProfile()
	{
		var profile = new PipelineProfile(InternalEvent, MessageKinds.Event)
		{
			Description = "Lightweight pipeline for internal event processing",
		};

		// Minimal middleware for internal events
		// Note: CorrelationMiddleware removed in Sprint 70 - correlation now handled at Dispatcher level
		// Explicit criticality, as with every shipped profile: none of these may depend on the
		// MiddlewareEntry default. Internal events are already inside the trust boundary and this profile
		// declares no security middleware, so an unresolvable entry degrades behaviour rather than
		// removing a protection the consumer asked for.
		profile.AddMiddleware<TenantIdentityMiddleware>(1, MiddlewareCriticality.Optional);
		profile.AddMiddleware<ContractVersionCheckMiddleware>(2, MiddlewareCriticality.Optional);
		profile.AddMiddleware<TimeoutMiddleware>(3, MiddlewareCriticality.Optional);
		profile.AddMiddleware<OutboxStagingMiddleware>(4, MiddlewareCriticality.Optional);
		profile.AddMiddleware<MetricsLoggingMiddleware>(5, MiddlewareCriticality.Optional);

		return profile;
	}

	/// <summary>
	/// Creates the batch/backfill pipeline profile. Optimized for high-throughput batch processing.
	/// </summary>
	public static IPipelineProfile CreateBatchProfile()
	{
		var profile = new PipelineProfile(Batch, MessageKinds.All)
		{
			Description = "Optimized pipeline for batch processing and backfill operations",
		};

		// Minimal middleware for batch processing
		// Note: CorrelationMiddleware removed in Sprint 70 - correlation now handled at Dispatcher level
		// Explicit criticality, as with every shipped profile. Batch processing declares no security
		// middleware, so neither entry gates a protection a consumer opted into.
		profile.AddMiddleware<UnifiedBatchingMiddleware>(1, MiddlewareCriticality.Optional);
		profile.AddMiddleware<MetricsLoggingMiddleware>(2, MiddlewareCriticality.Optional);

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
	public static IPipelineProfile CreateDirectProfile()
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
		Func<IPipelineProfile> profileFactory)
	{
		if (registry.GetProfile(profileName) is null)
		{
			registry.RegisterProfile(profileFactory());
		}
	}
}
