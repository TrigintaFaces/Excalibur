// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3.Audit;
using Excalibur.A3.Audit.Internal;
using Excalibur.Application;
using Excalibur.Dispatch.Abstractions;
using Excalibur.Domain;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods to wire the <see cref="AuditMiddleware"/> and its context
/// dependencies with a single call.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="AuditMiddleware"/> builds an <c>ActivityAudit</c> for every
/// <c>IAmAuditable</c> command that flows through the dispatch pipeline. The
/// middleware depends on <see cref="IActivityContext"/>, which in turn
/// requires <see cref="ITenantId"/>, <see cref="ICorrelationId"/>,
/// <see cref="IETag"/>, and <see cref="IClientAddress"/>. Consumers who opted
/// in to audit through <c>CommandBase&lt;T&gt; + IAmAuditable</c> previously
/// had to register every sibling by hand — a classic DX failure (see
/// <c>management/specs/conformance-minimal-wiring-spec.md</c>, bd-20ft0e).
/// </para>
/// <para>
/// <see cref="AddExcaliburAudit"/> bundles the full set. It is idempotent and
/// every dependency uses <c>TryAdd</c> semantics, so single-tenant hosts work
/// against an otherwise-empty <see cref="IServiceCollection"/> and
/// multi-tenant hosts override <see cref="ITenantId"/> with their own
/// resolver before calling this extension.
/// </para>
/// <para>
/// The consumer still registers an <see cref="IAuditMessagePublisher"/>
/// (production: Kafka / SNS / EventHubs / SIEM; sample: in-memory) — the
/// framework intentionally leaves the destination pluggable.
/// </para>
/// </remarks>
internal static class AuditServiceCollectionExtensions
{
	/// <summary>
	/// Registers <see cref="AuditMiddleware"/> and every context service it
	/// needs (<see cref="IActivityContext"/>, <see cref="ITenantId"/>,
	/// <see cref="ICorrelationId"/>, <see cref="IETag"/>,
	/// <see cref="IClientAddress"/>) with safe <c>TryAdd</c> defaults.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <returns>The service collection for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is <see langword="null"/>.</exception>
	/// <example>
	/// <code>
	/// services.AddExcalibur(x => x.AddAudit());
	/// services.AddSingleton&lt;IAuditMessagePublisher, MyAuditPublisher&gt;();
	/// </code>
	/// </example>
	internal static IServiceCollection AddExcaliburAudit(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		// Context sibling services — TryAdd so multi-tenant hosts can override
		// the ITenantId resolver to read a header / claim before calling this.
		_ = services.TryAddTenantId();
		_ = services.TryAddCorrelationId();
		_ = services.TryAddETag();
		_ = services.TryAddClientAddress();

		// ActivityContext aggregates the sibling context services.
		services.TryAddScoped<IActivityContext, ActivityContext>();

		// Framework-internal IOutboxDispatcher sibling — Shape 1 Bucket-A TryAdd
		// default so the Audit composition is wireable without an explicit Outbox
		// backend (S792 bd-drizep / ADR-322 §Decision-3). When AddExcaliburOutbox
		// is present, TryAdd is a no-op and the real MessageOutbox wins.
		services.TryAddSingleton<IOutboxDispatcher, DefaultOutboxDispatcher>();

		// The middleware itself — Scoped lifetime aligns with IActivityContext
		// (itself Scoped) so ServiceProviderOptions.ValidateScopes=true is clean.
		// TryAddEnumerable avoids double-registration when this extension is
		// called more than once (S792 bd-b5w27b / captive-dep fix).
		services.TryAddEnumerable(ServiceDescriptor.Scoped<IDispatchMiddleware, AuditMiddleware>());

		return services;
	}
}
