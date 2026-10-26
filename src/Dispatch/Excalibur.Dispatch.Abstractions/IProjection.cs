// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


namespace Excalibur.Dispatch;

/// <summary>
/// Represents a read model projection that is built from domain events.
/// </summary>
/// <typeparam name="TKey"> The type of the key that uniquely identifies this projection. </typeparam>
/// <remarks>
/// Projections are read models that are derived from event streams and are used to support query scenarios in CQRS and Event Sourcing architectures.
/// </remarks>
public interface IProjection<out TKey>
	where TKey : notnull
{
	/// <summary>
	/// Gets the unique identifier for this projection.
	/// </summary>
	/// <value> The unique projection identifier. </value>
	TKey Id { get; }

	/// <summary>
	/// Gets the version this projection's state reflects.
	/// </summary>
	/// <value> The projection version number. </value>
	/// <remarks>
	/// <para>
	/// ASSIGN IT; DO NOT INCREMENT IT. <c>Version++</c> counts how many times a fold has run against
	/// this row, which is a different quantity from the one this property names. The two agree only
	/// when the row has folded every version from the first, contiguously, exactly once — so a
	/// projection registered later and filled from a mid-stream position, or a row re-folded by a
	/// rebuild or a recovery, reports a version its stream never had.
	/// </para>
	/// <para>
	/// WHAT TO ASSIGN follows from what the projection is keyed by. A read model of ONE aggregate
	/// assigns the committed version of the event it is folding, supplied to a DI-resolved handler as
	/// <c>ProjectionHandlerContext.CommittedVersion</c>. A projection keyed across SEVERAL streams has
	/// no single aggregate version to name, so it assigns the event's global stream position instead:
	/// monotonic, and meaningful for a row that many streams write to.
	/// </para>
	/// <para>
	/// This value is yours — the framework never reads it. Suppressing an event a row has already
	/// folded is done against a separate position the store owns, which is not this property and is not
	/// present on the document you read back. So a wrong value here does not weaken that suppression;
	/// it misreports the row to whoever queries it, which is the harm to avoid.
	/// </para>
	/// </remarks>
	long Version { get; }

	/// <summary>
	/// Gets the timestamp when this projection was last updated.
	/// </summary>
	/// <value> The timestamp of the last update. </value>
	DateTimeOffset LastModified { get; }
}
