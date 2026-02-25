// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.AuditLogging.Encryption;

/// <summary>
/// Options controlling which audit event fields are encrypted at rest.
/// </summary>
/// <remarks>
/// <para>
/// By default, only the <see cref="EncryptActorId"/> and <see cref="EncryptIpAddress"/> fields
/// are encrypted, as these are the most common PII-bearing fields in audit events.
/// </para>
/// <para>
/// The <see cref="EncryptionPurpose"/> is passed to the <see cref="Compliance.EncryptionContext"/>
/// to allow key selection policies to differentiate audit encryption from other uses.
/// </para>
/// </remarks>
public sealed class AuditEncryptionOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether the <see cref="Compliance.AuditEvent.ActorId"/> field is encrypted.
	/// </summary>
	/// <value><see langword="true"/> by default.</value>
	public bool EncryptActorId { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether the <see cref="Compliance.AuditEvent.IpAddress"/> field is encrypted.
	/// </summary>
	/// <value><see langword="true"/> by default.</value>
	public bool EncryptIpAddress { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether the <see cref="Compliance.AuditEvent.Reason"/> field is encrypted.
	/// </summary>
	/// <value><see langword="false"/> by default.</value>
	public bool EncryptReason { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether the <see cref="Compliance.AuditEvent.UserAgent"/> field is encrypted.
	/// </summary>
	/// <value><see langword="false"/> by default.</value>
	public bool EncryptUserAgent { get; set; }

	/// <summary>
	/// Gets or sets the encryption purpose passed to the encryption context for key selection.
	/// </summary>
	/// <value>"audit-event-field" by default.</value>
	public string EncryptionPurpose { get; set; } = "audit-event-field";
}
