// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.EventSourcing.Projections;

namespace Excalibur.EventSourcing.Tests.Projections;

/// <summary>
/// Adapts bare domain events into the shape the apply path takes.
/// </summary>
/// <remarks>
/// The apply delegate receives the aggregate id and the global position ON each event, because a batch
/// can span aggregates and a projection keyed by anything other than the aggregate must still be folded
/// in stream order. These tests supply one aggregate at a time, so the id is uniform and the position
/// is only needed where the test asserts on it.
/// </remarks>
internal static class ProjectionEventTestExtensions
{
	/// <summary>Wraps events as coming from one aggregate, with no global position.</summary>
	internal static IReadOnlyList<ProjectionEvent> AsProjectionEvents(
		this IEnumerable<IDomainEvent> events,
		string aggregateId) =>
		[.. events.Select(e => new ProjectionEvent(e, aggregateId, GlobalPosition: null))];

	/// <summary>Wraps events as coming from one aggregate, numbering them from 1 in order.</summary>
	internal static IReadOnlyList<ProjectionEvent> AsPositionedProjectionEvents(
		this IEnumerable<IDomainEvent> events,
		string aggregateId) =>
		[.. events.Select((e, i) => new ProjectionEvent(e, aggregateId, i + 1))];
}
