// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Represents an annotation (tag, bookmark, or note) attached to an audit event.
/// </summary>
/// <remarks>
/// Annotations are additive, immutable records that never modify the original
/// <see cref="AuditEvent"/> or its hash chain. They are stored separately
/// and linked by <see cref="EventId"/>.
/// </remarks>
public sealed record AuditAnnotation
{
	/// <summary>
	/// Gets the unique identifier of this annotation.
	/// </summary>
	public required string Id { get; init; }

	/// <summary>
	/// Gets the identifier of the audit event this annotation is attached to.
	/// </summary>
	public required string EventId { get; init; }

	/// <summary>
	/// Gets the type of annotation (tag, bookmark, or note).
	/// </summary>
	public required AuditAnnotationType Type { get; init; }

	/// <summary>
	/// Gets the content of the annotation: tag label, bookmark label, or note text.
	/// </summary>
	public required string Content { get; init; }

	/// <summary>
	/// Gets the identifier of the actor who created this annotation.
	/// </summary>
	public required string ActorId { get; init; }

	/// <summary>
	/// Gets the timestamp when this annotation was created.
	/// </summary>
	public required DateTimeOffset CreatedAt { get; init; }

	/// <summary>
	/// Gets the visibility of this annotation (personal or shared).
	/// </summary>
	public required AuditAnnotationVisibility Visibility { get; init; }
}
