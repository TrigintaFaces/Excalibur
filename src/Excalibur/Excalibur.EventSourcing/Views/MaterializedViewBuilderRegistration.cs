// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

namespace Excalibur.EventSourcing.Views;

/// <summary>
/// Registration record for materialized view builders used for service discovery.
/// </summary>
/// <param name="ViewType">The type of view being built.</param>
/// <param name="BuilderType">The type of the builder implementation.</param>
/// <param name="BuilderInstance">The builder instance.</param>
/// <param name="Accessor">
/// A strongly-typed store accessor for <paramref name="ViewType"/>, constructed at registration time
/// (where the view type is statically known) so the processor never reflects at runtime.
/// </param>
/// <param name="Semantics">
/// The delivery guarantee the projection declared (<see cref="IMaterializedViewBuilder{TView}.DeliverySemantics"/>),
/// read from the builder at registration. Gates the atomic-store requirement: an
/// <see cref="ViewDeliverySemantics.ExactlyOnce"/> view requires an atomic store, an
/// <see cref="ViewDeliverySemantics.AtLeastOnceIdempotent"/> view may run on a non-atomic one.
/// </param>
internal sealed record MaterializedViewBuilderRegistration(
	Type ViewType,
	[property: DynamicallyAccessedMembers(
		DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicMethods)]
	Type BuilderType,
	object BuilderInstance,
	ViewStoreAccessor Accessor,
	ViewDeliverySemantics Semantics);
