// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Aggregates all annotations for a single audit event, grouped by type.
/// </summary>
/// <param name="EventId">The audit event these annotations belong to.</param>
/// <param name="Tags">Tag labels applied to the event.</param>
/// <param name="Bookmarks">Bookmark annotations on the event.</param>
/// <param name="Notes">Free-text note annotations on the event.</param>
public sealed record AuditAnnotations(
	string EventId,
	IReadOnlyList<string> Tags,
	IReadOnlyList<AuditAnnotation> Bookmarks,
	IReadOnlyList<AuditAnnotation> Notes);
