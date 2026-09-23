// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Dispatch;

/// <summary>
/// Marks middleware that ESTABLISHES the ambient tenant for the rest of the pipeline.
/// </summary>
/// <remarks>
/// <para>
/// The pipeline asserts ordering between these markers rather than between named types, and that is
/// the point of them: the durable statement is <b>"anything that reads tenant context runs after the
/// thing that establishes it"</b>. Which concrete middleware happens to establish it is an accident of
/// today's middleware set, and a rule written against a type list cannot cover a middleware that does
/// not exist yet — including one written by a consumer of this framework.
/// </para>
/// <para>
/// A middleware establishes tenant context when it opens an ambient scope that remains open for the
/// duration of the call to the next middleware. Implement this only if that is true of your type:
/// the pipeline uses it to decide what is allowed to run before you.
/// </para>
/// </remarks>
public interface IEstablishesTenantContext
{
}

/// <summary>
/// Marks middleware that READS the ambient tenant, and so must not run before it is established.
/// </summary>
/// <remarks>
/// <para>
/// Implementing this is how a middleware — ours or a consumer's — obtains the ordering guarantee. The
/// hazard it closes is silent: middleware are ordered by <see cref="DispatchMiddlewareStage"/> with a
/// STABLE sort, so two components in the same stage are ordered by registration order alone. A
/// tenant-reading middleware registered before the establisher therefore runs OUTSIDE the ambient
/// scope and observes no tenant, with no exception and no log.
/// </para>
/// <para>
/// This states the reason as well as the fact. "Runs after TenantIdentityMiddleware" records what;
/// <c>IRequiresTenantContext</c> records WHY, so the next reader does not have to reconstruct the
/// intent from an ordering table.
/// </para>
/// </remarks>
public interface IRequiresTenantContext
{
}
