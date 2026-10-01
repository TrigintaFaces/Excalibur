// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;

namespace Excalibur.EventSourcing.Projections;

/// <summary>
/// One event as the projection apply path needs to see it: the event itself, the aggregate it came
/// from, and where it sits in the global stream.
/// </summary>
/// <param name="Domain">The deserialized domain event.</param>
/// <param name="AggregateId">The aggregate this event was appended to.</param>
/// <param name="GlobalPosition">
/// Its position in the global stream, or <see langword="null"/> on the save path, where the event is
/// being committed and no global position has been assigned yet.
/// </param>
/// <remarks>
/// <para>
/// <b>Why the aggregate id travels PER EVENT rather than once per call.</b> It used to arrive on the
/// notification context, which forced the caller to dispatch one call per aggregate. That is correct
/// for a projection keyed by aggregate and WRONG for one keyed by anything else: a keyed projection
/// maps events from many aggregates onto one projection id, so it was written once per aggregate group,
/// and group order is not stream order. The same id could therefore be written at position 7 and then
/// at position 6, in one batch, with no concurrency involved.
/// </para>
/// <para>
/// Carrying the id on the event lets the whole batch be applied in ONE call, in global order, with each
/// projection id loaded once, folded once and written once.
/// </para>
/// </remarks>
internal readonly record struct ProjectionEvent(
	IDomainEvent Domain,
	string AggregateId,
	long? GlobalPosition);


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
		IReadOnlyList<ProjectionEvent> events,
		EventNotificationContext context,
		IServiceProvider serviceProvider,
		CancellationToken cancellationToken);

	/// <summary>
	/// Delegate type for clearing one erased aggregate's contribution from this projection without
	/// reflection. Captured at registration time when the generic type is known.
	/// </summary>
	internal delegate Task ClearForAggregateDelegate(
		IServiceProvider serviceProvider,
		string aggregateId,
		string aggregateType,
		CancellationToken cancellationToken);

	internal ProjectionRegistration(
		Type projectionType,
		ProjectionMode mode,
		object projection,
		InlineApplyDelegate? inlineApply,
		TimeSpan? cacheTtl = null,
		Func<string, CancellationToken, Task>? deleteAction = null,
		Type? storeType = null,
		ProjectionOptions? options = null,
		Func<object, string>? searchTextComputer = null,
		Action<object, string>? searchTextSetter = null,
		ClearForAggregateDelegate? clearForAggregate = null)
	{
		ClearForAggregate = clearForAggregate;
		ProjectionType = projectionType;
		Mode = mode;
		Projection = projection;
		InlineApply = inlineApply;
		CacheTtl = cacheTtl;
		DeleteAction = deleteAction;
		StoreType = storeType;
		Options = options;
		SearchTextComputer = searchTextComputer;
		SearchTextSetter = searchTextSetter;
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
	/// Gets the pre-bound delegate that clears one erased aggregate's contribution from this
	/// projection, or <see langword="null"/> when this projection cannot be cleared one aggregate at a
	/// time.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>Null is a statement, not an omission.</b> It is bound only for a PERSISTED projection whose
	/// projection id IS the aggregate id — a projection registering a <c>KeyedBy</c> selector maps many
	/// aggregates onto one row, so replaying a single aggregate could not produce a correct row for such
	/// a key, and an EPHEMERAL projection persists no row for an erasure to miss. Binding it only where
	/// it can work makes the unclearable case inexpressible rather than guarded: no code path attempts
	/// it, so no runtime check can be got wrong later.
	/// </para>
	/// <para>
	/// GDPR erasure reads this to clear what it can and to NAME what it cannot. Both readers — the
	/// erasure contributor that invokes it and the gap report that enumerates the projections without
	/// one — key on this same property, so the report cannot drift from the wiring it describes.
	/// </para>
	/// </remarks>
	internal ClearForAggregateDelegate? ClearForAggregate { get; }

	/// <summary>
	/// Gets the optional cache TTL for ephemeral projection caching via IDistributedCache.
	/// Null means no caching.
	/// </summary>
	internal TimeSpan? CacheTtl { get; }

	/// <summary>
	/// Gets the optional deletion handler registered via <c>WhenDeleted</c>.
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

	/// <summary>
	/// Gets the type-erased search text computation function configured via <c>WithSearchText</c>.
	/// Accepts the projection as <c>object</c> and returns the computed search text.
	/// Null means no search text computation is configured.
	/// </summary>
	internal Func<object, string>? SearchTextComputer { get; }

	/// <summary>
	/// Gets the type-erased search text setter configured via <c>WithSearchText</c>.
	/// Accepts the projection as <c>object</c> and the computed search text string.
	/// Null means no search text computation is configured.
	/// </summary>
	internal Action<object, string>? SearchTextSetter { get; }
}
