// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Domain.Model;

/// <summary>
/// Enriches domain events with cross-cutting metadata before they are stored.
/// </summary>
/// <remarks>
/// <para>
/// Implementations can add metadata such as:
/// <list type="bullet">
/// <item><description>Correlation IDs</description></item>
/// <item><description>Causation IDs</description></item>
/// <item><description>User/tenant identifiers</description></item>
/// <item><description>Timestamps (server-side)</description></item>
/// <item><description>Source system/service identifiers</description></item>
/// </list>
/// </para>
/// <para>
/// Multiple enrichers can be composed via DI registration. Each enricher is invoked
/// in registration order before the event is persisted.
/// </para>
/// <para>
/// Pattern follows <c>Microsoft.Extensions.Logging</c> enrichment approach:
/// minimal interface (1 method), composable via DI.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public class CorrelationIdEnricher : IDomainEventEnricher
/// {
///     private readonly ICorrelationContext _context;
///
///     public Task&lt;IDomainEvent&gt; EnrichAsync(IDomainEvent @event, CancellationToken ct)
///     {
///         var metadata = @event.Metadata ?? new Dictionary&lt;string, object&gt;();
///         metadata["CorrelationId"] = _context.CorrelationId;
///         return Task.FromResult(@event);
///     }
/// }
/// </code>
/// </example>
public interface IDomainEventEnricher
{
	/// <summary>
	/// Enriches a domain event with additional metadata before storage.
	/// </summary>
	/// <param name="event">The domain event to enrich.</param>
	/// <param name="cancellationToken">A token that is observed for cancellation.</param>
	/// <returns>The enriched domain event, which may be the same instance or a new one with metadata applied.</returns>
	Task<IDomainEvent> EnrichAsync(IDomainEvent @event, CancellationToken cancellationToken);
}
