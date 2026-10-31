// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.EventSourcing.Projections;

/// <summary>
/// Derives a projection destination's apply path from the projection's REGISTERED MODE, so the fact is
/// declared once rather than twice.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this exists.</b> A destination's apply path was declarable in two places with nothing
/// reconciling them: the projection's mode (<see cref="ProjectionMode.TransactionalInline"/>, declared on
/// the projection builder) and the provider store's own destination kind. A consumer could state the two
/// inconsistently and no part of the system could tell. Whatever default the store chose only decided
/// which inconsistency was cheap to write — it could not make the pair agree.
/// </para>
/// <para>
/// <b>What it changes.</b> The mode is now the single declaration and the store's kind is derived from it
/// when the consumer has not stated one explicitly. A consumer who registers a projection
/// <c>TransactionalInline</c> therefore gets a transactional destination without naming it twice, and the
/// two surfaces that no test arm covers — the shipped sample and the documented registration snippets —
/// are correct without carrying a second declaration a reader has to remember.
/// </para>
/// <para>
/// <b>Order-free by construction.</b> This resolves at store-FACTORY time, not at registration time, so
/// it sees the completed registry however the consumer ordered their calls. Writing the derived value
/// into provider options during registration would have made the result depend on <c>Configure</c>
/// ordering, which is the kind of dependency nobody can see in a stack trace.
/// </para>
/// <para>
/// <b>An explicit choice always wins.</b> Callers apply this only when the consumer stated nothing, and
/// fall back to <see cref="ProjectionDestinationKind.Positioned"/> when there is no registration to read
/// — the case of a hand-built store, where construction is the only source of truth.
/// </para>
/// </remarks>
internal static class ProjectionDestinationKindResolver
{
    /// <summary>Reads the apply path implied by a projection's registered mode.</summary>
    /// <param name="services">The provider the store factory is resolving from.</param>
    /// <param name="projectionType">The projection's CLR type.</param>
    /// <returns>
    /// The implied kind, or <see langword="null"/> when nothing is registered for this projection — which
    /// is not an error: a store may legitimately be built with no registry present at all.
    /// </returns>
    internal static ProjectionDestinationKind? Resolve(IServiceProvider services, Type projectionType)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(projectionType);

        // GetService, never GetRequiredService: a projection store is usable without the registry, and a
        // resolver that threw when it found nothing would convert a supported configuration into a
        // startup failure.
        EventNotificationServiceCollectionExtensions.ConfigureProjections(services);
        var registry = services.GetService<IProjectionRegistry>();
        var mode = registry?.GetRegistration(projectionType)?.Mode;

        return mode switch
        {
            ProjectionMode.TransactionalInline => ProjectionDestinationKind.Transactional,

            // EVERY OTHER MODE LEAVES THE DECISION ALONE rather than asserting Positioned. The caller's
            // own fallback supplies that, so a mode added later does not silently acquire an apply path
            // here: it arrives with no opinion, which is the honest state for a value nobody decided.
            _ => null,
        };
    }
}
