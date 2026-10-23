// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.EventSourcing;

/// <summary>
/// A projection read, together with what can be said about how current it was.
/// </summary>
/// <typeparam name="TProjection">The projection type.</typeparam>
/// <param name="Projection">The projection, or <see langword="null"/> when no row exists for the id.</param>
/// <param name="Freshness">What the read can say about the projection's currency.</param>
/// <remarks>
/// The freshness travels WITH the value rather than being reported separately, because a caller that has
/// the projection in hand and has to go and ask a second question about it will mostly not ask.
/// </remarks>
public readonly record struct ProjectionRead<TProjection>(
	TProjection? Projection,
	ProjectionFreshness Freshness)
	where TProjection : class;
