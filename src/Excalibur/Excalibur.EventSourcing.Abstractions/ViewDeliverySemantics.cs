// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.EventSourcing;

/// <summary>
/// Declares the delivery guarantee a materialized-view projection requires from its view store.
/// </summary>
/// <remarks>
/// The guarantee is an intrinsic property of the projection's <see cref="IMaterializedViewBuilder{TView}.Apply"/>
/// logic — whether re-applying the same event changes the view — so it is declared by the projection author,
/// not the deployment. It selects the write path and, at startup, whether an atomic view store is required.
/// </remarks>
public enum ViewDeliverySemantics
{
	/// <summary>
	/// The projection is accumulating / non-idempotent: re-applying the same event changes the view (for
	/// example, a running total). It MUST run on an <see cref="IAtomicMaterializedViewStore"/> that persists
	/// the view and its checkpoint as one atomic operation, so a crash between the two writes cannot replay the
	/// last event and double-count. Wiring an exactly-once projection to a non-atomic store is refused at startup.
	/// </summary>
	ExactlyOnce = 0,

	/// <summary>
	/// The projection is idempotent (upsert-by-view-id): re-applying the same event yields the same view (for
	/// example, "set status = Shipped"). It tolerates the at-least-once replay a non-atomic store allows after a
	/// crash, so it may run on any view store — including Elasticsearch and OpenSearch, which have no cross-index
	/// transaction and therefore cannot persist a view and its checkpoint atomically.
	/// </summary>
	AtLeastOnceIdempotent = 1,
}
