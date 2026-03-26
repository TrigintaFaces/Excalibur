// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.EventSourcing.Abstractions;

namespace Excalibur.EventSourcing.Projections;

/// <summary>
/// Represents a registered projection with its mode, event handler dispatch table,
/// and a pre-bound delegate for AOT-safe inline processing.
/// </summary>
internal sealed class ProjectionRegistration
{
	/// <summary>
	/// Delegate type for applying events to a projection without reflection.
	/// Captured at registration time when the generic type is known.
	/// </summary>
	internal delegate Task InlineApplyDelegate(
		IReadOnlyList<IDomainEvent> events,
		EventNotificationContext context,
		IServiceProvider serviceProvider,
		CancellationToken cancellationToken);

	internal ProjectionRegistration(
		Type projectionType,
		ProjectionMode mode,
		object projection,
		InlineApplyDelegate? inlineApply,
		TimeSpan? cacheTtl = null)
	{
		ProjectionType = projectionType;
		Mode = mode;
		Projection = projection;
		InlineApply = inlineApply;
		CacheTtl = cacheTtl;
	}

	/// <summary>
	/// Gets the CLR type of the projection.
	/// </summary>
	internal Type ProjectionType { get; }

	/// <summary>
	/// Gets how this projection processes events.
	/// </summary>
	internal ProjectionMode Mode { get; }

	/// <summary>
	/// Gets the multi-stream projection containing event handlers.
	/// </summary>
	internal object Projection { get; }

	/// <summary>
	/// Gets the pre-bound delegate for inline projection processing.
	/// Null for non-inline projections.
	/// </summary>
	internal InlineApplyDelegate? InlineApply { get; }

	/// <summary>
	/// Gets the optional cache TTL for ephemeral projection caching via IDistributedCache.
	/// Null means no caching.
	/// </summary>
	internal TimeSpan? CacheTtl { get; }
}
