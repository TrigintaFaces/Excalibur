// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

namespace Excalibur.Compliance;

/// <summary>
/// Configuration options for the audit annotation store.
/// </summary>
public sealed class AuditAnnotationOptions
{
	/// <summary>
	/// Gets or sets the maximum number of tags allowed per audit event. Default is 50.
	/// </summary>
	[Range(1, 1000)]
	public int MaxTagsPerEvent { get; set; } = 50;

	/// <summary>
	/// Gets or sets the maximum length of a single tag label. Default is 128.
	/// </summary>
	[Range(1, 512)]
	public int MaxTagLength { get; set; } = 128;

	/// <summary>
	/// Gets or sets the maximum length of a note in characters. Default is 4000.
	/// </summary>
	[Range(1, 32_000)]
	public int MaxNoteLength { get; set; } = 4000;

	/// <summary>
	/// Gets or sets the maximum number of notes allowed per audit event. Default is 100.
	/// </summary>
	[Range(1, 10_000)]
	public int MaxNotesPerEvent { get; set; } = 100;

	/// <summary>
	/// Gets or sets a value indicating whether annotation creation emits a
	/// meta-audit event of type <see cref="AuditEventType.Administrative"/>.
	/// Default is <see langword="true"/>.
	/// </summary>
	public bool EmitMetaAuditEvents { get; set; } = true;
}
