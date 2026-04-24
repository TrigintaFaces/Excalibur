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
		TimeSpan? cacheTtl = null,
		Func<string, CancellationToken, Task>? deleteAction = null,
		Type? storeType = null,
		ProjectionOptions? options = null)
	{
		ProjectionType = projectionType;
		Mode = mode;
		Projection = projection;
		InlineApply = inlineApply;
		CacheTtl = cacheTtl;
		DeleteAction = deleteAction;
		StoreType = storeType;
		Options = options;
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
	/// Gets the pre-bound delegate for applying events to this projection.
	/// Set for both <see cref="ProjectionMode.Inline"/> and <see cref="ProjectionMode.Async"/>
	/// modes. Null only for <see cref="ProjectionMode.Ephemeral"/> projections.
	/// </summary>
	internal InlineApplyDelegate? InlineApply { get; }

	/// <summary>
	/// Gets the optional cache TTL for ephemeral projection caching via IDistributedCache.
	/// Null means no caching.
	/// </summary>
	internal TimeSpan? CacheTtl { get; }

	/// <summary>
	/// Gets the optional deletion handler registered via <c>WhenDeleted</c> (R27.23).
	/// Null means no deletion handler is configured.
	/// </summary>
	internal Func<string, CancellationToken, Task>? DeleteAction { get; }

	/// <summary>
	/// Gets the optional store type override registered via <c>WithStore&lt;TStore&gt;</c>.
	/// Null means the default DI-resolved <c>IProjectionStore&lt;T&gt;</c> is used.
	/// </summary>
	internal Type? StoreType { get; }

	/// <summary>
	/// Gets the optional per-projection options configured via <c>WithOptions</c>.
	/// Null means default options apply.
	/// </summary>
	internal ProjectionOptions? Options { get; }
}
