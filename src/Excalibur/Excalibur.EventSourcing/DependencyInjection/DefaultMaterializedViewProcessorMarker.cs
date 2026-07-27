// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.EventSourcing.DependencyInjection;

/// <summary>
/// Registered only when the built-in materialized view processor won registration — that is, when the
/// consumer did not supply their own through <c>UseProcessor&lt;TProcessor&gt;()</c>.
/// </summary>
/// <remarks>
/// <para>
/// This records a composition fact and nothing else: which implementation of
/// <c>IMaterializedViewProcessor</c> is in the container. It is registered by reading the winning service
/// descriptor after configuration completes, so it cannot disagree with the container it describes.
/// </para>
/// <para>
/// It exists because the built-in processor's persistence contract — an atomic view-and-checkpoint write —
/// is the framework's to guarantee, while a consumer-supplied processor owns its own. A startup check that
/// could not tell the two apart would either excuse a broken framework configuration or reject a legitimate
/// custom one.
/// </para>
/// <para>
/// A marker is only ever evidence of the registration that placed it. It says which processor is wired; it
/// says nothing about whether that processor can persist anything. That question is answered by resolving
/// the store and asking it.
/// </para>
/// </remarks>
internal sealed class DefaultMaterializedViewProcessorMarker;
