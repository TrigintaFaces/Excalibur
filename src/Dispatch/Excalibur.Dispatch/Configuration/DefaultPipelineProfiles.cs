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

using Excalibur.Dispatch.Options.Middleware;
using Excalibur.Dispatch.Serialization;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Configuration;

/// <summary>
/// Provides default pipeline profiles for the dispatch system.
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
	/// Creates the default pipeline profile with canonical middleware ordering.
	/// </summary>
	public static IPipelineProfile CreateDefaultProfile()
	{
		var profile = new PipelineProfile(Default, MessageKinds.All)
		{
			Description = "Default pipeline profile with canonical middleware ordering",
		};

		// Default baseline order
		// Note: CorrelationMiddleware removed - correlation now handled at Dispatcher level
		// AuthorizationMiddleware is intentionally NOT in the Default profile: it is a security-sensitive
		// middleware that depends on consumer-supplied authorization services. Because profile materialization
		// null-skips any middleware whose services are unregistered (Microsoft fail-open), including it here
		// would silently no-op when a consumer selects "Default" without wiring auth — a silent authorization
		// bypass. Authorization is opt-in via the Strict profile, which the consumer deliberately selects.
		// Every entry states its criticality EXPLICITLY, for the same reason as the strict profile: a
		// shipped profile must not depend on the MiddlewareEntry default. See DefaultProfileMiddleware
		// below for the membership, the criterion that decides it, and why all entries are Optional.
		foreach (var entry in DefaultProfileMiddleware)
		{
			profile.AddMiddleware(entry.MiddlewareType, entry.Order, entry.Criticality);
		}

		return profile;
	}

	/// <summary>
	/// The single source of the default profile's membership: what it declares AND what
	/// <c>AddDispatch()</c> registers are both read from this list.
	/// </summary>
	/// <remarks>
	/// <para>
	/// A default-profile entry that the container does not receive is not a bug to be detected — it is
	/// a state that cannot be written down. Declaration and registration are two fields of one element,
	/// so they cannot disagree; adding an entry here registers it, and removing one un-registers it.
	/// </para>
	/// <para>
	/// Membership is decided by a criterion, not by a list somebody maintains: an entry belongs here if
	/// and only if it can be constructed from a bare service collection after <c>AddDispatch()</c> alone,
	/// with the consumer registering no infrastructure of their own. That is why
	/// <c>TransactionMiddleware</c> is absent — it requires a consumer-supplied transaction service, and
	/// seating it by default would make a zero-configuration host fail to dispatch. It stays available to
	/// any profile a consumer deliberately selects, exactly as authorization does.
	/// </para>
	/// <para>
	/// Each entry registers itself through an explicit closed generic rather than a reflected
	/// <see cref="Type"/>, so the set stays trim-safe and ahead-of-time friendly.
	/// </para>
	/// </remarks>
	internal static readonly DefaultMiddlewareEntry[] DefaultProfileMiddleware =
	[
		new(typeof(TenantIdentityMiddleware), 1, static s => s.TryAddScoped<TenantIdentityMiddleware>()),
		new(typeof(TimeoutMiddleware), 2, static s => s.TryAddScoped<TimeoutMiddleware>()),
		new(typeof(MetricsLoggingMiddleware), 3, static s => s.TryAddScoped<MetricsLoggingMiddleware>()),
		new(typeof(OutboxStagingMiddleware), 4, static s => s.TryAddScoped(static sp => new OutboxStagingMiddleware(
			sp.GetRequiredService<IOptions<OutboxStagingOptions>>(),
			sp.GetService<IOutboxStore>(),
			sp.GetRequiredService<DispatchJsonSerializer>(),
			sp.GetRequiredService<ILogger<OutboxStagingMiddleware>>()))),
	];

	// Measured against a bare service collection, not reasoned about. These three fail the criterion
	// today because a dependency of theirs is not supplied by AddDispatch(), and the container throws
	// rather than returning null when a registered middleware's own dependency cannot resolve:
	//
	//   ContractVersionCheckMiddleware  needs IContractVersionService
	//   ValidationMiddleware            needs IMessageValidationService
	// OutboxStagingMiddleware WAS listed here, and it did not belong. Its parameter is genuinely nullable
	// and its body already self-gates on the store, so it failed the criterion only on a DI-resolution
	// technicality -- constructor injection will not pass null for a parameter with no default, so the
	// container threw where GetService would have returned null. That is a registration detail, not a
	// missing infrastructure dependency, and filing it beside the two real ones above is what kept outbox
	// staging off the default pipeline. It is now seated through a factory that asks GetService, and it is
	// inert when no store answers.
	//
	// They remain available to any profile a consumer deliberately selects. Supplying framework defaults
	// for those services would move them back across the criterion, and that is a real improvement worth
	// making on its own terms — it is deliberately not bundled here, because each default is a design
	// decision about what the framework validates and versions on a consumer's behalf.

	/// <summary>
	/// One entry of the default profile: the type the profile declares, its order, and the registration
	/// that seats it in the container.
	/// </summary>
	/// <param name="MiddlewareType"> The middleware type the profile declares. </param>
	/// <param name="Order"> Position in the canonical ordering. </param>
	/// <param name="Register"> Seats the middleware in the container, using try-add semantics so a consumer registration wins. </param>
	internal sealed record DefaultMiddlewareEntry(
		Type MiddlewareType,
		int Order,
		Action<IServiceCollection> Register)
	{
		/// <summary>
		/// Gets the criticality every default-profile entry carries.
		/// </summary>
		/// <remarks>
		/// Optional, and stated here once rather than per entry. This profile declares no security
		/// boundary — the comment above records that authorization is deliberately absent precisely so
		/// that selecting "default" cannot look like authorization — so a Required entry would stop a
		/// host from starting while protecting nothing.
		/// </remarks>
		public MiddlewareCriticality Criticality => MiddlewareCriticality.Optional;
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

			// The registry auto-selects on this flag. Without it the profile carrying the Required
			// security middleware could never be chosen, and selection would fall through to a profile
			// that declares no security boundary.
			IsStrict = true,
		};

		// Order matters - security checks first
		// Note: CorrelationMiddleware removed - correlation now handled at Dispatcher level
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
		// Note: CorrelationMiddleware removed - correlation now handled at Dispatcher level
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
		// Note: CorrelationMiddleware removed - correlation now handled at Dispatcher level
		// Explicit criticality, as with every shipped profile. Batch processing declares no security
		// middleware, so neither entry gates a protection a consumer opted into.
		profile.AddMiddleware<UnifiedBatchingMiddleware>(1, MiddlewareCriticality.Optional);
		profile.AddMiddleware<MetricsLoggingMiddleware>(2, MiddlewareCriticality.Optional);

		return profile;
	}

	/// <summary>
	/// Creates the direct pipeline profile for high-frequency message processing. Minimizes middleware overhead for maximum throughput
	/// scenarios.
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

		// No middleware needed - correlation is now handled at the Dispatcher level
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
